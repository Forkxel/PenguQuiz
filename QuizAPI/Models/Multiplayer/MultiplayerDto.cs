public record PlayerInfo(string ConnectionId, string Username, string AvatarKey = "default_1");

public record LobbyPlayerView(
    string Username,
    string AvatarKey
);

public record LivePlayerScoreDto(
    string Username,
    string AvatarKey,
    int Score
);

public record LobbySettings(
    int Amount,
    int TimePerQuestion,
    string Difficulty,
    List<int> CategoryIds
);

public record CreateLobbyRequest(
    string HostUsername,
    LobbySettings Settings,
    string AvatarKey = "default_1"
);

public record JoinLobbyRequest(
    string LobbyCode,
    string Username,
    string AvatarKey = "default_1"
);

public record QuickMatchRequest(
    string Username,
    LobbySettings Preferences,
    int MinPlayers = 2,
    int MaxPlayers = 4,
    string AvatarKey = "default_1"
);

public record LobbyState(
    string Code,
    string HostUsername,
    LobbySettings Settings,
    List<string> Players,
    List<LobbyPlayerView> PlayerDetails,
    bool IsStarted,
    bool IsMatchmaking,
    DateTime? MatchmakingEndsAtUtc
);