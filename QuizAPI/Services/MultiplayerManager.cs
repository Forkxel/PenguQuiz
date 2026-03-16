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

        lobby.Players.Add(new PlayerInfo(hostConnectionId, hostUsername, hostAvatarKey));
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

            lobby.Players.Add(new PlayerInfo(connectionId, username, avatarKey));
            return (true, "");
        }
    }

    public void LeaveByConnection(string connectionId)
    {
        foreach (var kv in _lobbies)
        {
            var lobby = kv.Value;

            lock (_lock)
            {
                var idx = lobby.Players.FindIndex(p => p.ConnectionId == connectionId);
                if (idx < 0) continue;

                lobby.Players.RemoveAt(idx);
                
                if (lobby.HostConnectionId == connectionId)
                {
                    if (lobby.Players.Count > 0)
                    {
                        lobby.HostConnectionId = lobby.Players[0].ConnectionId;
                        lobby.HostUsername = lobby.Players[0].Username;
                    }
                    else
                    {
                        _lobbies.TryRemove(lobby.Code, out _);
                    }
                }
            }
        }
    }

    public void UpdateSettings(string code, string hostConnId, LobbySettings settings)
    {
        if (!_lobbies.TryGetValue(code, out var lobby)) return;
        lock (_lock)
        {
            if (lobby.HostConnectionId != hostConnId) return;
            if (lobby.IsStarted) return;
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
                if (lobby.MaxPlayers > 0 && lobby.Players.Count >= lobby.MaxPlayers)
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
            
            var created = CreateLobby(connectionId, username,avatarKey, prefs);
            created.MinPlayers = minPlayers;
            created.MaxPlayers = maxPlayers;
            created.IsQuickMatch = true;
            
            created.Settings = new LobbySettings(
                prefs.Amount,
                prefs.TimePerQuestion,
                prefs.Difficulty,
                new List<int>()
            );

            return created;
        }
    }

    public static LobbyState ToState(Lobby lobby) =>
        new LobbyState(
            lobby.Code,
            lobby.HostUsername,
            lobby.Settings,
            lobby.Players.Select(p => p.Username).ToList(),
            lobby.Players.Select(p => new LobbyPlayerView(p.Username, p.AvatarKey)).ToList(),
            lobby.IsStarted,
            lobby.IsMatchmaking,
            lobby.MatchmakingEndsAtUtc
        );

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
    }
}