using Microsoft.AspNetCore.SignalR;
using QuizAPI.Models;
using QuizAPI.Models.Multiplayer.MultiplayerRanked;
using QuizAPI.Services;

namespace QuizAPI;

public class RankedMultiplayerHub : Hub
{
    private readonly RankedMultiplayerManager _manager;

    private const int QuestionIntroSeconds = 4;

    public RankedMultiplayerHub(RankedMultiplayerManager manager)
    {
        _manager = manager;
    }

    public async Task<List<LivePlayerScoreDto>> GetLiveScores(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return new();

        return BuildLiveScores(lobby);
    }

    public async Task<TriviaQuestion?> GetCurrentRankedQuestion(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return null;
        if (lobby.Questions.Count == 0) return null;
        if (lobby.CurrentQuestionIndex >= lobby.Questions.Count) return null;

        return lobby.Questions[lobby.CurrentQuestionIndex];
    }

    public async Task AnswerRankedQuestion(string code, string answer)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return;

        bool correct = false;
        string username = "";

        lock (lobby)
        {
            if (lobby.QuestionLocked) return;
            if (lobby.Questions.Count == 0) return;
            if (DateTime.UtcNow < lobby.QuestionStartedAtUtc.AddSeconds(QuestionIntroSeconds))
                return;

            var current = lobby.Questions[lobby.CurrentQuestionIndex];
            username = lobby.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId)?.Username ?? "Unknown";

            correct = string.Equals(answer, current.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
            lobby.QuestionLocked = true;

            if (correct)
            {
                if (!lobby.Scores.ContainsKey(Context.ConnectionId))
                    lobby.Scores[Context.ConnectionId] = 0;

                lobby.Scores[Context.ConnectionId]++;
            }
        }

        await Clients.Group(lobby.GroupName).SendAsync("QuestionResolved", username, answer, correct);
        await Clients.Group(lobby.GroupName).SendAsync("ScoresUpdated", BuildLiveScores(lobby));

        await Task.Delay(1500);
        await MoveNext(code);
    }

    public async Task MoveNext(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return;

        lock (lobby)
        {
            lobby.CurrentQuestionIndex++;
            lobby.QuestionLocked = false;
        }

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

            var dto = new RankedGameFinishedDto
            {
                LobbyCode = code,
                Results = results
            };

            await Clients.Group(lobby.GroupName).SendAsync("GameFinished", dto);
            return;
        }

        await SendQuestion(code);
    }

    public async Task SendQuestion(string code)
    {
        var lobby = _manager.GetLobby(code);
        if (lobby == null) return;
        if (lobby.CurrentQuestionIndex >= lobby.Questions.Count) return;

        lock (lobby)
        {
            lobby.QuestionStartedAtUtc = DateTime.UtcNow;
            lobby.QuestionLocked = false;
        }

        var question = lobby.Questions[lobby.CurrentQuestionIndex];
        await Clients.Group(lobby.GroupName).SendAsync("NewQuestion", question);

        int seconds = lobby.Settings.TimePerQuestion;
        if (seconds <= 0) seconds = 15;
        seconds += QuestionIntroSeconds;

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));

            var currentLobby = _manager.GetLobby(code);
            if (currentLobby == null) return;

            bool shouldResolve = false;

            lock (currentLobby)
            {
                if (!currentLobby.QuestionLocked && currentLobby.CurrentQuestionIndex < currentLobby.Questions.Count)
                {
                    currentLobby.QuestionLocked = true;
                    shouldResolve = true;
                }
            }

            if (shouldResolve)
            {
                await Clients.Group(currentLobby.GroupName).SendAsync("QuestionResolved", "TIMEOUT", "", false);
                await Task.Delay(1500);
                await MoveNext(code);
            }
        });
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