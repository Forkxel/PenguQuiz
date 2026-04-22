using WebQuizGame.Classes.Models;

namespace WebQuizGame.Classes.Models.Multiplayer;

public class NewQuestionDto
{
    public TriviaQuestion Question { get; set; } = default!;
    public DateTime QuestionStartedAtUtc { get; set; }
}
