using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly DatabaseServices _db;
    private readonly JwtService _jwt;
    private readonly UsernameValidatorService _usernameValidator;

    private static readonly HashSet<string> AllowedAvatars = new(StringComparer.OrdinalIgnoreCase)
    {
        "default_1",
        "default_2",
        "default_3",
        "cat_blue",
        "cat_red",
        "robot_green",
        "fox_purple",
        "wizard_gold"
    };

    public AccountController(
        DatabaseServices db,
        JwtService jwt,
        UsernameValidatorService usernameValidator)
    {
        _db = db;
        _jwt = jwt;
        _usernameValidator = usernameValidator;
    }

    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        int userId = GetUserId();

        var settings = _db.GetAccountSettings(userId);
        if (settings == null)
            return NotFound("Account not found.");

        return Ok(settings);
    }

    [HttpPost("change-username")]
    public IActionResult ChangeUsername([FromBody] ChangeUsernameRequest req)
    {
        int userId = GetUserId();

        var newUsername = req.NewUsername?.Trim() ?? "";

        var usernameError = _usernameValidator.Validate(newUsername);
        if (usernameError != null)
            return BadRequest(usernameError);

        if (_db.UsernameExists(newUsername, userId))
            return BadRequest("Username already exists.");

        bool ok = _db.UpdateUsername(userId, newUsername);
        if (!ok)
            return NotFound("User not found.");

        var newToken = _jwt.GenerateToken(userId, newUsername);

        return Ok(new ChangeUsernameResponse
        {
            UserId = userId,
            Username = newUsername,
            Token = newToken
        });
    }

    [HttpPost("change-avatar")]
    public IActionResult ChangeAvatar([FromBody] ChangeAvatarRequest req)
    {
        int userId = GetUserId();

        if (string.IsNullOrWhiteSpace(req.AvatarKey) || !AllowedAvatars.Contains(req.AvatarKey))
            return BadRequest("Invalid avatar.");

        bool ok = _db.UpdateAvatar(userId, req.AvatarKey);
        if (!ok)
            return NotFound("User not found.");

        return Ok();
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
            throw new UnauthorizedAccessException();

        return int.Parse(userIdClaim);
    }
}