namespace WebQuizGame.Classes;

public class AnswerRecord
{
    public Question Question { get; set; }
    public bool IsCorrect { get; set; }
    public int TimeTaken { get; set; }
}
