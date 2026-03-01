using System.Text.Json.Serialization;

namespace QuizAPI.Models;

public class TriviaResponse
{
    [JsonPropertyName("response_code")]
    public int ResponseCode { get; set; }

    [JsonPropertyName("results")]
    public List<TriviaQuestion> Results { get; set; } = new();
}