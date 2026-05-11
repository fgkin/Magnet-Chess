using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
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

    public TurnOwner CurrentTurn => currentTurn;
    public TurnState CurrentState => currentState;
    public float CurrentTurnTimeRemaining => currentTurnTimeRemaining;
    public int ActivePlayerCount => activePlayerCount;

    private void Start()
    {
        activePlayerCount = Mathf.Clamp(activePlayerCount, 2, 4);
        
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

        Debug.Log("Game started.");
    }

    private void OnDestroy()
    {
        if (piecePlacement != null)
            piecePlacement.OnPiecePlacedSuccessfully -= HandlePiecePlaced;
    }

    private void Update()
    {
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
            piece.Initialize(owner);

            targetList.Add(piece);
            reserveLayout.RegisterPiece(piece);
        }
    }

    private void BeginPlayerTurn()
    {
        if (IsGameOver())
        {
            currentState = TurnState.GameOver;
            Debug.Log(GetWinnerMessage());

            if (gameAudio != null)
                gameAudio.PlayVictory();

            return;
        }

        currentState = TurnState.WaitingForPlayerInput;
        currentTurnTimeRemaining = turnDuration;

        Debug.Log($"Turn started: {currentTurn}");
    }

    private void UpdateTurnTimer()
    {
        if (currentState != TurnState.WaitingForPlayerInput)
            return;

        currentTurnTimeRemaining -= Time.deltaTime;

        if (currentTurnTimeRemaining <= 0f)
        {
            currentTurnTimeRemaining = 0f;
            HandleTurnTimeout();
        }
    }

    private void HandleTurnTimeout()
    {
        if (currentState != TurnState.WaitingForPlayerInput)
            return;

        Debug.Log($"{currentTurn} ran out of time!");

        currentState = TurnState.ResolvingTurn;

        if (gameAudio != null)
            gameAudio.PlayTimeout();

        if (piecePlacement != null)
            piecePlacement.CancelCurrentDrag();

        RefreshAllReserveLayouts();
        FinishTurnResolution();
    }

    private void HandlePiecePlaced(MagnetPiece placedPiece)
    {
        currentState = TurnState.ResolvingTurn;

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
        int currentIndex = TurnOwnerToIndex(currentTurn);
        int nextIndex = (currentIndex + 1) % activePlayerCount;
        currentTurn = IndexToTurnOwner(nextIndex);
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
        for (int i = 0; i < activePlayerCount; i++)
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
        return currentState == TurnState.WaitingForPlayerInput;
    }

    public bool CanCurrentPlayerDrag(MagnetPiece piece)
    {
        if (!CanPlayerInteract())
            return false;

        if (piece == null)
            return false;

        if (piece.PieceState != MagnetPiece.State.Reserve)
            return false;

        MagnetPiece.Owner currentOwner = TurnOwnerToPieceOwner(currentTurn);

        return piece.PieceOwner == currentOwner;
    }

    public int GetReserveCountForPlayer(int playerNumber)
    {
        int index = playerNumber - 1;

        if (index < 0 || index >= activePlayerCount)
            return 0;

        ReserveLayout layout = GetReserveLayoutByIndex(index);

        return layout != null ? layout.GetReserveCount() : 0;
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
}