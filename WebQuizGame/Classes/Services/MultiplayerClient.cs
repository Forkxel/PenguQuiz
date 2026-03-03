using Microsoft.AspNetCore.SignalR.Client;
using WebQuizGame.Classes.Models;

namespace WebQuizGame.Classes.Services;

public class MultiplayerClient
{
    private HubConnection? _conn;

    public event Action<LobbyState>? OnLobbyUpdated;
    public event Action<LobbyState>? OnGameStarted;
    public event Action<TriviaQuestion>? OnNewQuestion;
    public event Action<string>? OnQuestionWon;
    public event Action<Dictionary<string,int>>? OnGameFinished;
    public event Action<string>? OnGameError;
    public event Action<string,string,bool>? OnQuestionResolved;

    public async Task ConnectAsync(string apiBaseUrl)
    {
        if (_conn != null) return;

        _conn = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl.TrimEnd('/')}/hubs/multiplayer")
            .WithAutomaticReconnect()
            .Build();

        _conn.On<LobbyState>("LobbyUpdated", state => OnLobbyUpdated?.Invoke(state));
        _conn.On<string>("GameError", msg => OnGameError?.Invoke(msg));
        _conn.On<LobbyState>("GameStarted", state => OnGameStarted?.Invoke(state));
        _conn.On<TriviaQuestion>("NewQuestion",
            q => OnNewQuestion?.Invoke(q));

        _conn.On<string>("QuestionWon",
            winner => OnQuestionWon?.Invoke(winner));

        _conn.On<Dictionary<string,int>>("GameFinished",
            scores => OnGameFinished?.Invoke(scores));
        
        _conn.On<string,string,bool>("QuestionResolved",
            (who, chosen, correct) => OnQuestionResolved?.Invoke(who, chosen, correct));

        await _conn.StartAsync();
    }
    
    public async Task AnswerQuestionAsync(string lobbyCode, string answer)
        => await _conn!.InvokeAsync("AnswerQuestion", lobbyCode, answer);

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
    
    public async Task<LobbyState> GetLobbyStateAsync(string lobbyCode)
        => await _conn!.InvokeAsync<LobbyState>("GetLobbyState", lobbyCode);
    
    public async Task<TriviaQuestion?> GetCurrentQuestionAsync(string lobbyCode)
        => await _conn!.InvokeAsync<TriviaQuestion?>("GetCurrentQuestion", lobbyCode);
}