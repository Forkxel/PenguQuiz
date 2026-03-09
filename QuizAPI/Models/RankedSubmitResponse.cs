namespace QuizAPI.Models;

public class RankedSubmitResponse
{
    public int OldRating { get; set; }
    public int NewRating { get; set; }
    public int Delta { get; set; }
}