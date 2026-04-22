using QuizAPI.Models;

namespace QuizAPI.Models.Multiplayer;

public class NewQuestionDto
{
    public TriviaQuestion Question { get; set; } = default!;
    public DateTime QuestionStartedAtUtc { get; set; }
}
