namespace WebQuizGame.Classes;

public class Question
{
    public string Text { get; set; } = "";
    public string CorrectAnswer { get; set; } = "";
    public string Type { get; set; } = "";
    public List<string> Answers { get; set; } = new();
}