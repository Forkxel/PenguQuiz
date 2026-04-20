using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomQuizController : ControllerBase
{
    private readonly DatabaseServices _db;

    public CustomQuizController(DatabaseServices db)
    {
        _db = db;
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateCustomQuizRequest req)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        var userId = int.Parse(userIdClaim);

        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Quiz title is required.");

        if (req.Questions == null || req.Questions.Count == 0)
            return BadRequest("At least one question is required.");

        var id = _db.CreateCustomQuiz(userId, req);
        return Ok(new { Id = id });
    }

    [HttpGet("mine")]
    public IActionResult GetMine()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        var userId = int.Parse(userIdClaim);
        var quizzes = _db.GetCustomQuizzesByUser(userId);
        return Ok(quizzes);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        var userId = int.Parse(userIdClaim);
        var quiz = _db.GetCustomQuizById(id, userId);

        if (quiz == null)
            return NotFound();

        return Ok(quiz);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        var userId = int.Parse(userIdClaim);
        var ok = _db.DeleteCustomQuiz(id, userId);

        if (!ok)
            return NotFound();

        return Ok();
    }
}