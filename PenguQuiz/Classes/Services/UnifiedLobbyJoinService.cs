using WebQuizGame.Classes.Models;
using WebQuizGame.Classes.Models.Multiplayer;

namespace WebQuizGame.Classes.Services;

public class UnifiedLobbyJoinService
{
    private readonly MultiplayerClient _normal;
    private readonly CustomQuizMultiplayerClient _custom;

    public UnifiedLobbyJoinService(
        MultiplayerClient normal,
        CustomQuizMultiplayerClient custom)
    {
        _normal = normal;
        _custom = custom;
    }

    public async Task<bool> TryJoinAsync(string code, string username, string avatar)
    {
        try
        {
            await _custom.JoinLobbyAsync(new JoinCustomQuizMultiplayerLobbyRequest(
                code, username, avatar));
            return true;
        }
        catch
        {
            try
            {
                await _normal.JoinLobbyAsync(new JoinLobbyRequest(
                    code, username, avatar));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}