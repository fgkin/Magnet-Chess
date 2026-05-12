using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public enum TurnOwner
    {
        Player1,
        Player2,
        Player3,
        Player4
    }

    public enum TurnState
    {
        WaitingForPlayerInput,
        ResolvingTurn,
        GameOver
    }

    private readonly NetworkVariable<bool> networkIsPaused = new(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    private bool isEndingOnlineGame;
    private readonly NetworkVariable<int> networkCurrentTurnIndex = new(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> networkTurnState = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> networkTurnTimeRemaining = new(
        10f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> networkActivePlayerCount = new(
        2,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("References")]
    [SerializeField] private PiecePlacement piecePlacement;
    [SerializeField] private MagnetPiece magnetPrefab;
    [SerializeField] private ArenaEllipseBounds arenaBounds;

    [Header("Reserve Layouts")]
    [SerializeField] private ReserveLayout player1ReserveLayout;
    [SerializeField] private ReserveLayout player2ReserveLayout;
    [SerializeField] private ReserveLayout player3ReserveLayout;
    [SerializeField] private ReserveLayout player4ReserveLayout;

    [Header("Systems")]
    [SerializeField] private MagnetSystem magnetSystem;
    [SerializeField] private GameAudio gameAudio;
    [SerializeField] private CameraShake cameraShake;

    [Header("Turn Timer")]
    [SerializeField] private float turnDuration = 10f;
    [SerializeField] private float currentTurnTimeRemaining;

    [Header("Setup")]
    [Range(2, 4)]
    [SerializeField] private int activePlayerCount = 2;

    [SerializeField] private int magnetsPerPlayer = 6;

    [Header("Arena Scaling")]
    [SerializeField] private Transform visibleArena;
    [SerializeField] private Vector2 arenaRadiiFor2Players = new Vector2(4f, 3f);
    [SerializeField] private Vector2 arenaRadiiFor3Players = new Vector2(4.8f, 3.5f);
    [SerializeField] private Vector2 arenaRadiiFor4Players = new Vector2(5.5f, 4f);
    [SerializeField] private Vector3 visibleArenaBaseScale = Vector3.one;
    [SerializeField] private bool scaleVisibleArena = true;

    [Header("Debug")]
    [SerializeField] private TurnOwner currentTurn = TurnOwner.Player1;
    [SerializeField] private TurnState currentState = TurnState.WaitingForPlayerInput;

    private readonly List<MagnetPiece> player1Pieces = new();
    private readonly List<MagnetPiece> player2Pieces = new();
    private readonly List<MagnetPiece> player3Pieces = new();
    private readonly List<MagnetPiece> player4Pieces = new();

    public TurnOwner CurrentTurn =>
    IsOnlineGame()
        ? IndexToTurnOwner(networkCurrentTurnIndex.Value)
        : currentTurn;

    public TurnState CurrentState =>
        IsOnlineGame()
            ? (TurnState)networkTurnState.Value
            : currentState;

    public float CurrentTurnTimeRemaining =>
        IsOnlineGame()
            ? networkTurnTimeRemaining.Value
            : currentTurnTimeRemaining;

    public int ActivePlayerCount =>
        IsOnlineGame()
            ? networkActivePlayerCount.Value
            : activePlayerCount;

    public bool IsOnlineGame()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private System.Collections.IEnumerator RegisterPiecesOnClientAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        RegisterExistingNetworkPiecesToLayouts();
    }

   private bool hasInitialized;

    private void Start()
    {
        if (IsOnlineGame())
            return;

        InitializeGameLocal();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOnlineGame())
            return;

        if (NetworkManager.Singleton != null)
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        if (piecePlacement != null)
            piecePlacement.SetGameManager(this);

        if (magnetSystem != null)
            magnetSystem.Initialize(this);

        if (IsServer)
        {
            StartCoroutine(InitializeOnlineServerAfterDelay());
        }
        else
        {
            StartCoroutine(RegisterPiecesOnClientAfterDelay());
        }
    }

    private System.Collections.IEnumerator InitializeOnlineServerAfterDelay()
    {
        yield return null;
        yield return new WaitForSeconds(1f);

        InitializeGameOnlineServer();
    }

    public override void OnDestroy()
    {
        if (piecePlacement != null)
            piecePlacement.OnPiecePlacedSuccessfully -= HandlePiecePlaced;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

        base.OnDestroy();
    }

    private void Update()
    {
        if (IsOnlineGame() && !IsServer)
            return;

        UpdateTurnTimer();
    }

    private void CreateAllPieces()
    {
        SpawnReservePieces(MagnetPiece.Owner.Player1, player1Pieces, player1ReserveLayout);

        if (activePlayerCount >= 2)
            SpawnReservePieces(MagnetPiece.Owner.Player2, player2Pieces, player2ReserveLayout);

        if (activePlayerCount >= 3)
            SpawnReservePieces(MagnetPiece.Owner.Player3, player3Pieces, player3ReserveLayout);

        if (activePlayerCount >= 4)
            SpawnReservePieces(MagnetPiece.Owner.Player4, player4Pieces, player4ReserveLayout);
    }

    private void SpawnReservePieces(
    MagnetPiece.Owner owner,
    List<MagnetPiece> targetList,
    ReserveLayout reserveLayout)
    {
        if (reserveLayout == null)
        {
            Debug.LogWarning($"Missing reserve layout for {owner}");
            return;
        }

        for (int i = 0; i < magnetsPerPlayer; i++)
        {
            MagnetPiece piece = Instantiate(magnetPrefab, Vector3.zero, Quaternion.identity);

            if (IsOnlineGame())
            {
                if (!IsServer)
                {
                    Destroy(piece.gameObject);
                    return;
                }

                NetworkObject networkObject = piece.GetComponent<NetworkObject>();

                if (networkObject == null)
                {
                    Debug.LogError("Magnet prefab is missing NetworkObject.");
                    Destroy(piece.gameObject);
                    return;
                }

                piece.Initialize(owner);

                Debug.Log($"SERVER SPAWNING {owner} MAGNET");

                networkObject.Spawn(true);

                Debug.Log($"SPAWNED {owner} MAGNET ID: {networkObject.NetworkObjectId}");
            }
            else
            {
                piece.Initialize(owner);
            }

            targetList.Add(piece);
            reserveLayout.RegisterPiece(piece);
        }
    }

    private void BeginPlayerTurn()
    {
        if (IsGameOver())
        {
            SetTurnState(TurnState.GameOver);
            Debug.Log(GetWinnerMessage());

            if (gameAudio != null)
                gameAudio.PlayVictory();

            return;
        }

        SetTurnState(TurnState.WaitingForPlayerInput);
        SetTurnTime(turnDuration);

        Debug.Log($"Turn started: {CurrentTurn}");
    }

    private void SetTurnState(TurnState newState)
    {
        currentState = newState;

        if (IsOnlineGame() && IsServer)
            networkTurnState.Value = (int)newState;
    }

    private void SetTurnTime(float time)
    {
        currentTurnTimeRemaining = time;

        if (IsOnlineGame() && IsServer)
            networkTurnTimeRemaining.Value = time;
    }

    private void SetCurrentTurn(TurnOwner newTurn)
    {
        currentTurn = newTurn;

        if (IsOnlineGame() && IsServer)
            networkCurrentTurnIndex.Value = TurnOwnerToIndex(newTurn);
    }

    private void UpdateTurnTimer()
    {
        if (IsGamePaused)
        return;

        if (CurrentState != TurnState.WaitingForPlayerInput)
            return;

        float newTime = CurrentTurnTimeRemaining - Time.deltaTime;
        newTime = Mathf.Max(0f, newTime);

        SetTurnTime(newTime);

        if (newTime <= 0f)
            HandleTurnTimeout();
    }

    private void HandleTurnTimeout()
    {
        if (currentState != TurnState.WaitingForPlayerInput)
            return;

        Debug.Log($"{currentTurn} ran out of time!");

        SetTurnState(TurnState.ResolvingTurn);

        if (gameAudio != null)
            gameAudio.PlayTimeout();

        if (piecePlacement != null)
            piecePlacement.CancelCurrentDrag();

        RefreshAllReserveLayouts();
        FinishTurnResolution();
    }

    private void HandlePiecePlaced(MagnetPiece placedPiece)
    {
        SetTurnState(TurnState.ResolvingTurn);

        RefreshReserveLayoutForPiece(placedPiece);

        if (magnetSystem != null)
        {
            magnetSystem.EvaluatePlacement(placedPiece);
        }
        else
        {
            FinishTurnResolution();
        }
    }

    private void FinishTurnResolution()
    {
        if (IsGameOver())
        {
            currentState = TurnState.GameOver;
            Debug.Log(GetWinnerMessage());

            if (gameAudio != null)
                gameAudio.PlayVictory();

            return;
        }

        SwitchTurn();
        BeginPlayerTurn();
    }

    private void SwitchTurn()
    {
        int currentIndex = TurnOwnerToIndex(CurrentTurn);
        int nextIndex = (currentIndex + 1) % ActivePlayerCount;
        SetCurrentTurn(IndexToTurnOwner(nextIndex));
    }

    private int TurnOwnerToIndex(TurnOwner owner)
    {
        return owner switch
        {
            TurnOwner.Player1 => 0,
            TurnOwner.Player2 => 1,
            TurnOwner.Player3 => 2,
            TurnOwner.Player4 => 3,
            _ => 0
        };
    }

    private TurnOwner IndexToTurnOwner(int index)
    {
        return index switch
        {
            0 => TurnOwner.Player1,
            1 => TurnOwner.Player2,
            2 => TurnOwner.Player3,
            3 => TurnOwner.Player4,
            _ => TurnOwner.Player1
        };
    }

    private MagnetPiece.Owner TurnOwnerToPieceOwner(TurnOwner owner)
    {
        return owner switch
        {
            TurnOwner.Player1 => MagnetPiece.Owner.Player1,
            TurnOwner.Player2 => MagnetPiece.Owner.Player2,
            TurnOwner.Player3 => MagnetPiece.Owner.Player3,
            TurnOwner.Player4 => MagnetPiece.Owner.Player4,
            _ => MagnetPiece.Owner.Player1
        };
    }

    private int PieceOwnerToIndex(MagnetPiece.Owner owner)
    {
        return owner switch
        {
            MagnetPiece.Owner.Player1 => 0,
            MagnetPiece.Owner.Player2 => 1,
            MagnetPiece.Owner.Player3 => 2,
            MagnetPiece.Owner.Player4 => 3,
            _ => 0
        };
    }

    private ReserveLayout GetReserveLayoutByIndex(int index)
    {
        return index switch
        {
            0 => player1ReserveLayout,
            1 => player2ReserveLayout,
            2 => player3ReserveLayout,
            3 => player4ReserveLayout,
            _ => null
        };
    }

    private ReserveLayout GetReserveLayoutByOwner(MagnetPiece.Owner owner)
    {
        return GetReserveLayoutByIndex(PieceOwnerToIndex(owner));
    }

    private ReserveLayout GetCurrentTurnReserveLayout()
    {
        return GetReserveLayoutByIndex(TurnOwnerToIndex(currentTurn));
    }

    private void RefreshReserveLayoutForPiece(MagnetPiece piece)
    {
        if (piece == null)
            return;

        ReserveLayout layout = GetReserveLayoutByOwner(piece.PieceOwner);

        if (layout != null)
            layout.RefreshLayout();
    }

    private void RefreshAllReserveLayouts()
    {
        for (int i = 0; i < ActivePlayerCount; i++)
        {
            ReserveLayout layout = GetReserveLayoutByIndex(i);

            if (layout != null)
                layout.RefreshLayout();
        }
    }

    public void RefreshReserveLayouts()
    {
        RefreshAllReserveLayouts();
    }

    public bool CanPlayerInteract()
    {
        if (IsGamePaused)
            return false;

        return CurrentState == TurnState.WaitingForPlayerInput;
    }

    public bool CanCurrentPlayerDrag(MagnetPiece piece)
    {
        if (!CanPlayerInteract())
            return false;

        if (piece == null)
            return false;

        if (piece.PieceState != MagnetPiece.State.Reserve)
            return false;

        MagnetPiece.Owner currentOwner = TurnOwnerToPieceOwner(CurrentTurn);

        if (piece.PieceOwner != currentOwner)
            return false;

        if (IsOnlineGame())
        {
            MagnetPiece.Owner localOwner = GetLocalNetworkPlayerOwner();

            if (piece.PieceOwner != localOwner)
                return false;
        }

        return true;
    }

    private MagnetPiece.Owner GetLocalNetworkPlayerOwner()
    {
        if (!IsOnlineGame())
            return TurnOwnerToPieceOwner(CurrentTurn);

        int playerIndex = Mathf.Clamp((int)NetworkManager.Singleton.LocalClientId, 0, 3);
        return (MagnetPiece.Owner)playerIndex;
    }

    public int GetReserveCountForPlayer(int playerNumber)
    {
        int index = playerNumber - 1;

        if (index < 0 || index >= ActivePlayerCount)
            return 0;

        MagnetPiece.Owner owner = (MagnetPiece.Owner)index;
        int count = 0;

        MagnetPiece[] allPieces = FindObjectsByType<MagnetPiece>(FindObjectsSortMode.None);

        foreach (MagnetPiece piece in allPieces)
        {
            if (piece != null &&
                piece.PieceOwner == owner &&
                piece.PieceState == MagnetPiece.State.Reserve)
            {
                count++;
            }
        }

        return count;
    }

    public int GetPlayer1ReserveCount()
    {
        return GetReserveCountForPlayer(1);
    }

    public int GetPlayer2ReserveCount()
    {
        return GetReserveCountForPlayer(2);
    }

    public int GetPlayer3ReserveCount()
    {
        return GetReserveCountForPlayer(3);
    }

    public int GetPlayer4ReserveCount()
    {
        return GetReserveCountForPlayer(4);
    }

    private bool IsGameOver()
    {
        for (int i = 0; i < activePlayerCount; i++)
        {
            if (GetReserveCountForPlayer(i + 1) == 0)
                return true;
        }

        return false;
    }

    private string GetWinnerMessage()
    {
        List<string> winners = new();

        for (int i = 0; i < activePlayerCount; i++)
        {
            if (GetReserveCountForPlayer(i + 1) == 0)
                winners.Add($"Player {i + 1}");
        }

        if (winners.Count == 0)
            return "Game still running.";

        if (winners.Count > 1)
            return "Draw!";

        return $"{winners[0]} wins!";
    }

    public string GetWinnerUILabel()
    {
        List<string> winners = new();

        for (int i = 0; i < activePlayerCount; i++)
        {
            if (GetReserveCountForPlayer(i + 1) == 0)
                winners.Add($"Player {i + 1}");
        }

        if (winners.Count == 0)
            return "";

        if (winners.Count > 1)
            return "Draw!";

        return $"{winners[0]} Wins!";
    }

    private void ApplyArenaSizeForPlayerCount()
    {
        Vector2 selectedRadii = GetArenaRadiiForPlayerCount(activePlayerCount);

        if (arenaBounds != null)
            arenaBounds.SetRadii(selectedRadii.x, selectedRadii.y);

        if (scaleVisibleArena && visibleArena != null)
            ScaleVisibleArena(selectedRadii);
    }

    private Vector2 GetArenaRadiiForPlayerCount(int playerCount)
    {
        return playerCount switch
        {
            2 => arenaRadiiFor2Players,
            3 => arenaRadiiFor3Players,
            4 => arenaRadiiFor4Players,
            _ => arenaRadiiFor2Players
        };
    }

    private void ScaleVisibleArena(Vector2 selectedRadii)
    {
        Vector2 baseRadii = arenaRadiiFor2Players;

        if (baseRadii.x <= 0f || baseRadii.y <= 0f)
            return;

        float scaleX = selectedRadii.x / baseRadii.x;
        float scaleZ = selectedRadii.y / baseRadii.y;

        visibleArena.localScale = new Vector3(
            visibleArenaBaseScale.x * scaleX,
            visibleArenaBaseScale.y,
            visibleArenaBaseScale.z * scaleZ
        );
    }

    public void CollectCluster(List<MagnetPiece> cluster)
    {
        if (cluster == null || cluster.Count == 0)
        {
            FinishTurnResolution();
            return;
        }

        if (gameAudio != null)
            gameAudio.PlayMagnetSnap();

        if (cameraShake != null)
            cameraShake.PlayShake();

        HapticsHelper.LightImpact();

        Debug.Log("Collecting cluster for current turn player...");

        MagnetPiece.Owner collector = TurnOwnerToPieceOwner(currentTurn);
        ReserveLayout targetReserve = GetCurrentTurnReserveLayout();

        if (targetReserve == null)
        {
            Debug.LogWarning("No reserve layout found for current turn player.");
            FinishTurnResolution();
            return;
        }

        foreach (var piece in cluster)
        {
            if (piece == null)
                continue;

            for (int i = 0; i < activePlayerCount; i++)
            {
                ReserveLayout layout = GetReserveLayoutByIndex(i);

                if (layout != null)
                    layout.UnregisterPiece(piece);
            }

            piece.SetOwner(collector);
            piece.SetState(MagnetPiece.State.Reserve);
            piece.SetPhysicsEnabled(false);
            piece.SnapUpright();
            piece.gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");
            piece.ResetVisual();

            targetReserve.RegisterPiece(piece);
        }

        RefreshAllReserveLayouts();
        FinishTurnResolution();
    }

    public void FinishTurnAfterMagnetResolution()
    {
        FinishTurnResolution();
    }

    private void InitializeGameLocal()
    {
        if (hasInitialized)
            return;

        hasInitialized = true;

        activePlayerCount = 2;

        ApplyArenaSizeForPlayerCount();

        CreateAllPieces();

        if (piecePlacement != null)
        {
            piecePlacement.OnPiecePlacedSuccessfully += HandlePiecePlaced;
            piecePlacement.SetGameManager(this);
        }

        if (magnetSystem != null)
            magnetSystem.Initialize(this);

        RefreshAllReserveLayouts();
        BeginPlayerTurn();

        Debug.Log("Local game started.");
    }

    private void InitializeGameOnlineServer()
    {
        if (hasInitialized)
            return;

        Debug.Log("ONLINE SERVER INITIALIZE STARTED");
        hasInitialized = true;

        int connectedPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count;
        activePlayerCount = Mathf.Clamp(connectedPlayers, 2, 4);
        networkActivePlayerCount.Value = activePlayerCount;

        networkCurrentTurnIndex.Value = 0;
        networkTurnState.Value = (int)TurnState.WaitingForPlayerInput;
        networkTurnTimeRemaining.Value = turnDuration;
        networkIsPaused.Value = false;

        ApplyArenaSizeForPlayerCount();

        CreateAllPieces();

        if (piecePlacement != null)
        {
            piecePlacement.OnPiecePlacedSuccessfully += HandlePiecePlaced;
            piecePlacement.SetGameManager(this);
        }

        if (magnetSystem != null)
            magnetSystem.Initialize(this);

        RefreshAllReserveLayouts();
        BeginPlayerTurn();

        Debug.Log($"Online game started with {activePlayerCount} players.");
    }

    public void RequestPlacePiece(MagnetPiece piece, Vector3 position)
    {
        if (piece == null)
            return;

        if (!IsOnlineGame())
            return;

        NetworkObject networkObject = piece.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("Placed piece has no NetworkObject.");
            return;
        }

        SubmitPlacementServerRpc(networkObject, position);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitPlacementServerRpc(NetworkObjectReference pieceReference, Vector3 position, RpcParams rpcParams = default)
    {
        if (!pieceReference.TryGet(out NetworkObject pieceNetworkObject))
            return;

        MagnetPiece piece = pieceNetworkObject.GetComponent<MagnetPiece>();

        if (piece == null)
            return;

        int senderPlayerIndex = Mathf.Clamp((int)rpcParams.Receive.SenderClientId, 0, 3);
        MagnetPiece.Owner senderOwner = (MagnetPiece.Owner)senderPlayerIndex;
        MagnetPiece.Owner currentOwner = TurnOwnerToPieceOwner(CurrentTurn);

        if (senderOwner != currentOwner)
        {
            Debug.LogWarning($"Rejected placement. Sender {senderOwner}, current turn {currentOwner}");
            return;
        }

        if (piece.PieceOwner != senderOwner)
        {
            Debug.LogWarning("Rejected placement. Player tried to place someone else's piece.");
            return;
        }

        if (piece.PieceState != MagnetPiece.State.Reserve &&
            piece.PieceState != MagnetPiece.State.Dragging)
        {
            Debug.LogWarning("Rejected placement. Piece is not in reserve/dragging state.");
            return;
        }

        PlacePieceOnServer(piece, position);
    }

    private void PlacePieceOnServer(MagnetPiece piece, Vector3 position)
    {
       

        piece.transform.position = position;
        piece.SetPhysicsEnabled(true);
        piece.SetState(MagnetPiece.State.Placed);
        piece.gameObject.layer = LayerMask.NameToLayer("PlacedMagnet");
        piece.ResetVisual();

        if (gameAudio != null)
            gameAudio.PlayPlace();

        HandlePiecePlaced(piece);
    }

    private void RegisterExistingNetworkPiecesToLayouts()
    {
        MagnetPiece[] allPieces = FindObjectsByType<MagnetPiece>(FindObjectsSortMode.None);

        foreach (MagnetPiece piece in allPieces)
        {
            if (piece == null)
                continue;

            ReserveLayout layout = GetReserveLayoutByOwner(piece.PieceOwner);

            if (layout != null && piece.PieceState == MagnetPiece.State.Reserve)
                layout.RegisterPiece(piece);
        }

        RefreshAllReserveLayouts();
    }

    public bool IsGamePaused
    {
        get
        {
            if (IsOnlineGame())
                return networkIsPaused.Value;

            return Time.timeScale == 0f;
        }
    }

    public void RequestSetPause(bool paused)
    {
        if (!IsOnlineGame())
        {
            Time.timeScale = paused ? 0f : 1f;
            return;
        }

        if (IsServer)
        {
            SetPausedOnServer(paused);
        }
        else
        {
            SetPausedRpc(paused);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetPausedRpc(bool paused)
    {
        SetPausedOnServer(paused);
    }

    private void SetPausedOnServer(bool paused)
    {
        if (!IsServer)
            return;

        networkIsPaused.Value = paused;
    }

    public void RequestLeaveOrEndOnlineGame()
    {
        if (!IsOnlineGame())
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
            return;
        }

        if (IsServer)
        {
            EndOnlineGameForEveryone();
        }
        else
        {
            LeaveOnlineGameClientSide();
        }
    }

    private void LeaveOnlineGameClientSide()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void EndOnlineGameForEveryone()
    {
        if (isEndingOnlineGame)
            return;

        isEndingOnlineGame = true;

        ReturnToMainMenuRpc();

        StartCoroutine(ShutdownNetworkAfterDelay());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReturnToMainMenuRpc()
    {
        StartCoroutine(ReturnToMainMenuRoutine());
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        yield return null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private IEnumerator ShutdownNetworkAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.2f);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        if (isEndingOnlineGame)
            return;

        if (!IsOnlineGame())
            return;

        Debug.Log($"Client {clientId} disconnected. Ending online game for everyone.");

        EndOnlineGameForEveryone();
    }
}