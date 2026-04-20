using System.Collections.Concurrent;
using QuizAPI.Models;
using QuizAPI.Models.Multiplayer;

namespace QuizAPI.Services;

public class MultiplayerManager
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();
    private readonly object _lock = new();

    public Lobby CreateLobby(string hostConnectionId, string hostUsername, string hostAvatarKey, LobbySettings settings)
    {
        var code = GenerateCode();

        var lobby = new Lobby
        {
            Code = code,
            HostConnectionId = hostConnectionId,
            HostUsername = hostUsername,
            Settings = settings,
            IsStarted = false,
            MinPlayers = 2,
            MaxPlayers = 4
        };

        lobby.Players.Add(new PlayerInfo(hostConnectionId, hostUsername, hostAvatarKey, GetNextAvailableColor(lobby)));
        _lobbies[code] = lobby;
        return lobby;
    }

    public bool TryGetLobby(string code, out Lobby? lobby) => _lobbies.TryGetValue(code, out lobby);

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

            lobby.Players.Add(new PlayerInfo(connectionId, username, avatarKey, GetNextAvailableColor(lobby)));
            return (true, "");
        }
    }

    public void UpdateSettings(string code, string hostConnId, LobbySettings settings)
    {
        if (!_lobbies.TryGetValue(code, out var lobby)) return;

        lock (_lock)
        {
            if (lobby.HostConnectionId != hostConnId) return;
            if (lobby.IsStarted) return;
            if (lobby.IsQuickMatch) return;

            lobby.Settings = settings;
        }
    }

    public void StartGame(string code, string hostConnId)
    {
        if (!_lobbies.TryGetValue(code, out var lobby)) return;
        lock (_lock)
        {
            if (lobby.HostConnectionId != hostConnId) return;
            if (lobby.Players.Count < lobby.MinPlayers) return;
            lobby.IsStarted = true;
        }
    }
    
    public Lobby QuickMatch(string connectionId, string username, string avatarKey, LobbySettings prefs, int minPlayers, int maxPlayers)
    {
        if (minPlayers <= 0) minPlayers = 2;
        if (maxPlayers <= 0) maxPlayers = 4;

        var quickSettings = new LobbySettings(
            10,
            10,
            "any",
            new List<int> { 9 });

        lock (_lock)
        {
            foreach (var existing in _lobbies.Values)
            {
                if (existing.Players.Any(p => p.ConnectionId == connectionId))
                    return existing;
            }

            foreach (var lobby in _lobbies.Values)
            {
                if (lobby.IsStarted) continue;
                if (!lobby.IsQuickMatch) continue;
                if (lobby.MaxPlayers > 0 && lobby.Players.Count >= lobby.MaxPlayers)
                    continue;

                var sameSettings =
                    lobby.Settings.Amount == quickSettings.Amount &&
                    lobby.Settings.TimePerQuestion == quickSettings.TimePerQuestion &&
                    string.Equals(lobby.Settings.Difficulty, quickSettings.Difficulty, StringComparison.OrdinalIgnoreCase) &&
                    lobby.Settings.CategoryIds.SequenceEqual(quickSettings.CategoryIds);

                if (!sameSettings)
                    continue;

                var join = JoinLobby(lobby.Code, connectionId, username, avatarKey);
                if (join.ok)
                {
                    lobby.MinPlayers = minPlayers;
                    lobby.MaxPlayers = maxPlayers;
                    lobby.IsQuickMatch = true;
                    return lobby;
                }
            }

            var created = CreateLobby(connectionId, username, avatarKey, quickSettings);
            created.MinPlayers = minPlayers;
            created.MaxPlayers = maxPlayers;
            created.IsQuickMatch = true;
            return created;
        }
    }

    public static LobbyState ToState(Lobby lobby) =>
        new LobbyState(
            lobby.Code,
            lobby.HostUsername,
            lobby.Settings,
            lobby.Players.Select(p => p.Username).ToList(),
            lobby.Players.Select(p => new LobbyPlayerView(p.Username, p.AvatarKey, p.PlayerColor)).ToList(),
            lobby.IsStarted,
            lobby.IsMatchmaking,
            lobby.MatchmakingEndsAtUtc,
            lobby.IsQuickMatch
        );

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
    }
    
    private static readonly string[] PlayerColors =
    {
        "#ff6b6b",
        "#4dabf7",
        "#51cf66",
        "#ffd43b"
    };

    private static string GetNextAvailableColor(Lobby lobby)
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
    
    public bool IsPlayerInLobby(string code, string connectionId)
    {
        if (!_lobbies.TryGetValue(code, out var lobby))
            return false;

        lock (_lock)
        {
            return lobby.Players.Any(p => p.ConnectionId == connectionId);
        }
    }

    public Lobby? LeaveByConnection(string connectionId)
    {
        foreach (var kv in _lobbies)
        {
            var lobby = kv.Value;

            lock (_lock)
            {
                var idx = lobby.Players.FindIndex(p => p.ConnectionId == connectionId);
                if (idx < 0) continue;

                lobby.Players.RemoveAt(idx);

                if (!lobby.IsStarted && lobby.IsMatchmaking && lobby.Players.Count < lobby.MinPlayers)
                {
                    lobby.IsMatchmaking = false;
                    lobby.MatchmakingEndsAtUtc = null;
                    lobby.MatchmakingCts?.Cancel();
                    lobby.MatchmakingCts = null;
                }

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
                        lobby.MatchmakingCts?.Cancel();
                        _lobbies.TryRemove(lobby.Code, out _);
                        return null;
                    }
                }

                lobby.SubmittedAnswers.Remove(connectionId);
                lobby.Scores.Remove(connectionId);

                return lobby;
            }
        }

        return null;
    }
}