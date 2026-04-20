namespace QuizAPI.Models;

public class CustomQuizQuestionDto
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = "";
    public string Answer1 { get; set; } = "";
    public string Answer2 { get; set; } = "";
    public string? Answer3 { get; set; }
    public string? Answer4 { get; set; }
    public string CorrectAnswer { get; set; } = "";
    public int QuestionOrder { get; set; }
}

public class CustomQuizDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public int TimePerQuestion { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<CustomQuizQuestionDto> Questions { get; set; } = new();
}

public class CreateCustomQuizRequest
{
    public string Title { get; set; } = "";
    public int TimePerQuestion { get; set; } = 15;
    public List<CreateCustomQuizQuestionRequest> Questions { get; set; } = new();
}

public class CreateCustomQuizQuestionRequest
{
    public string QuestionText { get; set; } = "";
    public string Answer1 { get; set; } = "";
    public string Answer2 { get; set; } = "";
    public string? Answer3 { get; set; }
    public string? Answer4 { get; set; }
    public string CorrectAnswer { get; set; } = "";
}