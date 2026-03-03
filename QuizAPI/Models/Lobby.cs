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
}