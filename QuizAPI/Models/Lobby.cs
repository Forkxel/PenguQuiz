namespace QuizAPI.Models;

public class Lobby
{
    public string Code { get; init; } = "";
    public string HostConnectionId { get; set; } = "";
    public string HostUsername { get; set; } = "";

    public LobbySettings Settings { get; set; } =
        new LobbySettings(10, 10, "any", new List<int>());

    public List<PlayerInfo> Players { get; } = new();
    public bool IsStarted { get; set; } = false;

    public int MaxPlayers { get; set; } = 4;
    public int MinPlayers { get; set; } = 2;

    public string GroupName => $"lobby:{Code}";
    
    public List<TriviaQuestion> Questions { get; set; } = new();
    public int CurrentQuestionIndex { get; set; } = 0;
    public bool QuestionLocked { get; set; } = false;
    public Dictionary<string, int> Scores { get; } = new();
    public bool IsQuickMatch { get; set; } = false;
    
    public DateTime QuestionStartedAtUtc { get; set; } = DateTime.UtcNow;
    public string? FirstAnswerConnectionId { get; set; }
    public string? FirstAnswerText { get; set; }
    public bool? FirstAnswerCorrect { get; set; }
    public CancellationTokenSource? QuestionCts { get; set; }
}