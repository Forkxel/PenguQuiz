namespace WebQuizGame.Classes.Models;

public record PlayerInfo(string ConnectionId, string Username);

public record LobbySettings(
    int Amount,
    int TimePerQuestion,
    string Difficulty,       
    List<int> CategoryIds 
);

public record CreateLobbyRequest(string HostUsername, LobbySettings Settings);
public record JoinLobbyRequest(string LobbyCode, string Username);

public record QuickMatchRequest(
    string Username,
    LobbySettings Preferences,
    int MinPlayers = 2,
    int MaxPlayers = 4
);

public record LobbyState(
    string Code,
    string HostUsername,
    LobbySettings Settings,
    List<string> Players,
    bool IsStarted,
    bool IsMatchmaking,
    DateTime? MatchmakingEndsAtUtc
);

public class MultiplayerDto
{
    
}