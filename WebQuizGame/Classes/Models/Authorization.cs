using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WebQuizGame.Classes.Models;

public class Authorization
{
    private readonly ILocalStorageService _localStorage;

    public bool IsLoggedIn { get; private set; }
    public int? UserId { get; private set; }
    public string? Username { get; private set; }

    public Authorization(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("token");

        if (string.IsNullOrWhiteSpace(token))
        {
            IsLoggedIn = false;
            UserId = null;
            Username = null;
            return;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("http://localhost:5237/api/auth/verify");

        if (!response.IsSuccessStatusCode)
        {
            await Logout();
            return;
        }

        var data = await response.Content.ReadFromJsonAsync<VerifyResponse>();

        if (data == null || !data.IsValid)
        {
            await Logout();
            return;
        }

        IsLoggedIn = true;
        UserId = data.UserId;
        Username = data.Username;
    }

    public async Task Login(LoginResponse loginResponse)
    {
        await _localStorage.SetItemAsync("token", loginResponse.Token);

        IsLoggedIn = true;
        UserId = loginResponse.UserId;
        Username = loginResponse.Username;
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("token");

        IsLoggedIn = false;
        UserId = null;
        Username = null;
    }
    
    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>("token");
    }
}