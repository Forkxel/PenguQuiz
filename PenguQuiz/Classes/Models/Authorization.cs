using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WebQuizGame.Classes.Models;

public class Authorization
{
    public bool IsLoggedIn { get; private set; }
    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? AvatarKey { get; private set; }

    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _httpClient;

    public Authorization(ILocalStorageService localStorage, HttpClient httpClient)
    {
        _localStorage = localStorage;
        _httpClient = httpClient;
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

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.GetAsync("api/auth/verify");

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
        AvatarKey = data.AvatarKey;
    }

    public async Task Login(LoginResponse loginResponse)
    {
        await _localStorage.SetItemAsync("token", loginResponse.Token);

        IsLoggedIn = true;
        UserId = loginResponse.UserId;
        Username = loginResponse.Username;
        AvatarKey = loginResponse.AvatarKey;
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("token");
        _httpClient.DefaultRequestHeaders.Authorization = null;

        IsLoggedIn = false;
        UserId = null;
        Username = null;
        AvatarKey = null;
    }
    
    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>("token");
    }
}