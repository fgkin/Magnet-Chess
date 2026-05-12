using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerLobbyManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private TextMeshProUGUI playerCountText;

    [Header("Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string gameplaySceneName = "Main";

    private Lobby currentLobby;
    private float heartbeatTimer;
    private float pollTimer;
    private bool hasCreatedOrJoinedLobby;

    private const string RelayJoinCodeKey = "RelayJoinCode";

    private async void Start()
    {
        await InitializeUnityServices();

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        SetStatus("Ready.");
        SetLobbyCode("-");
        SetPlayerCount(0);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPolling();
    }

    private async Task InitializeUnityServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async void CreateLobby()
    {
        if (hasCreatedOrJoinedLobby)
        {
            SetStatus("Switching lobby...");
            await LeaveCurrentLobby();
        }

        hasCreatedOrJoinedLobby = true;

        try
        {
            SetStatus("Creating Relay...");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            SetStatus("Creating Lobby...");

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        RelayJoinCodeKey,
                        new DataObject(
                            DataObject.VisibilityOptions.Member,
                            relayJoinCode
                        )
                    }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                "Magnet Lobby",
                maxPlayers,
                options
            );

            SetLobbyCode(currentLobby.LobbyCode);
            SetPlayerCount(currentLobby.Players.Count);
            SetStatus("Lobby created. Share the code with other players.");

            NetworkManager.Singleton.StartHost();
        }
        catch (Exception e)
        {
            hasCreatedOrJoinedLobby = false;
            Debug.LogError(e);
            SetStatus("Create failed: " + e.Message);
        }
    }

    public async void JoinLobby()
    {
        string lobbyCode = joinCodeInput != null ? joinCodeInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            SetStatus("Enter a lobby code.");
            return;
        }

        if (hasCreatedOrJoinedLobby)
        {
            SetStatus("Switching lobby...");
            await LeaveCurrentLobby();
        }

        hasCreatedOrJoinedLobby = true;

        try
        {

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                SetStatus("Enter a lobby code.");
                return;
            }

            SetStatus("Joining Lobby...");

            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

            string relayJoinCode = currentLobby.Data[RelayJoinCodeKey].Value;

            SetStatus("Joining Relay...");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            SetLobbyCode(currentLobby.LobbyCode);
            SetPlayerCount(currentLobby.Players.Count);
            SetStatus("Joined lobby.");

            NetworkManager.Singleton.StartClient();
        }

        catch (Exception e)
        {
            hasCreatedOrJoinedLobby = false;
            Debug.LogError(e);
            SetStatus("Join failed: " + e.Message);
        }
    }

    private async Task LeaveCurrentLobby()
    {
        try
        {
            Lobby lobbyToLeave = currentLobby;
            currentLobby = null;

            if (lobbyToLeave != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyToLeave.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(lobbyToLeave.Id, playerId);
                }
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            hasCreatedOrJoinedLobby = false;
            heartbeatTimer = 0f;
            pollTimer = 0f;

            SetLobbyCode("-");
            SetPlayerCount(0);
        }
        
        catch (Exception e)
        {
            Debug.LogWarning("Leave lobby failed: " + e.Message);

            currentLobby = null;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            hasCreatedOrJoinedLobby = false;
            heartbeatTimer = 0f;
            pollTimer = 0f;

            SetLobbyCode("-");
            SetPlayerCount(0);
        }
    }

    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            SetStatus("Only host can start.");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    private async void HandleLobbyHeartbeat()
    {
        if (currentLobby == null)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsHost)
            return;

        heartbeatTimer += Time.deltaTime;

        if (heartbeatTimer < 15f)
            return;

        heartbeatTimer = 0f;

        string lobbyId = currentLobby.Id;

        if (string.IsNullOrEmpty(lobbyId))
            return;

        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Lobby heartbeat failed: " + e.Message);
        }
    }

    private async void HandleLobbyPolling()
    {
        if (currentLobby == null)
            return;

        pollTimer += Time.deltaTime;

        if (pollTimer < 2f)
            return;

        pollTimer = 0f;

        string lobbyId = currentLobby.Id;

        if (string.IsNullOrEmpty(lobbyId))
            return;

        try
        {
            Lobby updatedLobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);

            if (updatedLobby == null)
                return;

            currentLobby = updatedLobby;
            SetPlayerCount(currentLobby.Players.Count);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Lobby poll failed: " + e.Message);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("Client connected: " + clientId);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            SetStatus("Lobby active. Waiting for players...");
        }
        else
        {
            SetStatus("Connected to host.");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        SetStatus("Client disconnected: " + clientId);
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
            statusText.text = message;
    }

    private void SetLobbyCode(string code)
    {
        if (lobbyCodeText != null)
            lobbyCodeText.text = "Code: " + code;
    }

    private void SetPlayerCount(int count)
    {
        if (playerCountText != null)
            playerCountText.text = $"Players: {count}/{maxPlayers}";
    }
}