using QuizAPI.Models.Multiplayer.MultiplayerRanked;

namespace QuizAPI.Services;

public class RankedMultiplayerManager
{
    private readonly Dictionary<string, RankedLobby> _lobbies = new();
    private readonly object _lock = new();

    private static readonly string[] PlayerColors =
    {
        "#ff6b6b",
        "#4dabf7",
        "#51cf66",
        "#ffd43b"
    };

    public RankedLobbyState QuickMatch(int userId, string connectionId, string username, string avatarKey = "default_1")
    {
        lock (_lock)
        {
            var lobby = _lobbies.Values.FirstOrDefault(l => !l.IsStarted && l.Players.Count < 4);

            if (lobby != null)
            {
                lobby.Players.Add(new RankedPlayerInfo(
                    userId,
                    connectionId,
                    username,
                    avatarKey,
                    GetNextAvailableColor(lobby)));

                return ToState(lobby);
            }

            var created = new RankedLobby
            {
                Code = GenerateCode()
            };

            created.Players.Add(new RankedPlayerInfo(
                userId,
                connectionId,
                username,
                avatarKey,
                GetNextAvailableColor(created)));

            _lobbies[created.Code] = created;
            return ToState(created);
        }
    }

    public RankedLobby? GetLobby(string code)
    {
        lock (_lock)
        {
            _lobbies.TryGetValue(code, out var lobby);
            return lobby;
        }
    }

    public RankedLobbyState? GetState(string code)
    {
        lock (_lock)
        {
            if (!_lobbies.TryGetValue(code, out var lobby))
                return null;

            return ToState(lobby);
        }
    }

    private RankedLobbyState ToState(RankedLobby lobby)
    {
        return new RankedLobbyState(
            lobby.Code,
            lobby.Settings,
            lobby.Players.Select(p => p.Username).ToList(),
            lobby.Players.Select(p => new RankedLobbyPlayerView(p.Username, p.AvatarKey, p.PlayerColor)).ToList(),
            lobby.IsStarted,
            lobby.IsMatchmaking,
            lobby.MatchmakingEndsAtUtc
        );
    }

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

    private string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();

        string code;
        do
        {
            code = new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }
        while (_lobbies.ContainsKey(code));

        return code;
    }
}