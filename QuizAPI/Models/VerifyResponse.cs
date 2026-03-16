namespace QuizAPI.Models;

public class VerifyResponse
{
    public bool IsValid { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string AvatarKey { get; set; } = "default_1";
}