namespace WebQuizGame.Classes.Models;

public class RankedProfileResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";

    public int SingleElo { get; set; }
    public int MultiElo { get; set; }

    public int SingleRankedPlayed { get; set; }
    public int SingleRankedWins { get; set; }

    public int MultiRankedPlayed { get; set; }
    public int MultiRankedWins { get; set; }
}