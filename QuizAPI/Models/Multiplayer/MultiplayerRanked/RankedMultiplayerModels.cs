namespace QuizAPI.Models.Multiplayer.MultiplayerRanked;

public record RankedPlayerInfo(
    int UserId,
    string ConnectionId,
    string Username,
    string AvatarKey = "default_1",
    string PlayerColor = "#6f5cff"
);

public record RankedLobbyPlayerView(
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

public class RankedLobby
{
    public string Code { get; init; } = "";
    public List<RankedPlayerInfo> Players { get; } = new();

    public LobbySettings Settings { get; set; } =
        new LobbySettings(15, 15, "any", new List<int>());

    public bool IsStarted { get; set; } = false;
    public bool IsMatchmaking { get; set; } = false;
    public DateTime? MatchmakingEndsAtUtc { get; set; }

    public string GroupName => $"ranked-lobby:{Code}";

    public List<TriviaQuestion> Questions { get; set; } = new();
    public int CurrentQuestionIndex { get; set; } = 0;
    public bool QuestionLocked { get; set; } = false;

    public Dictionary<string, int> Scores { get; } = new();

    public DateTime QuestionStartedAtUtc { get; set; } = DateTime.UtcNow;
    public CancellationTokenSource? QuestionCts { get; set; }
    public CancellationTokenSource? MatchmakingCts { get; set; }
}

public record RankedLobbyState(
    string Code,
    LobbySettings Settings,
    List<string> Players,
    List<RankedLobbyPlayerView> PlayerDetails,
    bool IsStarted,
    bool IsMatchmaking,
    DateTime? MatchmakingEndsAtUtc
);

public class RankedMatchPlayerResultDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string AvatarKey { get; set; } = "default_1";
    public int Score { get; set; }

    public int OldRating { get; set; }
    public int NewRating { get; set; }
    public int Delta { get; set; }

    public int Place { get; set; }
    public bool IsWinner { get; set; }
}

public class RankedGameFinishedDto
{
    public string LobbyCode { get; set; } = "";
    public List<RankedMatchPlayerResultDto> Results { get; set; } = new();
}