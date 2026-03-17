namespace WebQuizGame.Classes.Models;

public class LoginResponse
{
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string AvatarKey { get; set; } = "default_1";
}