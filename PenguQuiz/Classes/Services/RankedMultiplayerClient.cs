using Microsoft.AspNetCore.SignalR.Client;
using WebQuizGame.Classes.Models;
using WebQuizGame.Classes.Models.Multiplayer.MultiplayerRanked;
using LivePlayerScoreDto = WebQuizGame.Classes.Models.Multiplayer.MultiplayerRanked.LivePlayerScoreDto;

namespace WebQuizGame.Classes.Services;

public class RankedMultiplayerClient
{
    private HubConnection? _conn;
    private string? _tokenUsed;

    public event Action<RankedLobbyState>? OnLobbyUpdated;
    public event Action<TriviaQuestion>? OnNewQuestion;
    public event Action<RankedGameFinishedDto>? OnGameFinished;
    public event Action<string>? OnGameError;
    public event Action<RankedQuestionResolutionDto>? OnQuestionResolved;
    public event Action<List<LivePlayerScoreDto>>? OnScoresUpdated;

    public async Task ConnectAsync(string apiBaseUrl, string token)
    {
        if (_conn != null && _tokenUsed == token && _conn.State == HubConnectionState.Connected)
            return;

        if (_conn != null)
        {
            await _conn.DisposeAsync();
            _conn = null;
        }

        _tokenUsed = token;

        _conn = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl.TrimEnd('/')}/hubs/ranked-multiplayer", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _conn.On<RankedLobbyState>("RankedLobbyUpdated", s => OnLobbyUpdated?.Invoke(s));
        _conn.On<TriviaQuestion>("RankedNewQuestion", q => OnNewQuestion?.Invoke(q));
        _conn.On<RankedGameFinishedDto>("RankedGameFinished", dto => OnGameFinished?.Invoke(dto));
        _conn.On<string>("RankedGameError", msg => OnGameError?.Invoke(msg));
        _conn.On<RankedQuestionResolutionDto>("RankedQuestionResolved", dto => OnQuestionResolved?.Invoke(dto));
        _conn.On<List<LivePlayerScoreDto>>("ScoresUpdated", scores => OnScoresUpdated?.Invoke(scores));

        await _conn.StartAsync();
    }

    public async Task<RankedLobbyState> QuickMatchRankedAsync()
        => await _conn!.InvokeAsync<RankedLobbyState>("QuickMatchRanked");

    public async Task<RankedLobbyState> GetRankedLobbyStateAsync(string lobbyCode)
        => await _conn!.InvokeAsync<RankedLobbyState>("GetRankedLobbyState", lobbyCode);

    public async Task<TriviaQuestion?> GetCurrentRankedQuestionAsync(string lobbyCode)
        => await _conn!.InvokeAsync<TriviaQuestion?>("GetCurrentRankedQuestion", lobbyCode);

    public async Task AnswerRankedQuestionAsync(string lobbyCode, string answer)
        => await _conn!.InvokeAsync("AnswerRankedQuestion", lobbyCode, answer);

    public async Task<List<LivePlayerScoreDto>> GetLiveScoresAsync(string lobbyCode)
        => await _conn!.InvokeAsync<List<LivePlayerScoreDto>>("GetLiveScores", lobbyCode);
}