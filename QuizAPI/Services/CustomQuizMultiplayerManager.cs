using System.Collections.Concurrent;
using QuizAPI.Models;

namespace QuizAPI.Services;

public class CustomQuizMultiplayerManager
{
    private readonly ConcurrentDictionary<string, CustomQuizLobby> _lobbies = new();
    private readonly object _lock = new();

    private static readonly string[] PlayerColors =
    {
        "#ff6b6b",
        "#4dabf7",
        "#51cf66",
        "#ffd43b"
    };

    public CustomQuizLobby CreateLobby(
        string hostConnectionId,
        string hostUsername,
        string hostAvatarKey,
        int quizId,
        string quizTitle,
        int timePerQuestion,
        List<TriviaQuestion> questions)
    {
        var code = GenerateCode();

        var lobby = new CustomQuizLobby
        {
            Code = code,
            QuizId = quizId,
            QuizTitle = quizTitle,
            HostConnectionId = hostConnectionId,
            HostUsername = hostUsername,
            TimePerQuestion = timePerQuestion,
            IsStarted = false,
            MinPlayers = 1, 
            MaxPlayers = 4,
            Questions = questions
                .OrderBy(_ => Guid.NewGuid()) 
                .ToList()
        };

        lobby.Players.Add(new CustomQuizLobbyPlayerInfo(
            hostConnectionId,
            hostUsername,
            hostAvatarKey,
            GetNextAvailableColor(lobby)));

        _lobbies[code] = lobby;
        return lobby;
    }

    public bool TryGetLobby(string code, out CustomQuizLobby? lobby)
        => _lobbies.TryGetValue(code, out lobby);

    public bool IsPlayerInLobby(string code, string connectionId)
    {
        if (!_lobbies.TryGetValue(code, out var lobby))
            return false;

        lock (_lock)
        {
            return lobby.Players.Any(p => p.ConnectionId == connectionId);
        }
    }

    public (bool ok, string error) JoinLobby(string code, string connectionId, string username, string avatarKey)
    {
        if (!_lobbies.TryGetValue(code, out var lobby))
            return (false, "Lobby not found");

        lock (_lock)
        {
            if (lobby.IsStarted) return (false, "Game already started");
            if (lobby.Players.Count >= lobby.MaxPlayers) return (false, "Lobby is full");
            if (lobby.Players.Any(p => p.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return (false, "Username already in lobby");

            lobby.Players.Add(new CustomQuizLobbyPlayerInfo(
                connectionId,
                username,
                avatarKey,
                GetNextAvailableColor(lobby)));

            return (true, "");
        }
    }

    public void StartGame(string code, string hostConnId)
    {
        if (!_lobbies.TryGetValue(code, out var lobby))
            return;

        lock (_lock)
        {
            if (lobby.HostConnectionId != hostConnId) return;
            if (lobby.Players.Count < lobby.MinPlayers) return;
            lobby.IsStarted = true;
        }
    }

    public CustomQuizLobby? LeaveByConnection(string connectionId)
    {
        foreach (var kv in _lobbies)
        {
            var lobby = kv.Value;

            lock (_lock)
            {
                var idx = lobby.Players.FindIndex(p => p.ConnectionId == connectionId);
                if (idx < 0) continue;

                lobby.Players.RemoveAt(idx);
                lobby.SubmittedAnswers.Remove(connectionId);
                lobby.Scores.Remove(connectionId);

                if (lobby.HostConnectionId == connectionId)
                {
                    if (lobby.Players.Count > 0)
                    {
                        lobby.HostConnectionId = lobby.Players[0].ConnectionId;
                        lobby.HostUsername = lobby.Players[0].Username;
                    }
                    else
                    {
                        lobby.QuestionCts?.Cancel();
                        _lobbies.TryRemove(lobby.Code, out _);
                        return null;
                    }
                }

                return lobby;
            }
        }

        return null;
    }

    public static CustomQuizLobbyState ToState(CustomQuizLobby lobby) =>
        new(
            lobby.Code,
            lobby.QuizId,
            lobby.QuizTitle,
            lobby.HostUsername,
            lobby.TimePerQuestion,
            lobby.Questions.Count,
            lobby.Players.Select(p => p.Username).ToList(),
            lobby.Players.Select(p => new CustomQuizLobbyPlayerView(p.Username, p.AvatarKey, p.PlayerColor)).ToList(),
            lobby.IsStarted
        );

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
    }

    private static string GetNextAvailableColor(CustomQuizLobby lobby)
    {
        var used = lobby.Players
            .Select(p => p.PlayerColor)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var color in PlayerColors)
        {
            if (!used.Contains(color))
                return color;
        }

        return PlayerColors[lobby.Players.Count % PlayerColors.Length];
    }
}