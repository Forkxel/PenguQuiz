using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DatabaseServices _db;

    public AuthController(DatabaseServices db)
    {
        _db = db;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] UserDto user)
    {
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

        return Ok("Login successful");
    }
}