using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DatabaseServices _db;
    private readonly JwtService _jwt;
    private readonly UsernameValidatorService _usernameValidator;

    public AuthController(DatabaseServices db, JwtService jwt,  UsernameValidatorService userValidator)
    {
        _db = db;
        _jwt = jwt;
        _usernameValidator = userValidator;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] UserDto user)
    {
        var usernameError = _usernameValidator.Validate(user.Username);
        if (usernameError != null)
            return BadRequest(usernameError);

        var userId = _db.RegisterUser(user.Username.Trim(), user.Password);

        if (userId == null)
            return Conflict("Username already exists");

        _db.CreateDefaultRanking(userId.Value);

        return Ok("User registered");
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] UserDto user)
    {
        try
        {
            var dbUser = _db.GetUserByLogin(user.Username, user.Password);

            if (dbUser == null)
                return Unauthorized("Invalid credentials");

            var token = _jwt.GenerateToken(dbUser.Id, dbUser.Username);

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = dbUser.Id,
                Username = dbUser.Username,
                AvatarKey = dbUser.AvatarKey
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("LOGIN ERROR:");
            Console.WriteLine(ex.ToString());
            return StatusCode(500, ex.Message);
        }
    }

    [Authorize]
    [HttpGet("verify")]
    public IActionResult Verify()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        int userId = int.Parse(userIdClaim);

        var dbUser = _db.GetUserById(userId);
        if (dbUser == null)
            return Unauthorized();

        return Ok(new VerifyResponse
        {
            IsValid = true,
            UserId = dbUser.Id,
            Username = dbUser.Username,
            AvatarKey = dbUser.AvatarKey
        });
    }
}