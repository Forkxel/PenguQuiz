using Microsoft.AspNetCore.SignalR;
using QuizAPI.Models;
using QuizAPI.Models.Multiplayer;
using QuizAPI.Services;

namespace QuizAPI;

public class MultiplayerHub : Hub
{
    private readonly MultiplayerManager _lobbies;
    private readonly IHubContext<MultiplayerHub> _hubContext;

    private const int QuestionIntroSeconds = 4;
    private const int BaseCorrectPoints = 100;
    private const int MaxSpeedBonusPoints = 50;
    private const int ResultRevealSeconds = 2;

    public MultiplayerHub(MultiplayerManager lobbies, IHubContext<MultiplayerHub> hubContext)
    {
        _lobbies = lobbies;
        _hubContext = hubContext;
    }

    public Task<List<LivePlayerScoreDto>> GetLiveScores(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return Task.FromResult(new List<LivePlayerScoreDto>());

        if (!_lobbies.IsPlayerInLobby(lobbyCode, Context.ConnectionId))
            return Task.FromResult(new List<LivePlayerScoreDto>());

        lock (lobby)
        {
            return Task.FromResult(BuildLiveScores(lobby));
        }
    }

    public async Task<LobbyState> CreateLobby(CreateLobbyRequest req)
    {
        var lobby = _lobbies.CreateLobby(
            Context.ConnectionId,
            req.HostUsername,
            req.AvatarKey,
            req.Settings);

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);
        var state = MultiplayerManager.ToState(lobby);

        await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        return state;
    }

    public async Task<LobbyState> JoinLobby(JoinLobbyRequest req)
    {
        var (ok, error) = _lobbies.JoinLobby(
            req.LobbyCode,
            Context.ConnectionId,
            req.Username,
            req.AvatarKey);
        if (!ok) throw new HubException(error);

        _lobbies.TryGetLobby(req.LobbyCode, out var lobby);
        if (lobby == null) throw new HubException("Lobby not found");

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

        var state = MultiplayerManager.ToState(lobby);
        await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        return state;
    }

    public async Task<LobbyState> GetLobbyState(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            throw new HubException("Lobby not found");

        if (!_lobbies.IsPlayerInLobby(lobbyCode, Context.ConnectionId))
            throw new HubException("You are no longer in this lobby");

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

        return MultiplayerManager.ToState(lobby);
    }
    
    public async Task LeaveLobby(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobby.GroupName);

        var updatedLobby = _lobbies.LeaveByConnection(Context.ConnectionId);

        if (updatedLobby != null)
        {
            var state = MultiplayerManager.ToState(updatedLobby);
            await _hubContext.Clients.Group(updatedLobby.GroupName)
                .SendCoreAsync("LobbyUpdated", new object[] { state });
        }
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var updatedLobby = _lobbies.LeaveByConnection(Context.ConnectionId);

        if (updatedLobby != null)
        {
            var state = MultiplayerManager.ToState(updatedLobby);
            await _hubContext.Clients.Group(updatedLobby.GroupName)
                .SendCoreAsync("LobbyUpdated", new object[] { state });
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<LobbyState> QuickMatch(QuickMatchRequest req)
    {
        try
        {
            var lobby = _lobbies.QuickMatch(
                Context.ConnectionId,
                req.Username,
                req.AvatarKey,
                req.Preferences,
                req.MinPlayers,
                req.MaxPlayers);

            await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

            var state = MultiplayerManager.ToState(lobby);
            await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });

            if (lobby.IsQuickMatch && !lobby.IsStarted && lobby.Players.Count >= lobby.MinPlayers)
            {
                StartMatchmakingCountdownIfNeeded(lobby);

                var updatedState = MultiplayerManager.ToState(lobby);
                await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { updatedState });
            }

            return MultiplayerManager.ToState(lobby);
        }
        catch (Exception ex)
        {
            Console.WriteLine("QUICKMATCH ERROR:\n" + ex);
            throw new HubException(ex.Message);
        }
    }

    private void StartMatchmakingCountdownIfNeeded(Lobby lobby)
    {
        lock (lobby)
        {
            if (lobby.IsStarted) return;
            if (lobby.IsMatchmaking) return;

            lobby.IsMatchmaking = true;
            lobby.MatchmakingEndsAtUtc = DateTime.UtcNow.AddSeconds(30);

            lobby.MatchmakingCts?.Cancel();
            lobby.MatchmakingCts = new CancellationTokenSource();
            var token = lobby.MatchmakingCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                }
                catch
                {
                    return;
                }

                if (!_lobbies.TryGetLobby(lobby.Code, out var l) || l == null)
                    return;

                bool canStart;
                lock (l)
                {
                    canStart = !l.IsStarted && l.Players.Count >= l.MinPlayers;
                    if (canStart)
                    {
                        l.IsStarted = true;
                        l.IsMatchmaking = false;
                        l.MatchmakingEndsAtUtc = null;
                    }
                }

                if (!canStart) return;

                var state = MultiplayerManager.ToState(l);
                await _hubContext.Clients.Group(l.GroupName).SendCoreAsync("LobbyUpdated", new object[] { state });

                await Task.Delay(300);
                await InternalStartGame(l);
            });
        }
    }

    public async Task UpdateSettings(string lobbyCode, LobbySettings settings)
    {
        _lobbies.UpdateSettings(lobbyCode, Context.ConnectionId, settings);

        if (_lobbies.TryGetLobby(lobbyCode, out var lobby) && lobby != null)
        {
            var state = MultiplayerManager.ToState(lobby);
            await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        }
    }

    public async Task StartGame(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return;

        var state = MultiplayerManager.ToState(lobby);
        await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object[] { state });

        _lobbies.StartGame(lobbyCode, Context.ConnectionId);

        await InternalStartGame(lobby);
    }

    public Task<TriviaQuestion?> GetCurrentQuestion(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return Task.FromResult<TriviaQuestion?>(null);

        if (!_lobbies.IsPlayerInLobby(lobbyCode, Context.ConnectionId))
            return Task.FromResult<TriviaQuestion?>(null);

        lock (lobby)
        {
            if (lobby.Questions.Count == 0)
                return Task.FromResult<TriviaQuestion?>(null);

            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count)
                return Task.FromResult<TriviaQuestion?>(null);

            return Task.FromResult<TriviaQuestion?>(lobby.Questions[lobby.CurrentQuestionIndex]);
        }
    }

    public async Task AnswerQuestion(string lobbyCode, string answer)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return;
        
        if (!_lobbies.IsPlayerInLobby(lobbyCode, Context.ConnectionId))
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

            lobby.SubmittedAnswers[Context.ConnectionId] = new SubmittedAnswer
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

    private async Task InternalStartGame(Lobby lobby)
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
                await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("GameError",
                    new object[] { "No questions loaded (empty response)." });
                return;
            }

            lock (lobby)
            {
                lobby.Questions = questions;
                lobby.CurrentQuestionIndex = 0;
                lobby.QuestionLocked = false;
                lobby.SubmittedAnswers.Clear();

                lobby.Scores.Clear();
                foreach (var p in lobby.Players)
                    lobby.Scores[p.ConnectionId] = 0;
            }

            await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("ScoresUpdated",
                new object[] { BuildLiveScores(lobby) });

            await SendQuestion(lobby, lobby.Questions[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[START ERROR]\n" + ex);

            await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("GameError",
                new object[] { "StartGame failed: " + ex.Message });
        }
    }

    private async Task SendQuestion(Lobby lobby, TriviaQuestion q)
    {
        lock (lobby)
        {
            lobby.QuestionLocked = false;
            lobby.QuestionStartedAtUtc = DateTime.UtcNow;
            lobby.SubmittedAnswers.Clear();

            lobby.QuestionCts?.Cancel();
            lobby.QuestionCts = new CancellationTokenSource();
        }

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("NewQuestion", new object[] { q });

        _ = Task.Run(async () =>
        {
            CancellationToken token;
            int seconds;
            lock (lobby)
            {
                token = lobby.QuestionCts!.Token;
                seconds = lobby.Settings.TimePerQuestion;
            }

            if (seconds <= 0) seconds = 10;
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

    private async Task ResolveQuestion(Lobby lobby)
    {
        TriviaQuestion? currentQuestion;
        TriviaQuestion? nextQuestion = null;
        Dictionary<string, int>? finalScores = null;
        QuestionResolutionDto resolution;
        List<LivePlayerScoreDto> liveScores;

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            if (lobby.Questions.Count == 0) return;
            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count) return;

            lobby.QuestionLocked = true;
            lobby.QuestionCts?.Cancel();

            currentQuestion = lobby.Questions[lobby.CurrentQuestionIndex];

            var answerWindowOpenedAt = lobby.QuestionStartedAtUtc.AddSeconds(QuestionIntroSeconds);
            var maxResponseMs = Math.Max(1, lobby.Settings.TimePerQuestion) * 1000.0;

            resolution = new QuestionResolutionDto
            {
                CorrectAnswer = currentQuestion.CorrectAnswer,
                Results = new List<QuestionPlayerAnswerDto>()
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

                resolution.Results.Add(new QuestionPlayerAnswerDto
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

            if (lobby.CurrentQuestionIndex < lobby.Questions.Count)
                nextQuestion = lobby.Questions[lobby.CurrentQuestionIndex];
            else
                finalScores = new Dictionary<string, int>(lobby.Scores);
        }

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("ScoresUpdated",
            new object[] { liveScores });

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("QuestionResolved",
            new object[] { resolution });

        await Task.Delay(TimeSpan.FromSeconds(ResultRevealSeconds));

        if (nextQuestion != null)
            await SendQuestion(lobby, nextQuestion);
        else
            await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("GameFinished", new object[] { finalScores! });
    }

    private static List<LivePlayerScoreDto> BuildLiveScores(Lobby lobby)
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
    
    public Task<DateTime?> GetQuestionStartedAtUtc(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return Task.FromResult<DateTime?>(null);

        if (!_lobbies.IsPlayerInLobby(lobbyCode, Context.ConnectionId))
            return Task.FromResult<DateTime?>(null);

        lock (lobby)
        {
            if (lobby.Questions.Count == 0)
                return Task.FromResult<DateTime?>(null);

            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count)
                return Task.FromResult<DateTime?>(null);

            return Task.FromResult<DateTime?>(lobby.QuestionStartedAtUtc);
        }
    }
}