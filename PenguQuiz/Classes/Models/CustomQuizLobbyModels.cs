namespace WebQuizGame.Classes.Models;

public record CreateCustomQuizMultiplayerLobbyRequest(
    int QuizId,
    string HostUsername,
    string AvatarKey = "default_1"
);

public record JoinCustomQuizMultiplayerLobbyRequest(
    string LobbyCode,
    string Username,
    string AvatarKey = "default_1"
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

public class CustomQuizQuestionPlayerAnswerDto
{
    public string Username { get; set; } = "";
    public string AvatarKey { get; set; } = "default_1";
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