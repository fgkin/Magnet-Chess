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

    private const string ReadyKey = "Ready";

    [SerializeField] private MainMenuNavigation menuNavigation;

    public event Action OnLobbyUpdated;

    public bool IsInLobby => currentLobby != null;
    public bool IsLobbyHost => MultiplayerSessionData.IsLobbyHost;
    public string CurrentLobbyName => currentLobby != null ? currentLobby.Name : "-";
    public int CurrentPlayerCount => currentLobby != null ? currentLobby.Players.Count : 0;
    public int MaxLobbyPlayers => currentLobby != null ? currentLobby.MaxPlayers : maxPlayers;

    public int ReadyPlayerCount { get; private set; }
    public bool IsLocalPlayerReady { get; private set; }

    public bool CanHostStartGame =>
        IsLobbyHost &&
        CurrentPlayerCount >= 2 &&
        ReadyPlayerCount == CurrentPlayerCount &&
        AreAllLobbyPlayersNetworkConnected;

    public int NetworkConnectedPlayerCount =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening &&
        NetworkManager.Singleton.IsHost
            ? NetworkManager.Singleton.ConnectedClientsIds.Count
            : 0;

    public bool AreAllLobbyPlayersNetworkConnected =>
        IsLobbyHost &&
        NetworkConnectedPlayerCount >= CurrentPlayerCount;


    //Start    
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
        await CleanupBeforeNewSession();

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
                Player = CreateLobbyPlayer(false),
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

            MultiplayerSessionData.SetLobby(currentLobby.Id, true);
            SetLobbyCode(currentLobby.LobbyCode);
            SetPlayerCount(currentLobby.Players.Count);
            SetStatus("Lobi oluşturuldu. Kodunuzu paylaşabilirsiniz.");

            NetworkManager.Singleton.StartHost();

            RefreshReadyData();
            OnLobbyUpdated?.Invoke();

            if (menuNavigation != null)
                menuNavigation.OpenLobbyRoom();
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
            SetStatus("Lobi kodu giriniz.");
            return;
        }

        await CleanupBeforeNewSession();

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
                SetStatus("Lobi kodu giriniz.");
                return;
            }

            SetStatus("Joining Lobby...");

            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            MultiplayerSessionData.SetLobby(currentLobby.Id, false);

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

    private async Task CleanupBeforeNewSession()
    {
        SetStatus("Cleaning previous session...");

        if (currentLobby != null || MultiplayerSessionData.InLobby)
        {
            await LeaveCurrentLobby();
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            await Task.Delay(500);
        }

        hasCreatedOrJoinedLobby = false;
    }

    private async Task LeaveCurrentLobby()
    {
        try
        {
            await MultiplayerSessionData.LeaveLobbyIfNeeded();

            currentLobby = null;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
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
            MultiplayerSessionData.Clear();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
            }

            hasCreatedOrJoinedLobby = false;
            SetLobbyCode("-");
            SetPlayerCount(0);
        }
    }

    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            SetStatus("Sadece lobi sahibi oyunu başlatabilir.");
            return;
        }

        if (!CanHostStartGame)
        {
            SetStatus("Tüm oyuncular hazır değil.");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            gameplaySceneName,
            LoadSceneMode.Single
        );
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
            RefreshReadyData();
            OnLobbyUpdated?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Lobby poll failed: " + e.Message);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("Client connected: " + clientId);
        OnLobbyUpdated?.Invoke();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("Client disconnected: " + clientId);
        OnLobbyUpdated?.Invoke();
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
            playerCountText.text = $"Oyuncular: {count}/{maxPlayers}";
    }


    //Multiplayer Lobby Thingy
    public async void JoinLobbyById(string lobbyId)
    {
        await CleanupBeforeNewSession();

        hasCreatedOrJoinedLobby = true;

        try
        {
            SetStatus("Joining Lobby...");

            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            MultiplayerSessionData.SetLobby(currentLobby.Id, false);

            string relayJoinCode = currentLobby.Data[RelayJoinCodeKey].Value;

            SetStatus("Joining Relay...");

            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            UnityTransport transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

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
            RefreshReadyData();
            OnLobbyUpdated?.Invoke();

            if (menuNavigation != null)
                menuNavigation.OpenLobbyRoom();
        }
        catch (System.Exception e)
        {
            hasCreatedOrJoinedLobby = false;
            Debug.LogError(e);
            SetStatus("Join failed: " + e.Message);
        }
    }


    //More Multiplayer Lobby Thingy
    public async System.Threading.Tasks.Task EnsureServicesReady()
    {
        await InitializeUnityServices();
    }

    //Lobby Shenanigans
    private Player CreateLobbyPlayer(bool isReady)
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {
                    ReadyKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        isReady ? "1" : "0"
                    )
                }
            }
        };
    }

    private void RefreshReadyData()
    {
        ReadyPlayerCount = 0;
        IsLocalPlayerReady = false;

        if (currentLobby == null || currentLobby.Players == null)
            return;

        string localPlayerId = AuthenticationService.Instance.PlayerId;

        foreach (Player player in currentLobby.Players)
        {
            bool isReady = false;

            if (player.Data != null &&
                player.Data.TryGetValue(ReadyKey, out PlayerDataObject readyData))
            {
                isReady = readyData.Value == "1";
            }

            if (isReady)
                ReadyPlayerCount++;

            if (player.Id == localPlayerId)
                IsLocalPlayerReady = isReady;
        }
    }

    public async void ToggleReady()
    {
        if (currentLobby == null)
            return;

        bool newReadyState = !IsLocalPlayerReady;

        try
        {
            await LobbyService.Instance.UpdatePlayerAsync(
                currentLobby.Id,
                AuthenticationService.Instance.PlayerId,
                new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {
                            ReadyKey,
                            new PlayerDataObject(
                                PlayerDataObject.VisibilityOptions.Member,
                                newReadyState ? "1" : "0"
                            )
                        }
                    }
                }
            );

            await RefreshLobbyNow();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            SetStatus("Hazır durumu güncellenemedi: " + e.Message);
        }
    }

    private async Task RefreshLobbyNow()
    {
        if (currentLobby == null)
            return;

        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

        SetPlayerCount(currentLobby.Players.Count);
        RefreshReadyData();
        OnLobbyUpdated?.Invoke();
    }

    public async void LeaveLobbyAndReturnToBrowser()
    {
        await LeaveCurrentLobby();

        if (menuNavigation != null)
            menuNavigation.OpenLobbyBrowser();
    }
}