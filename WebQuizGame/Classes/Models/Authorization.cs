using Blazored.LocalStorage;

namespace WebQuizGame.Classes.Models;

public class Authorization
{
    private readonly ILocalStorageService _localStorage;

    public bool IsLoggedIn { get; private set; }
    public string? Username { get; private set; }

    public Authorization(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("token");

        if (string.IsNullOrEmpty(token))
            return;

        var client = new HttpClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("http://localhost:5237/api/auth/verify");

        IsLoggedIn = response.IsSuccessStatusCode;
    }

    public async Task Login(string username, string token)
    {
        await _localStorage.SetItemAsync("token", token);

        Username = username;
        IsLoggedIn = true;
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("token");

        Username = null;
        IsLoggedIn = false;
    }
}
