using Microsoft.AspNetCore.SignalR;
using QuizAPI.Models;
using QuizAPI.Services;

namespace QuizAPI;

public class MultiplayerHub : Hub
{
    private readonly MultiplayerManager _lobbies;

    public MultiplayerHub(MultiplayerManager lobbies)
    {
        _lobbies = lobbies;
    }

    public async Task<LobbyState> CreateLobby(CreateLobbyRequest req)
    {
        var lobby = _lobbies.CreateLobby(Context.ConnectionId, req.HostUsername, req.Settings);

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);
        var state = MultiplayerManager.ToState(lobby);

        await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        return state;
    }

    public async Task<LobbyState> JoinLobby(JoinLobbyRequest req)
    {
        var (ok, error) = _lobbies.JoinLobby(req.LobbyCode, Context.ConnectionId, req.Username);
        if (!ok) throw new HubException(error);

        _lobbies.TryGetLobby(req.LobbyCode, out var lobby);
        if (lobby == null) throw new HubException("Lobby not found");

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

        var state = MultiplayerManager.ToState(lobby);
        await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        return state;
    }

    public async Task<LobbyState> QuickMatch(QuickMatchRequest req)
    {
        var lobby = _lobbies.QuickMatch(Context.ConnectionId, req.Username, req.Preferences, req.MinPlayers, req.MaxPlayers);

        await Groups.AddToGroupAsync(Context.ConnectionId, lobby.GroupName);

        var state = MultiplayerManager.ToState(lobby);
        await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        
        return state;
    }

    public async Task UpdateSettings(string lobbyCode, LobbySettings settings)
    {
        _lobbies.UpdateSettings(lobbyCode, Context.ConnectionId, settings);

        if (_lobbies.TryGetLobby(lobbyCode, out var lobby) && lobby != null)
        {
            var state = MultiplayerManager.ToState(lobby);
            await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
        }
    }

    public async Task StartGame(string lobbyCode)
    {
        _lobbies.StartGame(lobbyCode, Context.ConnectionId);

        if (_lobbies.TryGetLobby(lobbyCode, out var lobby) && lobby != null)
        {
            var state = MultiplayerManager.ToState(lobby);
            await Clients.Group(lobby.GroupName).SendCoreAsync("LobbyUpdated", new object?[] { state });
            await Clients.Group(lobby.GroupName).SendCoreAsync("GameStarted", new object?[] { state });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _lobbies.LeaveByConnection(Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }
}