namespace QuizAPI.Models;

public record CustomQuizLobbyPlayerInfo(
    string ConnectionId,
    string Username,
    string AvatarKey = "default",
    string PlayerColor = "#6f5cff"
);

public record CustomQuizLobbyPlayerView(
    string Username,
    string AvatarKey,
    string PlayerColor
);

public record CustomQuizLivePlayerScoreDto(
    string Username,
    string AvatarKey,
    int Score,
    string PlayerColor
);

public record CreateCustomQuizMultiplayerLobbyRequest(
    int QuizId,
    string HostUsername,
    string AvatarKey = "default"
);

public record JoinCustomQuizMultiplayerLobbyRequest(
    string LobbyCode,
    string Username,
    string AvatarKey = "default"
);

public record CustomQuizLobbyState(
    string Code,
    int QuizId,
    string QuizTitle,
    string HostUsername,
    int TimePerQuestion,
    int QuestionCount,
    List<string> Players,
    List<CustomQuizLobbyPlayerView> PlayerDetails,
    bool IsStarted
);

public class CustomQuizSubmittedAnswer
{
    public string Answer { get; set; } = "";
    public DateTime AnsweredAtUtc { get; set; }
}

public class CustomQuizQuestionPlayerAnswerDto
{
    public string Username { get; set; } = "";
    public string AvatarKey { get; set; } = "default";
    public string PlayerColor { get; set; } = "#6f5cff";
    public string? Answer { get; set; }
    public bool IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
    public int ResponseMs { get; set; }
}

public class CustomQuizQuestionResolutionDto
{
    public string CorrectAnswer { get; set; } = "";
    public List<CustomQuizQuestionPlayerAnswerDto> Results { get; set; } = new();
}

public class CustomQuizLobby
{
    public string Code { get; init; } = "";
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = "";
    public string HostConnectionId { get; set; } = "";
    public string HostUsername { get; set; } = "";
    public int TimePerQuestion { get; set; } = 15;

    public List<CustomQuizLobbyPlayerInfo> Players { get; } = new();

    public bool IsStarted { get; set; } = false;
    public int MaxPlayers { get; set; } = 4;
    public int MinPlayers { get; set; } = 2;

    public string GroupName => $"customquiz-lobby:{Code}";

    public List<TriviaQuestion> Questions { get; set; } = new();
    public int CurrentQuestionIndex { get; set; } = 0;
    public bool QuestionLocked { get; set; } = false;

    public Dictionary<string, int> Scores { get; } = new();
    public DateTime QuestionStartedAtUtc { get; set; } = DateTime.UtcNow;
    public CancellationTokenSource? QuestionCts { get; set; }

    public Dictionary<string, CustomQuizSubmittedAnswer> SubmittedAnswers { get; } = new();
}