namespace QuizAPI.Models.Multiplayer;

public record PlayerInfo(string ConnectionId, string Username, string AvatarKey = "default_1", string PlayerColor = "#6f5cff");

public record LobbyPlayerView(
    string Username,
    string AvatarKey,
    string PlayerColor
);

public record LivePlayerScoreDto(
    string Username,
    string AvatarKey,
    int Score,
    string PlayerColor
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

public class SubmittedAnswer
{
    public string Answer { get; set; } = "";
    public DateTime AnsweredAtUtc { get; set; }
}

public class QuestionPlayerAnswerDto
{
    public string Username { get; set; } = "";
    public string AvatarKey { get; set; } = "default_1";
    public string PlayerColor { get; set; } = "#6f5cff";
    public string? Answer { get; set; }
    public bool IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
    public int ResponseMs { get; set; }
}

public class QuestionResolutionDto
{
    public string CorrectAnswer { get; set; } = "";
    public List<QuestionPlayerAnswerDto> Results { get; set; } = new();
}