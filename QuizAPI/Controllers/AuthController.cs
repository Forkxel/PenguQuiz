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
        Console.WriteLine("USERNAME: '" + user.Username + "'");
        Console.WriteLine("PASSWORD: '" + user.Password + "'");

        bool success = _db.RegisterUser(user.Username, user.Password);

        if (!success)
            return BadRequest("Username already exists");

        return Ok("User registered");
    }


    [HttpPost("login")]
    public IActionResult Login([FromBody] UserDto user)
    {
        bool success = _db.LoginUser(user.Username, user.Password);

        if (!success)
            return Unauthorized("Invalid credentials");
        
        var token = _jwt.GenerateToken(user.Username);

        return Ok(new { token });
    }
    
    [Authorize]
    [HttpGet("verify")]
    public IActionResult Verify()
    {
        return Ok();
    }
}