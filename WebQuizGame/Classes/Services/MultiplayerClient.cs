using Microsoft.AspNetCore.SignalR.Client;
using WebQuizGame.Classes.Models;

namespace WebQuizGame.Classes.Services;

public class MultiplayerClient
{
    private HubConnection? _conn;

    public event Action<LobbyState>? OnLobbyUpdated;
    public event Action<LobbyState>? OnGameStarted;

    public async Task ConnectAsync(string apiBaseUrl)
    {
        if (_conn != null) return;

        _conn = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl.TrimEnd('/')}/hubs/multiplayer")
            .WithAutomaticReconnect()
            .Build();

        _conn.On<LobbyState>("LobbyUpdated", state => OnLobbyUpdated?.Invoke(state));
        _conn.On<LobbyState>("GameStarted", state => OnGameStarted?.Invoke(state));

        await _conn.StartAsync();
    }

    public async Task<LobbyState> CreateLobbyAsync(CreateLobbyRequest req)
        => await _conn!.InvokeAsync<LobbyState>("CreateLobby", req);

    public async Task<LobbyState> JoinLobbyAsync(JoinLobbyRequest req)
        => await _conn!.InvokeAsync<LobbyState>("JoinLobby", req);

    public async Task<LobbyState> QuickMatchAsync(QuickMatchRequest req)
        => await _conn!.InvokeAsync<LobbyState>("QuickMatch", req);

    public async Task UpdateSettingsAsync(string lobbyCode, LobbySettings settings)
        => await _conn!.InvokeAsync("UpdateSettings", lobbyCode, settings);

    public async Task StartGameAsync(string lobbyCode)
        => await _conn!.InvokeAsync("StartGame", lobbyCode);
}