using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizAPI.Models;
using QuizAPI.Models.Multiplayer;
using QuizAPI.Models.Multiplayer.MultiplayerRanked;
using QuizAPI.Services;
using LivePlayerScoreDto = QuizAPI.Models.Multiplayer.MultiplayerRanked.LivePlayerScoreDto;

namespace QuizAPI;

[Authorize]
public class RankedMultiplayerHub : Hub
{
    private readonly RankedMultiplayerManager _manager;
    private readonly DatabaseServices _db;

    private const int QuestionIntroSeconds = 4;
    private const int BaseCorrectPoints = 100;
    private const int MaxSpeedBonusPoints = 50;
    private const int ResultRevealSeconds = 2;
    private const int MatchmakingCountdownSeconds = 10;

    public RankedMultiplayerHub(RankedMultiplayerManager manager, DatabaseServices db)
    {
        _manager = manager;
        _db = db;
    }

    public async Task<RankedLobbyState> QuickMatchRanked()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = Context.User?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(userIdClaim) || string.IsNullOrWhiteSpace(username))
            throw new HubException("Unauthorized");

        if (!int.TryParse(userIdClaim, out var userId))
            throw new HubException("Invalid user id");

        var account = _db.GetUserById(userId);
        var avatarKey = account?.AvatarKey ?? "default_1";

        var state = _manager.QuickMatch(userId, Context.ConnectionId, username, avatarKey);
        var lobby = _manager.GetLobby(state.Code);
        if (lobby == null)
            throw new HubException("Lobby not found");

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);
        await Clients.Group(lobby.GroupName).SendAsync("RankedLobbyUpdated", _manager.GetState(lobby.Code)!);

        if (!lobby.IsStarted && lobby.Players.Count >= 2)
            StartMatchmakingCountdownIfNeeded(lobby);

        return _manager.GetState(lobby.Code)!;
    }

    public async Task<RankedLobbyState> GetRankedLobbyState(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null)
            throw new HubException("Lobby not found");

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

        var state = _manager.GetState(code);
        if (state == null)
            throw new HubException("Lobby not found");

        return state;
    }

    public Task<List<LivePlayerScoreDto>> GetLiveScores(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return Task.FromResult(new List<LivePlayerScoreDto>());

        lock (lobby)
        {
            return Task.FromResult(BuildLiveScores(lobby));
        }
    }

    public Task<TriviaQuestion?> GetCurrentRankedQuestion(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return Task.FromResult<TriviaQuestion?>(null);

        lock (lobby)
        {
            if (lobby.Questions.Count == 0) return Task.FromResult<TriviaQuestion?>(null);
            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count)
                return Task.FromResult<TriviaQuestion?>(null);

            return Task.FromResult<TriviaQuestion?>(lobby.Questions[lobby.CurrentQuestionIndex]);
        }
    }

    public async Task AnswerRankedQuestion(string code, string answer)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return;

        bool shouldResolveImmediately = false;

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            if (lobby.Questions.Count == 0) return;
            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count) return;

            if (DateTime.UtcNow < lobby.QuestionStartedAtUtc.AddSeconds(QuestionIntroSeconds))
                return;

            if (lobby.SubmittedAnswers.ContainsKey(Context.ConnectionId))
                return;

            var chosen = answer?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(chosen))
                return;

            lobby.SubmittedAnswers[Context.ConnectionId] = new RankedSubmittedAnswer
            {
                Answer = chosen,
                AnsweredAtUtc = DateTime.UtcNow
            };

            if (lobby.SubmittedAnswers.Count >= lobby.Players.Count)
            {
                shouldResolveImmediately = true;
                lobby.QuestionCts?.Cancel();
            }
        }

        if (shouldResolveImmediately)
            await ResolveQuestion(lobby);
    }

    private void StartMatchmakingCountdownIfNeeded(RankedLobby lobby)
    {
        lock (lobby)
        {
            if (lobby.IsStarted) return;
            if (lobby.IsMatchmaking) return;

            lobby.IsMatchmaking = true;
            lobby.MatchmakingEndsAtUtc = DateTime.UtcNow.AddSeconds(MatchmakingCountdownSeconds);

            lobby.MatchmakingCts?.Cancel();
            lobby.MatchmakingCts = new CancellationTokenSource();
            var token = lobby.MatchmakingCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(MatchmakingCountdownSeconds), token);
                }
                catch
                {
                    return;
                }

                bool shouldStart;
                lock (lobby)
                {
                    shouldStart = !lobby.IsStarted && lobby.Players.Count >= 2;
                    if (shouldStart)
                    {
                        lobby.IsStarted = true;
                        lobby.IsMatchmaking = false;
                        lobby.MatchmakingEndsAtUtc = null;
                    }
                }

                if (!shouldStart) return;

                await Clients.Group(lobby.GroupName).SendAsync("RankedLobbyUpdated", _manager.GetState(lobby.Code)!);
                await StartRankedGame(lobby);
            });
        }
    }

    private async Task StartRankedGame(RankedLobby lobby)
    {
        try
        {
            using var http = new HttpClient();

            var cats = lobby.Settings.CategoryIds.Count == 0
                ? ""
                : string.Join(",", lobby.Settings.CategoryIds);

            var url =
                $"http://localhost:5237/api/trivia?amount={lobby.Settings.Amount}" +
                $"&difficulty={lobby.Settings.Difficulty}" +
                $"&categories={cats}&fresh=true";

            var resp = await http.GetFromJsonAsync<TriviaResponse>(url);
            var questions = resp?.Results ?? new List<TriviaQuestion>();

            if (questions.Count == 0)
            {
                await Clients.Group(lobby.GroupName).SendAsync("RankedGameError", "No questions loaded.");
                return;
            }

            lock (lobby)
            {
                lobby.Questions = questions;
                lobby.CurrentQuestionIndex = 0;
                lobby.QuestionLocked = false;
                lobby.SubmittedAnswers.Clear();

                lobby.Scores.Clear();
                foreach (var player in lobby.Players)
                    lobby.Scores[player.ConnectionId] = 0;
            }

            await Clients.Group(lobby.GroupName).SendAsync("ScoresUpdated", BuildLiveScores(lobby));
            await SendQuestion(lobby);
        }
        catch (Exception ex)
        {
            await Clients.Group(lobby.GroupName).SendAsync("RankedGameError", $"Start failed: {ex.Message}");
        }
    }

    private async Task SendQuestion(RankedLobby lobby)
    {
        TriviaQuestion question;

        lock (lobby)
        {
            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count)
                return;

            lobby.QuestionStartedAtUtc = DateTime.UtcNow;
            lobby.QuestionLocked = false;
            lobby.SubmittedAnswers.Clear();

            lobby.QuestionCts?.Cancel();
            lobby.QuestionCts = new CancellationTokenSource();

            question = lobby.Questions[lobby.CurrentQuestionIndex];
        }

        await Clients.Group(lobby.GroupName).SendAsync("RankedNewQuestion", question);

        _ = Task.Run(async () =>
        {
            CancellationToken token;
            int seconds;
            lock (lobby)
            {
                token = lobby.QuestionCts!.Token;
                seconds = lobby.Settings.TimePerQuestion;
            }

            if (seconds <= 0) seconds = 15;
            seconds += QuestionIntroSeconds;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            }
            catch
            {
                return;
            }

            await ResolveQuestion(lobby);
        });
    }

    private async Task ResolveQuestion(RankedLobby lobby)
    {
        RankedQuestionResolutionDto resolution;
        List<LivePlayerScoreDto> liveScores;
        RankedGameFinishedDto? finishedDto = null;
        bool hasNextQuestion = false;

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            if (lobby.Questions.Count == 0) return;
            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count) return;

            lobby.QuestionLocked = true;
            lobby.QuestionCts?.Cancel();

            var currentQuestion = lobby.Questions[lobby.CurrentQuestionIndex];
            var answerWindowOpenedAt = lobby.QuestionStartedAtUtc.AddSeconds(QuestionIntroSeconds);
            var maxResponseMs = Math.Max(1, lobby.Settings.TimePerQuestion) * 1000.0;

            resolution = new RankedQuestionResolutionDto
            {
                CorrectAnswer = currentQuestion.CorrectAnswer,
                Results = new List<RankedQuestionPlayerAnswerDto>()
            };

            foreach (var player in lobby.Players)
            {
                lobby.SubmittedAnswers.TryGetValue(player.ConnectionId, out var submitted);

                var chosenAnswer = submitted?.Answer;
                var isCorrect = !string.IsNullOrWhiteSpace(chosenAnswer) &&
                                string.Equals(chosenAnswer.Trim(), currentQuestion.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);

                var responseMs = 0;
                var awardedPoints = 0;

                if (submitted != null)
                    responseMs = (int)Math.Max(0, (submitted.AnsweredAtUtc - answerWindowOpenedAt).TotalMilliseconds);

                if (isCorrect)
                {
                    var clampedResponseMs = Math.Clamp(responseMs, 0, (int)maxResponseMs);
                    var speedRatio = Math.Max(0d, (maxResponseMs - clampedResponseMs) / maxResponseMs);
                    var speedBonus = (int)Math.Round(speedRatio * MaxSpeedBonusPoints);

                    awardedPoints = BaseCorrectPoints + speedBonus;
                    lobby.Scores[player.ConnectionId] = lobby.Scores.GetValueOrDefault(player.ConnectionId) + awardedPoints;
                }

                resolution.Results.Add(new RankedQuestionPlayerAnswerDto
                {
                    Username = player.Username,
                    AvatarKey = player.AvatarKey,
                    PlayerColor = player.PlayerColor,
                    Answer = chosenAnswer,
                    IsCorrect = isCorrect,
                    PointsAwarded = awardedPoints,
                    ResponseMs = responseMs
                });
            }

            liveScores = BuildLiveScores(lobby);

            lobby.CurrentQuestionIndex++;

            if (lobby.CurrentQuestionIndex >= lobby.Questions.Count)
            {
                var results = lobby.Players
                    .Select(p =>
                    {
                        var score = lobby.Scores.TryGetValue(p.ConnectionId, out var s) ? s : 0;
                        return new RankedMatchPlayerResultDto
                        {
                            UserId = p.UserId,
                            Username = p.Username,
                            AvatarKey = p.AvatarKey,
                            Score = score,
                            OldRating = 0,
                            NewRating = 0,
                            Delta = 0,
                            Place = 0,
                            IsWinner = false
                        };
                    })
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Username)
                    .ToList();

                for (int i = 0; i < results.Count; i++)
                {
                    results[i].Place = i + 1;
                    results[i].IsWinner = i == 0;
                }

                finishedDto = new RankedGameFinishedDto
                {
                    LobbyCode = lobby.Code,
                    Results = results
                };
            }
            else
            {
                hasNextQuestion = true;
            }
        }

        await Clients.Group(lobby.GroupName).SendAsync("ScoresUpdated", liveScores);
        await Clients.Group(lobby.GroupName).SendAsync("RankedQuestionResolved", resolution);

        await Task.Delay(TimeSpan.FromSeconds(ResultRevealSeconds));

        if (finishedDto != null)
        {
            await Clients.Group(lobby.GroupName).SendAsync("RankedGameFinished", finishedDto);
            return;
        }

        if (hasNextQuestion)
            await SendQuestion(lobby);
    }

    private static List<LivePlayerScoreDto> BuildLiveScores(RankedLobby lobby)
    {
        return lobby.Players
            .Select(p => new LivePlayerScoreDto(
                p.Username,
                p.AvatarKey,
                lobby.Scores.TryGetValue(p.ConnectionId, out var score) ? score : 0,
                p.PlayerColor))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Username)
            .ToList();
    }
}