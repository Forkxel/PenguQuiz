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

    public AuthController(DatabaseServices db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] UserDto user)
    {
        var userId = _db.RegisterUser(user.Username, user.Password);

        if (userId == null)
            return BadRequest("Username already exists");

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
                Username = dbUser.Username
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
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var usernameClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || string.IsNullOrWhiteSpace(usernameClaim))
            return Unauthorized();

        return Ok(new VerifyResponse
        {
            IsValid = true,
            UserId = int.Parse(userIdClaim),
            Username = usernameClaim
        });
    }
}