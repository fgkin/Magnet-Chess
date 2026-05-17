using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using UnityEngine;

public static class MultiplayerSessionData
{
    public static string CurrentLobbyId;
    public static bool InLobby;
    public static bool IsLobbyHost;

    public static void SetLobby(string lobbyId, bool isHost)
    {
        CurrentLobbyId = lobbyId;
        InLobby = true;
        IsLobbyHost = isHost;
    }

    public static async Task LeaveLobbyIfNeeded()
    {
        if (!InLobby || string.IsNullOrEmpty(CurrentLobbyId))
        {
            Clear();
            return;
        }

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;

            if (IsLobbyHost)
            {
                await LobbyService.Instance.DeleteLobbyAsync(CurrentLobbyId);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobbyId, playerId);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Lobby cleanup failed: " + e.Message);
        }

        Clear();
    }

    public static void Clear()
    {
        CurrentLobbyId = null;
        InLobby = false;
        IsLobbyHost = false;
    }
}