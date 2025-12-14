namespace WebQuizGame.Classes;

public class Question
{
    public string Text { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public List<string> Answers { get; set; } = new List<string>();
}