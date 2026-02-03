namespace WebQuizGame.Classes.Models;

public class AnswerRecord
{
    public Question Question { get; set; }
    public bool IsCorrect { get; set; }
    public double TimeTaken { get; set; }
}
