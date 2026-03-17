namespace WebQuizGame.Classes.Models;

public class AccountSettingsResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string AvatarKey { get; set; } = "default_1";
}

public class ChangeUsernameRequest
{
    public string NewUsername { get; set; } = "";
}

public class ChangeAvatarRequest
{
    public string AvatarKey { get; set; } = "";
}

public class ChangeUsernameResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
}