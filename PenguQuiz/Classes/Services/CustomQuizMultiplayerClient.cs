using Microsoft.AspNetCore.SignalR.Client;
using WebQuizGame.Classes.Models;

namespace WebQuizGame.Classes.Services;

public class CustomQuizMultiplayerClient
{
    private HubConnection? _conn;

    public event Action<CustomQuizLobbyState>? OnLobbyUpdated;
    public event Action<TriviaQuestion>? OnNewQuestion;
    public event Action<Dictionary<string, int>>? OnGameFinished;
    public event Action<string>? OnGameError;
    public event Action<CustomQuizQuestionResolutionDto>? OnQuestionResolved;
    public event Action<List<CustomQuizLivePlayerScoreDto>>? OnScoresUpdated;

    public string? LocalUsername { get; private set; }

    public async Task ConnectAsync(string apiBaseUrl)
    {
        if (_conn != null)
            return;

        _conn = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl.TrimEnd('/')}/hubs/custom-quiz-multiplayer")
            .WithAutomaticReconnect()
            .Build();

        _conn.On<CustomQuizLobbyState>("LobbyUpdated", state => OnLobbyUpdated?.Invoke(state));
        _conn.On<string>("GameError", msg => OnGameError?.Invoke(msg));
        _conn.On<TriviaQuestion>("NewQuestion", q => OnNewQuestion?.Invoke(q));
        _conn.On<Dictionary<string, int>>("GameFinished", scores => OnGameFinished?.Invoke(scores));
        _conn.On<CustomQuizQuestionResolutionDto>("QuestionResolved", dto => OnQuestionResolved?.Invoke(dto));
        _conn.On<List<CustomQuizLivePlayerScoreDto>>("ScoresUpdated", scores => OnScoresUpdated?.Invoke(scores));

        await _conn.StartAsync();
    }

    public async Task<CustomQuizLobbyState> CreateLobbyAsync(CreateCustomQuizMultiplayerLobbyRequest req)
    {
        LocalUsername = req.HostUsername;
        return await _conn!.InvokeAsync<CustomQuizLobbyState>("CreateLobby", req);
    }

    public async Task<CustomQuizLobbyState> JoinLobbyAsync(JoinCustomQuizMultiplayerLobbyRequest req)
    {
        LocalUsername = req.Username;
        return await _conn!.InvokeAsync<CustomQuizLobbyState>("JoinLobby", req);
    }

    public async Task<CustomQuizLobbyState> GetLobbyStateAsync(string lobbyCode)
        => await _conn!.InvokeAsync<CustomQuizLobbyState>("GetLobbyState", lobbyCode);

    public async Task LeaveLobbyAsync(string lobbyCode)
        => await _conn!.InvokeAsync("LeaveLobby", lobbyCode);

    public async Task StartGameAsync(string lobbyCode)
        => await _conn!.InvokeAsync("StartGame", lobbyCode);

    public async Task<TriviaQuestion?> GetCurrentQuestionAsync(string lobbyCode)
        => await _conn!.InvokeAsync<TriviaQuestion?>("GetCurrentQuestion", lobbyCode);

    public async Task<DateTime?> GetQuestionStartedAtUtcAsync(string lobbyCode)
        => await _conn!.InvokeAsync<DateTime?>("GetQuestionStartedAtUtc", lobbyCode);

    public async Task<List<CustomQuizLivePlayerScoreDto>> GetLiveScoresAsync(string lobbyCode)
        => await _conn!.InvokeAsync<List<CustomQuizLivePlayerScoreDto>>("GetLiveScores", lobbyCode);

    public async Task AnswerQuestionAsync(string lobbyCode, string answer)
        => await _conn!.InvokeAsync("AnswerQuestion", lobbyCode, answer);

    public async Task DisconnectAsync()
    {
        if (_conn == null)
            return;

        try
        {
            await _conn.StopAsync();
        }
        catch
        {
        }

        try
        {
            await _conn.DisposeAsync();
        }
        catch
        {
        }

        _conn = null;
        LocalUsername = null;
    }
}