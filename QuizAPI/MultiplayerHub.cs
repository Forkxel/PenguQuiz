using Microsoft.AspNetCore.SignalR;
using QuizAPI.Models;
using QuizAPI.Services;
using QuizAPI.Models.Multiplayer;

namespace QuizAPI;

public class MultiplayerHub : Hub
{
    private readonly MultiplayerManager _lobbies;
    private readonly IHubContext<MultiplayerHub> _hubContext;

    public MultiplayerHub(MultiplayerManager lobbies, IHubContext<MultiplayerHub> hubContext)
    {
        _lobbies = lobbies;
        _hubContext = hubContext;
    }

    public async Task<LobbyState> CreateLobby(CreateLobbyRequest req)
    {
        var lobby = _lobbies.CreateLobby(Context.ConnectionId, req.HostUsername, req.Settings);

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);
        var state = MultiplayerManager.ToState(lobby);

        await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        return state;
    }

    public async Task<LobbyState> JoinLobby(JoinLobbyRequest req)
    {
        var (ok, error) = _lobbies.JoinLobby(req.LobbyCode, Context.ConnectionId, req.Username);
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

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

        return MultiplayerManager.ToState(lobby);
    }

    public async Task<LobbyState> QuickMatch(QuickMatchRequest req)
    {
        try
        {
            var lobby = _lobbies.QuickMatch(Context.ConnectionId, req.Username, req.Preferences, req.MinPlayers, req.MaxPlayers);

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
    
    public async Task AnswerQuestion(string lobbyCode, string answer)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return;

        string who;
        string chosen;
        bool correct;
        TriviaQuestion? next = null;
        Dictionary<string,int>? finalScores = null;

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            if (lobby.Questions.Count == 0) return;

            var q = lobby.Questions[lobby.CurrentQuestionIndex];

            lobby.QuestionLocked = true;
            lobby.QuestionCts?.Cancel();

            who = lobby.Players.First(p => p.ConnectionId == Context.ConnectionId).Username;
            chosen = answer?.Trim() ?? "";

            correct = string.Equals(chosen, q.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase);

            lobby.FirstAnswerConnectionId = Context.ConnectionId;
            lobby.FirstAnswerText = chosen;
            lobby.FirstAnswerCorrect = correct;
            
            if (correct)
            {
                if (!lobby.Scores.ContainsKey(Context.ConnectionId))
                    lobby.Scores[Context.ConnectionId] = 0;
                lobby.Scores[Context.ConnectionId] += 1;
            }
        }
        
        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("QuestionResolved",
            new object[] { who, chosen, correct });

        await Task.Delay(1200);

        lock (lobby)
        {
            lobby.CurrentQuestionIndex++;

            if (lobby.CurrentQuestionIndex < lobby.Questions.Count)
                next = lobby.Questions[lobby.CurrentQuestionIndex];
            else
                finalScores = new Dictionary<string,int>(lobby.Scores);
        }

        if (next != null)
            await SendQuestion(lobby, next);
        else
            await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("GameFinished", new object[] { finalScores! });
    }
    
    private async Task InternalStartGame(Lobby lobby)
    {
        try
        {
            Console.WriteLine($"[START] lobby={lobby.Code} players={lobby.Players.Count}");

            using var http = new HttpClient();

            var cats = lobby.Settings.CategoryIds.Count == 0
                ? ""
                : string.Join(",", lobby.Settings.CategoryIds);

            var url =
                $"http://localhost:5237/api/trivia?amount={lobby.Settings.Amount}" +
                $"&difficulty={lobby.Settings.Difficulty}" +
                $"&categories={cats}&fresh=true";

            Console.WriteLine("[START] fetching: " + url);

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

                lobby.Scores.Clear();
                foreach (var p in lobby.Players)
                    lobby.Scores[p.ConnectionId] = 0;
            }

            Console.WriteLine($"[START] questions={questions.Count} -> sending NewQuestion");

            await SendQuestion(lobby, lobby.Questions[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[START ERROR]\n" + ex);

            await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("GameError",
                new object[] { "StartGame failed: " + ex.Message });
        }
    }
    
    public Task<TriviaQuestion?> GetCurrentQuestion(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
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
    
    private async Task SendQuestion(Lobby lobby, TriviaQuestion q)
    {
        lock (lobby)
        {
            lobby.QuestionLocked = false;
            lobby.FirstAnswerConnectionId = null;
            lobby.FirstAnswerText = null;
            lobby.FirstAnswerCorrect = null;
            lobby.QuestionStartedAtUtc = DateTime.UtcNow;

            lobby.QuestionCts?.Cancel();
            lobby.QuestionCts = new CancellationTokenSource();
        }

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync("NewQuestion", new object[] { q });
        
        _ = Task.Run(async () =>
        {
            CancellationToken token;
            int seconds = lobby.Settings.TimePerQuestion;
            if (seconds <= 0) seconds = 10;

            lock (lobby) token = lobby.QuestionCts!.Token;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            }
            catch { return; }
            
            await HandleQuestionTimeout(lobby);
        });
    }
    
    private async Task HandleQuestionTimeout(Lobby lobby)
    {
        TriviaQuestion? next = null;
        Dictionary<string,int>? finalScores = null;

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            lobby.QuestionLocked = true;

            lobby.CurrentQuestionIndex++;

            if (lobby.CurrentQuestionIndex < lobby.Questions.Count)
                next = lobby.Questions[lobby.CurrentQuestionIndex];
            else
                finalScores = new Dictionary<string,int>(lobby.Scores);
        }

        await Clients.Group(lobby.GroupName).SendCoreAsync("QuestionResolved",
            new object[] { "TIMEOUT", "", false });

        await Task.Delay(1200);

        if (next != null)
            await SendQuestion(lobby, next);
        else
            await Clients.Group(lobby.GroupName).SendCoreAsync("GameFinished", new object[] { finalScores! });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _lobbies.LeaveByConnection(Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }
}