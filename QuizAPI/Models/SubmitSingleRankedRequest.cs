namespace QuizAPI.Models;

public class SubmitSingleRankedRequest
{
    public int Score { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public double AverageTimeSeconds { get; set; }
    public string Difficulty { get; set; } = "any";
}