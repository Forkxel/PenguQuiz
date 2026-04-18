using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankedController : ControllerBase
{
    private readonly DatabaseServices _db;

    public RankedController(DatabaseServices db)
    {
        _db = db;
    }

    [Authorize]
    [HttpPost("single/submit")]
    public IActionResult SubmitSingle([FromBody] SubmitSingleRankedRequest req)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        int userId = int.Parse(userIdClaim);

        int oldRating = _db.GetSingleRating(userId);
        int delta = CalculateSingleDelta(req, oldRating);
        int newRating = Math.Max(0, oldRating + delta);

        _db.UpdateSingleRating(userId, newRating, delta > 0);

        return Ok(new RankedSubmitResponse
        {
            OldRating = oldRating,
            NewRating = newRating,
            Delta = delta
        });
    }
    
    [Authorize]
    [HttpPost("single/forfeit")]
    public IActionResult ForfeitSingle()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var old = _db.GetSingleRating(userId);
        var penalty = 15;

        var newRating = Math.Max(0, old - penalty);

        _db.UpdateSingleRating(userId, newRating, false);

        return Ok(new
        {
            OldRating = old,
            NewRating = newRating,
            Delta = -penalty
        });
    }

    private static int CalculateSingleDelta(SubmitSingleRankedRequest req, int currentRating)
    {
        double accuracy = req.TotalQuestions == 0
            ? 0
            : (double)req.CorrectAnswers / req.TotalQuestions;

        int delta = 0;

        if (accuracy >= 0.9) delta += 25;
        else if (accuracy >= 0.8) delta += 15;
        else if (accuracy >= 0.7) delta += 8;
        else if (accuracy >= 0.6) delta += 0;
        else if (accuracy >= 0.5) delta -= 8;
        else delta -= 15;

        if (req.AverageTimeSeconds <= 5) delta += 5;
        else if (req.AverageTimeSeconds >= 15) delta -= 5;

        if (req.Difficulty == "hard") delta += 5;
        else if (req.Difficulty == "easy") delta -= 3;

        if (currentRating >= 1400) delta -= 3;
        if (currentRating >= 1800) delta -= 3;

        return delta;
    }
    
    [Authorize]
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        int userId = int.Parse(userIdClaim);

        _db.EnsureRankingExists(userId);

        var profile = _db.GetRankedProfile(userId);

        if (profile == null)
            return NotFound("Ranked profile not found.");

        return Ok(profile);
    }
    
    [HttpGet("leaderboard/single")]
    public IActionResult GetSingleLeaderboard([FromQuery] int top = 20)
    {
        if (top <= 0) top = 20;
        if (top > 100) top = 100;

        var leaderboard = _db.GetSingleLeaderboard(top);
        return Ok(leaderboard);
    }
    
    [HttpGet("leaderboard/multi")]
    public IActionResult GetMultiLeaderboard([FromQuery] int top = 20)
    {
        if (top <= 0) top = 20;
        if (top > 100) top = 100;

        var leaderboard = _db.GetMultiLeaderboard(top);
        return Ok(leaderboard);
    }
}