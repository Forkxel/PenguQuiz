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

                lobby.Players.Add(new RankedPlayerInfo(
                    userId,
                    connectionId,
                    username,
                    avatarKey,
                    GetNextAvailableColor(lobby)));
                return lobby;
            }

            var created = new RankedLobby
            {
                Code = GenerateCode(),
                Settings = new LobbySettings(15, 15, "any", new List<int>()),
                IsStarted = false,
                IsMatchmaking = false
            };

            created.Players.Add(new RankedPlayerInfo(
                userId,
                connectionId,
                username,
                avatarKey,
                GetNextAvailableColor(created)));
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
            lobby.Players.Select(p => new RankedLobbyPlayerView(p.Username, p.AvatarKey, p.PlayerColor)).ToList(),
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
    
    private static readonly string[] PlayerColors =
    {
        "#ff6b6b",
        "#4dabf7",
        "#51cf66",
        "#ffd43b"
    };

    private static string GetNextAvailableColor(RankedLobby lobby)
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