using System.Collections.Concurrent;
using QuizAPI.Models;
using QuizAPI.Models.Multiplayer;
using QuizAPI.Models.Multiplayer.MultiplayerRanked;

namespace QuizAPI.Services;

public class RankedMultiplayerManager
{
    private readonly ConcurrentDictionary<string, RankedLobby> _lobbies = new();
    private readonly object _lock = new();

    public RankedLobby QuickMatch(int userId, string connectionId, string username, string avatarKey)
    {
        lock (_lock)
        {
            foreach (var existing in _lobbies.Values)
            {
                if (existing.Players.Any(p => p.ConnectionId == connectionId || p.UserId == userId))
                    return existing;
            }

            foreach (var lobby in _lobbies.Values)
            {
                if (lobby.IsStarted) continue;
                if (lobby.Players.Count >= 2) continue;
                if (lobby.Players.Any(p => p.UserId == userId)) continue;

                lobby.Players.Add(new RankedPlayerInfo(userId, connectionId, username, avatarKey));
                return lobby;
            }

            var created = new RankedLobby
            {
                Code = GenerateCode(),
                Settings = new LobbySettings(15, 15, "any", new List<int>()),
                IsStarted = false,
                IsMatchmaking = false
            };

            created.Players.Add(new RankedPlayerInfo(userId, connectionId, username, avatarKey));
            _lobbies[created.Code] = created;

            return created;
        }
    }

    public bool TryGetLobby(string code, out RankedLobby? lobby)
        => _lobbies.TryGetValue(code, out lobby);

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

                if (lobby.Players.Count == 0)
                {
                    lobby.MatchmakingCts?.Cancel();
                    lobby.QuestionCts?.Cancel();
                    _lobbies.TryRemove(lobby.Code, out _);
                }
            }
        }
    }

    public static RankedLobbyState ToState(RankedLobby lobby) =>
        new RankedLobbyState(
            lobby.Code,
            lobby.Settings,
            lobby.Players.Select(p => p.Username).ToList(),
            lobby.Players.Select(p => new RankedLobbyPlayerView(p.Username, p.AvatarKey)).ToList(),
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