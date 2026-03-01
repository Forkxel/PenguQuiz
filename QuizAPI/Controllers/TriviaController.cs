using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QuizAPI.Models;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TriviaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public TriviaController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    // GET: /api/trivia?amount=10&difficulty=easy&categories=9,22&fresh=true
    [HttpGet]
    public async Task<ActionResult<TriviaResponse>> Get(
        [FromQuery] int amount = 10,
        [FromQuery] string? difficulty = "any",
        [FromQuery] string? categories = null,
        [FromQuery] bool fresh = false)
    {
        amount = Math.Clamp(amount, 1, 50);

        if (string.IsNullOrWhiteSpace(categories))
        {
            var single = await FetchCached(amount, difficulty, categoryId: null, fresh);
            return Ok(single);
        }

        var ids = categories.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.Parse(s.Trim()))
            .ToList();

        if (ids.Count == 0)
        {
            var single = await FetchCached(amount, difficulty, categoryId: null, fresh);
            return Ok(single);
        }

        int baseAmount = amount / ids.Count;
        int remainder = amount % ids.Count;

        var combined = new TriviaResponse { ResponseCode = 0 };

        for (int i = 0; i < ids.Count; i++)
        {
            int toLoad = baseAmount + (i < remainder ? 1 : 0);
            if (toLoad <= 0) continue;

            var part = await FetchCached(toLoad, difficulty, ids[i], fresh);

            if (part.Results != null && part.Results.Count > 0)
                combined.Results.AddRange(part.Results);

            if (i < ids.Count - 1)
                await Task.Delay(5000);
        }

        return Ok(combined);
    }

    private async Task<TriviaResponse> FetchCached(int amount, string? difficulty, int? categoryId, bool fresh)
    {
        var key = $"amount={amount}|diff={difficulty}|cat={categoryId?.ToString() ?? "any"}";

        if (!fresh && _cache.TryGetValue(key, out TriviaResponse? cached) && cached != null)
            return cached;

        var client = _httpClientFactory.CreateClient("OpenTdb");

        var url = $"api.php?amount={amount}";

        if (!string.IsNullOrWhiteSpace(difficulty) && difficulty != "any")
            url += $"&difficulty={difficulty}";

        if (categoryId.HasValue)
            url += $"&category={categoryId.Value}";

        TriviaResponse result = await FetchWithRetry(client, url);

        if (!fresh)
            _cache.Set(key, result, TimeSpan.FromSeconds(60));

        return result;
    }

    private static async Task<TriviaResponse> FetchWithRetry(HttpClient client, string url)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TriviaResponse>();
                return data ?? new TriviaResponse { ResponseCode = -1 };
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                continue;
            }
            
            return new TriviaResponse { ResponseCode = -1 };
        }

        return new TriviaResponse { ResponseCode = 429 };
    }
}