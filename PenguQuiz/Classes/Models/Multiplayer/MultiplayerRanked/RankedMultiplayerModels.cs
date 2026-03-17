using WebQuizGame.Classes.Models.Multiplayer;

namespace WebQuizGame.Classes.Models.Multiplayer.MultiplayerRanked;

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