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

    private const int QuestionIntroSeconds = 4;
    private const int BaseCorrectPoints = 100;
    private const int MaxSpeedBonusPoints = 50;
    private const int ResultRevealSeconds = 2;
    private const int MatchmakingCountdownSeconds = 10;

    private readonly RankedMultiplayerManager _manager;
    private readonly DatabaseServices _db;
    private readonly IHubContext<RankedMultiplayerHub> _hubContext;
    private const int RankedK = 32;
    private const int DisconnectPenalty = 10;

    public RankedMultiplayerHub(
        RankedMultiplayerManager manager,
        DatabaseServices db,
        IHubContext<RankedMultiplayerHub> hubContext)
    {
        _manager = manager;
        _db = db;
        _hubContext = hubContext;
    }
    
    public async Task LeaveRankedLobby(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null)
            return;

        RankedGameFinishedDto? forfeitResult = null;

        lock (lobby)
        {
            var leavingPlayer = lobby.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (leavingPlayer == null)
                return;

            var shouldForfeit = !lobby.ResultProcessed && lobby.Players.Count >= 2;

            if (shouldForfeit)
                forfeitResult = BuildAndApplyRankedResult(lobby, Context.ConnectionId);
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobby.GroupName);
        _manager.RemovePlayerByConnection(Context.ConnectionId);

        if (forfeitResult != null && forfeitResult.Results.Any())
        {
            await _hubContext.Clients.Group(lobby.GroupName)
                .SendAsync("RankedGameFinished", forfeitResult);
        }
        else
        {
            var state = _manager.GetState(code);
            if (state != null)
            {
                await _hubContext.Clients.Group(lobby.GroupName)
                    .SendAsync("RankedLobbyUpdated", state);
            }
        }
    }
    
    public Task<DateTime?> GetQuestionStartedAtUtc(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null)
            return Task.FromResult<DateTime?>(null);

        if (!_manager.IsPlayerInLobby(code, Context.ConnectionId))
            return Task.FromResult<DateTime?>(null);

        lock (lobby)
        {
            if (lobby.Questions.Count == 0)
                return Task.FromResult<DateTime?>(null);

            return Task.FromResult<DateTime?>(lobby.QuestionStartedAtUtc);
        }
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var lobby = _manager.FindLobbyByConnection(Context.ConnectionId);
        if (lobby != null)
        {
            RankedGameFinishedDto? forfeitResult = null;

            lock (lobby)
            {
                var shouldForfeit = !lobby.ResultProcessed && lobby.Players.Count >= 2;
                if (shouldForfeit)
                    forfeitResult = BuildAndApplyRankedResult(lobby, Context.ConnectionId);
            }

            _manager.RemovePlayerByConnection(Context.ConnectionId);

            if (forfeitResult != null && forfeitResult.Results.Any())
            {
                await _hubContext.Clients.Group(lobby.GroupName)
                    .SendAsync("RankedGameFinished", forfeitResult);
            }
            else
            {
                var state = _manager.GetState(lobby.Code);
                if (state != null)
                {
                    await _hubContext.Clients.Group(lobby.GroupName)
                        .SendAsync("RankedLobbyUpdated", state);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
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

        await _hubContext.Clients.Group(lobby.GroupName)
            .SendAsync("RankedLobbyUpdated", _manager.GetState(lobby.Code)!);

        if (!lobby.IsStarted && lobby.Players.Count >= 2)
        {
            StartMatchmakingCountdownIfNeeded(lobby);

            var updatedState = _manager.GetState(lobby.Code);
            if (updatedState != null)
            {
                await _hubContext.Clients.Group(lobby.GroupName)
                    .SendAsync("RankedLobbyUpdated", updatedState);
            }
        }

        return _manager.GetState(lobby.Code)!;
    }

    public async Task<RankedLobbyState> GetRankedLobbyState(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null)
            throw new HubException("Lobby not found");

        if (!_manager.IsPlayerInLobby(code, Context.ConnectionId))
            throw new HubException("You are no longer in this ranked lobby");

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

        if (!_manager.IsPlayerInLobby(code, Context.ConnectionId))
            return Task.FromResult(new List<LivePlayerScoreDto>());

        lock (lobby)
        {
            return Task.FromResult(BuildLiveScores(lobby));
        }
    }

    public Task<TriviaQuestion?> GetCurrentRankedQuestion(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return Task.FromResult<TriviaQuestion?>(null);

        if (!_manager.IsPlayerInLobby(code, Context.ConnectionId))
            return Task.FromResult<TriviaQuestion?>(null);

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
        
        if (!_manager.IsPlayerInLobby(code, Context.ConnectionId))
            return;

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
            if (lobby.Players.Count < 2) return;

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

                if (!shouldStart)
                    return;

                var state = _manager.GetState(lobby.Code);
                if (state != null)
                {
                    await _hubContext.Clients.Group(lobby.GroupName)
                        .SendAsync("RankedLobbyUpdated", state);
                }

                await Task.Delay(300);
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
                await _hubContext.Clients.Group(lobby.GroupName)
                    .SendAsync("RankedGameError", "No questions loaded.");
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

            await _hubContext.Clients.Group(lobby.GroupName).SendAsync("ScoresUpdated", BuildLiveScores(lobby));
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

        await _hubContext.Clients.Group(lobby.GroupName)
            .SendAsync("RankedNewQuestion", question);

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
                finishedDto = BuildAndApplyRankedResult(lobby);
            }
            else
            {
                hasNextQuestion = true;
            }
        }

        await _hubContext.Clients.Group(lobby.GroupName)
            .SendAsync("ScoresUpdated", liveScores);

        await _hubContext.Clients.Group(lobby.GroupName)
            .SendAsync("RankedQuestionResolved", resolution);

        await Task.Delay(TimeSpan.FromSeconds(ResultRevealSeconds));

        if (finishedDto != null)
        {
            await _hubContext.Clients.Group(lobby.GroupName)
                .SendAsync("RankedGameFinished", finishedDto);
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
    
    private RankedGameFinishedDto BuildAndApplyRankedResult(RankedLobby lobby, string? forcedLoserConnectionId = null)
    {
        lock (lobby)
        {
            if (lobby.ResultProcessed)
            {
                return new RankedGameFinishedDto
                {
                    LobbyCode = lobby.Code,
                    Results = new List<RankedMatchPlayerResultDto>()
                };
            }

            lobby.ResultProcessed = true;
            lobby.QuestionCts?.Cancel();
            lobby.MatchmakingCts?.Cancel();
            lobby.IsMatchmaking = false;
            lobby.MatchmakingEndsAtUtc = null;

            var players = lobby.Players
                .Select(p => new
                {
                    Player = p,
                    Score = lobby.Scores.TryGetValue(p.ConnectionId, out var s) ? s : 0
                })
                .ToList();

            if (players.Count != 2)
            {
                return new RankedGameFinishedDto
                {
                    LobbyCode = lobby.Code,
                    Results = players.Select((x, i) => new RankedMatchPlayerResultDto
                    {
                        UserId = x.Player.UserId,
                        Username = x.Player.Username,
                        AvatarKey = x.Player.AvatarKey,
                        Score = x.Score,
                        OldRating = 0,
                        NewRating = 0,
                        Delta = 0,
                        Place = i + 1,
                        IsWinner = i == 0
                    }).ToList()
                };
            }

            var p1 = players[0];
            var p2 = players[1];

            var old1 = _db.GetMultiRating(p1.Player.UserId);
            var old2 = _db.GetMultiRating(p2.Player.UserId);

            double actual1;
            double actual2;

            if (!string.IsNullOrWhiteSpace(forcedLoserConnectionId))
            {
                actual1 = p1.Player.ConnectionId == forcedLoserConnectionId ? 0d : 1d;
                actual2 = p2.Player.ConnectionId == forcedLoserConnectionId ? 0d : 1d;
            }
            else if (p1.Score == p2.Score)
            {
                actual1 = 0.5d;
                actual2 = 0.5d;
            }
            else
            {
                actual1 = p1.Score > p2.Score ? 1d : 0d;
                actual2 = p2.Score > p1.Score ? 1d : 0d;
            }

            var expected1 = 1d / (1d + Math.Pow(10d, (old2 - old1) / 400d));
            var expected2 = 1d / (1d + Math.Pow(10d, (old1 - old2) / 400d));

            var delta1 = (int)Math.Round(RankedK * (actual1 - expected1));
            var delta2 = (int)Math.Round(RankedK * (actual2 - expected2));

            if (!string.IsNullOrWhiteSpace(forcedLoserConnectionId))
            {
                if (p1.Player.ConnectionId == forcedLoserConnectionId)
                    delta1 -= DisconnectPenalty;

                if (p2.Player.ConnectionId == forcedLoserConnectionId)
                    delta2 -= DisconnectPenalty;
            }

            var new1 = Math.Max(0, old1 + delta1);
            var new2 = Math.Max(0, old2 + delta2);

            _db.UpdateMultiRating(p1.Player.UserId, new1, delta1 > 0);
            _db.UpdateMultiRating(p2.Player.UserId, new2, delta2 > 0);

            var result1 = new RankedMatchPlayerResultDto
            {
                UserId = p1.Player.UserId,
                Username = p1.Player.Username,
                AvatarKey = p1.Player.AvatarKey,
                Score = p1.Score,
                OldRating = old1,
                NewRating = new1,
                Delta = delta1
            };

            var result2 = new RankedMatchPlayerResultDto
            {
                UserId = p2.Player.UserId,
                Username = p2.Player.Username,
                AvatarKey = p2.Player.AvatarKey,
                Score = p2.Score,
                OldRating = old2,
                NewRating = new2,
                Delta = delta2
            };

            List<RankedMatchPlayerResultDto> ordered;

            if (!string.IsNullOrWhiteSpace(forcedLoserConnectionId))
            {
                var winnerUserId = p1.Player.ConnectionId == forcedLoserConnectionId
                    ? p2.Player.UserId
                    : p1.Player.UserId;

                ordered = new List<RankedMatchPlayerResultDto> { result1, result2 }
                    .OrderByDescending(x => x.UserId == winnerUserId)
                    .ThenBy(x => x.Username)
                    .ToList();
            }
            else
            {
                ordered = new List<RankedMatchPlayerResultDto> { result1, result2 }
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Username)
                    .ToList();
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].Place = i + 1;
                ordered[i].IsWinner = i == 0;
            }

            var matchId = _db.CreateRankedMatch("multi");

            foreach (var r in ordered)
            {
                _db.CreateRankedMatchResult(
                    matchId,
                    r.UserId,
                    r.Score,
                    r.Place,
                    r.OldRating,
                    r.NewRating);
            }

            return new RankedGameFinishedDto
            {
                LobbyCode = lobby.Code,
                Results = ordered
            };
        }
    }
}