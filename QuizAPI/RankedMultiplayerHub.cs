using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizAPI.Models;
using QuizAPI.Models.Multiplayer.MultiplayerRanked;
using QuizAPI.Services;

namespace QuizAPI;

[Authorize]
public class RankedMultiplayerHub : Hub
{
    private readonly RankedMultiplayerManager _lobbies;
    private readonly IHubContext<RankedMultiplayerHub> _hubContext;
    private readonly DatabaseServices _db;

    public RankedMultiplayerHub(
        RankedMultiplayerManager lobbies,
        IHubContext<RankedMultiplayerHub> hubContext,
        DatabaseServices db)
    {
        _lobbies = lobbies;
        _hubContext = hubContext;
        _db = db;
    }
    
    public Task<List<LivePlayerScoreDto>> GetLiveScores(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return Task.FromResult(new List<LivePlayerScoreDto>());

        lock (lobby)
        {
            return Task.FromResult(BuildLiveScores(lobby));
        }
    }

    public async Task<RankedLobbyState> QuickMatchRanked()
    {
        var userId = GetUserId();

        var dbUser = _db.GetUserById(userId);
        if (dbUser == null)
            throw new HubException("User not found");

        _db.EnsureRankingExists(userId);

        var lobby = _lobbies.QuickMatch(
            userId,
            Context.ConnectionId,
            dbUser.Username,
            dbUser.AvatarKey);

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

        var state = RankedMultiplayerManager.ToState(lobby);
        await Clients.Group(lobby.GroupName).SendCoreAsync("RankedLobbyUpdated", new object[] { state });

        if (!lobby.IsStarted && lobby.Players.Count == 2)
        {
            StartMatchmakingCountdownIfNeeded(lobby);

            state = RankedMultiplayerManager.ToState(lobby);
            await Clients.Group(lobby.GroupName).SendCoreAsync("RankedLobbyUpdated", new object[] { state });
        }

        return RankedMultiplayerManager.ToState(lobby);
    }
    
    private static List<LivePlayerScoreDto> BuildLiveScores(RankedLobby lobby)
    {
        return lobby.Players
            .Select(p => new LivePlayerScoreDto(
                p.Username,
                p.AvatarKey,
                lobby.Scores.TryGetValue(p.ConnectionId, out var score) ? score : 0))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Username)
            .ToList();
    }

    public async Task<RankedLobbyState> GetRankedLobbyState(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            throw new HubException("Lobby not found");

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);
        return RankedMultiplayerManager.ToState(lobby);
    }

    public Task<TriviaQuestion?> GetCurrentRankedQuestion(string lobbyCode)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return Task.FromResult<TriviaQuestion?>(null);

        lock (lobby)
        {
            if (lobby.Questions.Count == 0) return Task.FromResult<TriviaQuestion?>(null);
            if (lobby.CurrentQuestionIndex < 0 || lobby.CurrentQuestionIndex >= lobby.Questions.Count)
                return Task.FromResult<TriviaQuestion?>(null);

            return Task.FromResult<TriviaQuestion?>(lobby.Questions[lobby.CurrentQuestionIndex]);
        }
    }

    public async Task AnswerRankedQuestion(string lobbyCode, string answer)
    {
        if (!_lobbies.TryGetLobby(lobbyCode, out var lobby) || lobby == null)
            return;

        string who;
        string chosen;
        bool correct;
        TriviaQuestion? next = null;
        RankedGameFinishedDto? final = null;

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            if (lobby.Questions.Count == 0) return;

            var q = lobby.Questions[lobby.CurrentQuestionIndex];

            lobby.QuestionLocked = true;
            lobby.QuestionCts?.Cancel();

            var player = lobby.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            who = player.Username;
            chosen = answer?.Trim() ?? "";

            correct = string.Equals(chosen, q.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase);

            if (correct)
            {
                if (!lobby.Scores.ContainsKey(Context.ConnectionId))
                    lobby.Scores[Context.ConnectionId] = 0;

                lobby.Scores[Context.ConnectionId] += 1;
            }
        }

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
            "ScoresUpdated",
            new object[] { BuildLiveScores(lobby) });

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
            "RankedQuestionResolved",
            new object[] { who, chosen, correct });

        await Task.Delay(1200);

        lock (lobby)
        {
            lobby.CurrentQuestionIndex++;

            if (lobby.CurrentQuestionIndex < lobby.Questions.Count)
            {
                next = lobby.Questions[lobby.CurrentQuestionIndex];
            }
        }

        if (next != null)
        {
            await SendQuestion(lobby, next);
            return;
        }

        final = FinishRankedGame(lobby);
        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
            "RankedGameFinished",
            new object[] { final });
    }

    private void StartMatchmakingCountdownIfNeeded(RankedLobby lobby)
    {
        lock (lobby)
        {
            if (lobby.IsStarted) return;
            if (lobby.IsMatchmaking) return;
            if (lobby.Players.Count < 2) return;

            lobby.IsMatchmaking = true;
            lobby.MatchmakingEndsAtUtc = DateTime.UtcNow.AddSeconds(5);

            lobby.MatchmakingCts?.Cancel();
            lobby.MatchmakingCts = new CancellationTokenSource();
            var token = lobby.MatchmakingCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
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
                    canStart = !l.IsStarted && l.Players.Count == 2;
                    if (canStart)
                    {
                        l.IsStarted = true;
                        l.IsMatchmaking = false;
                        l.MatchmakingEndsAtUtc = null;
                    }
                }

                if (!canStart) return;

                var state = RankedMultiplayerManager.ToState(l);
                await _hubContext.Clients.Group(l.GroupName).SendCoreAsync(
                    "RankedLobbyUpdated",
                    new object[] { state });

                await Task.Delay(300);
                await InternalStartGame(l);
            });
        }
    }

    private async Task InternalStartGame(RankedLobby lobby)
    {
        try
        {
            using var http = new HttpClient();

            var url =
                $"http://localhost:5237/api/trivia?amount={lobby.Settings.Amount}" +
                $"&difficulty={lobby.Settings.Difficulty}&fresh=true";

            var resp = await http.GetFromJsonAsync<TriviaResponse>(url);
            var questions = resp?.Results ?? new List<TriviaQuestion>();

            if (questions.Count == 0)
            {
                await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
                    "RankedGameError",
                    new object[] { "No questions loaded." });
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

            await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
                "ScoresUpdated",
                new object[] { BuildLiveScores(lobby) });

            await SendQuestion(lobby, lobby.Questions[0]);
        }
        catch (Exception ex)
        {
            await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
                "RankedGameError",
                new object[] { "StartGame failed: " + ex.Message });
        }
    }

    private async Task SendQuestion(RankedLobby lobby, TriviaQuestion q)
    {
        lock (lobby)
        {
            lobby.QuestionLocked = false;
            lobby.QuestionStartedAtUtc = DateTime.UtcNow;

            lobby.QuestionCts?.Cancel();
            lobby.QuestionCts = new CancellationTokenSource();
        }

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
            "RankedNewQuestion",
            new object[] { q });

        _ = Task.Run(async () =>
        {
            CancellationToken token;
            int seconds = lobby.Settings.TimePerQuestion;
            if (seconds <= 0) seconds = 15;

            lock (lobby) token = lobby.QuestionCts!.Token;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            }
            catch
            {
                return;
            }

            await HandleQuestionTimeout(lobby);
        });
    }

    private async Task HandleQuestionTimeout(RankedLobby lobby)
    {
        TriviaQuestion? next = null;
        RankedGameFinishedDto? final = null;

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            lobby.QuestionLocked = true;

            lobby.CurrentQuestionIndex++;

            if (lobby.CurrentQuestionIndex < lobby.Questions.Count)
                next = lobby.Questions[lobby.CurrentQuestionIndex];
        }
        
        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
            "ScoresUpdated",
            new object[] { BuildLiveScores(lobby) });

        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
            "RankedQuestionResolved",
            new object[] { "TIMEOUT", "", false });

        await Task.Delay(1200);

        if (next != null)
        {
            await SendQuestion(lobby, next);
            return;
        }

        final = FinishRankedGame(lobby);
        await _hubContext.Clients.Group(lobby.GroupName).SendCoreAsync(
            "RankedGameFinished",
            new object[] { final });
    }

    private RankedGameFinishedDto FinishRankedGame(RankedLobby lobby)
    {
        var players = lobby.Players.ToList();
        var oldRatings = new Dictionary<int, int>();
        var scoreByUserId = new Dictionary<int, int>();

        foreach (var p in players)
        {
            _db.EnsureRankingExists(p.UserId);
            oldRatings[p.UserId] = _db.GetMultiRating(p.UserId);
            scoreByUserId[p.UserId] = lobby.Scores.TryGetValue(p.ConnectionId, out var s) ? s : 0;
        }

        var ordered = players
            .Select(p => new
            {
                Player = p,
                Score = scoreByUserId[p.UserId]
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Player.Username)
            .ToList();

        var placeByUserId = new Dictionary<int, int>();
        int place = 1;
        for (int i = 0; i < ordered.Count; i++)
        {
            if (i > 0 && ordered[i].Score < ordered[i - 1].Score)
                place = i + 1;

            placeByUserId[ordered[i].Player.UserId] = place;
        }

        var deltaByUserId = CalculatePairwiseElo(players, oldRatings, scoreByUserId);

        var topScore = ordered.Max(x => x.Score);
        var results = new List<RankedMatchPlayerResultDto>();

        foreach (var p in players)
        {
            var oldRating = oldRatings[p.UserId];
            var delta = deltaByUserId[p.UserId];
            var newRating = Math.Max(0, oldRating + delta);
            var isWinner = scoreByUserId[p.UserId] == topScore;

            _db.UpdateMultiRating(p.UserId, newRating, isWinner);

            results.Add(new RankedMatchPlayerResultDto
            {
                UserId = p.UserId,
                Username = p.Username,
                AvatarKey = p.AvatarKey,
                Score = scoreByUserId[p.UserId],
                OldRating = oldRating,
                NewRating = newRating,
                Delta = delta,
                Place = placeByUserId[p.UserId],
                IsWinner = isWinner
            });
        }

        return new RankedGameFinishedDto
        {
            LobbyCode = lobby.Code,
            Results = results
                .OrderBy(r => r.Place)
                .ThenByDescending(r => r.Score)
                .ThenBy(r => r.Username)
                .ToList()
        };
    }

    private static Dictionary<int, int> CalculatePairwiseElo(
        List<RankedPlayerInfo> players,
        Dictionary<int, int> ratings,
        Dictionary<int, int> scores)
    {
        const double k = 24.0;
        var deltas = players.ToDictionary(p => p.UserId, _ => 0.0);

        for (int i = 0; i < players.Count; i++)
        {
            for (int j = i + 1; j < players.Count; j++)
            {
                var a = players[i];
                var b = players[j];

                double actualA;
                if (scores[a.UserId] > scores[b.UserId]) actualA = 1.0;
                else if (scores[a.UserId] < scores[b.UserId]) actualA = 0.0;
                else actualA = 0.5;

                double actualB = 1.0 - actualA;

                double expectedA = 1.0 / (1.0 + Math.Pow(10, (ratings[b.UserId] - ratings[a.UserId]) / 400.0));
                double expectedB = 1.0 / (1.0 + Math.Pow(10, (ratings[a.UserId] - ratings[b.UserId]) / 400.0));

                deltas[a.UserId] += k * (actualA - expectedA);
                deltas[b.UserId] += k * (actualB - expectedB);
            }
        }

        return deltas.ToDictionary(x => x.Key, x => (int)Math.Round(x.Value));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _lobbies.LeaveByConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private int GetUserId()
    {
        var userIdClaim =
            Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            Context.User?.FindFirst("nameid")?.Value ??
            Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
            throw new HubException("Unauthorized");

        return int.Parse(userIdClaim);
    }
}