namespace WebQuizGame.Classes.Models.Multiplayer.MultiplayerRanked;

public record RankedLobbyState(
    string Code,
    LobbySettings Settings,
    List<string> Players,
    bool IsStarted,
    bool IsMatchmaking,
    DateTime? MatchmakingEndsAtUtc
);

public class RankedMatchPlayerResultDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
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