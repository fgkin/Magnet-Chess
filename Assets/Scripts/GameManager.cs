using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum TurnOwner
    {
        Player1,
        Player2
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
    [SerializeField] private ReserveLayout player1ReserveLayout;
    [SerializeField] private ReserveLayout player2ReserveLayout;
    [SerializeField] private MagnetSystem magnetSystem;
    [SerializeField] private GameAudio gameAudio;
    [SerializeField] private CameraShake cameraShake;

    [Header("Turn Timer")]
    [SerializeField] private float turnDuration = 10f;
    [SerializeField] private float currentTurnTimeRemaining;

    [Header("Setup")]
    [SerializeField] private int magnetsPerPlayer = 6;

    [Header("Debug")]
    [SerializeField] private TurnOwner currentTurn = TurnOwner.Player1;
    [SerializeField] private TurnState currentState = TurnState.WaitingForPlayerInput;
   

    private readonly List<MagnetPiece> player1Pieces = new();
    private readonly List<MagnetPiece> player2Pieces = new();

    public TurnOwner CurrentTurn => currentTurn;
    public TurnState CurrentState => currentState;

    private void Start()
    {
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

    private void CreateAllPieces()
    {
        SpawnReservePieces(MagnetPiece.Owner.Player1, player1Pieces, player1ReserveLayout);
        SpawnReservePieces(MagnetPiece.Owner.Player2, player2Pieces, player2ReserveLayout);
    }

    private void SpawnReservePieces(
        MagnetPiece.Owner owner,
        List<MagnetPiece> targetList,
        ReserveLayout reserveLayout)
    {
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
            return;
        }

        currentState = TurnState.WaitingForPlayerInput;
        currentTurnTimeRemaining = turnDuration;

        Debug.Log($"Turn started: {currentTurn}");
    }

    private void Update()
    {
        UpdateTurnTimer();
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

    public void RefreshReserveLayouts()
    {
        RefreshAllReserveLayouts();
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
        currentTurn = currentTurn == TurnOwner.Player1
            ? TurnOwner.Player2
            : TurnOwner.Player1;
    }

    private void RefreshReserveLayoutForPiece(MagnetPiece piece)
    {
        if (piece == null)
            return;

        if (piece.PieceOwner == MagnetPiece.Owner.Player1)
            player1ReserveLayout.RefreshLayout();
        else
            player2ReserveLayout.RefreshLayout();
    }

    private void RefreshAllReserveLayouts()
    {
        if (player1ReserveLayout != null)
            player1ReserveLayout.RefreshLayout();

        if (player2ReserveLayout != null)
            player2ReserveLayout.RefreshLayout();
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

        if (currentTurn == TurnOwner.Player1 && piece.PieceOwner != MagnetPiece.Owner.Player1)
            return false;

        if (currentTurn == TurnOwner.Player2 && piece.PieceOwner != MagnetPiece.Owner.Player2)
            return false;

        return true;
    }

    public int GetPlayer1ReserveCount()
    {
        return player1ReserveLayout != null ? player1ReserveLayout.GetReserveCount() : 0;
    }

    public int GetPlayer2ReserveCount()
    {
        return player2ReserveLayout != null ? player2ReserveLayout.GetReserveCount() : 0;
    }

    public float CurrentTurnTimeRemaining => currentTurnTimeRemaining;

    private bool IsGameOver()
    {
        return GetPlayer1ReserveCount() == 0 || GetPlayer2ReserveCount() == 0;
    }

    private string GetWinnerMessage()
    {
        bool p1Empty = GetPlayer1ReserveCount() == 0;
        bool p2Empty = GetPlayer2ReserveCount() == 0;

        if (p1Empty && p2Empty)
            return "Draw!";

        if (p1Empty)
            return "Player 1 wins!";

        if (p2Empty)
            return "Player 2 wins!";

        return "Game still running.";
    }

    public string GetWinnerUILabel()
    {
        bool p1Empty = GetPlayer1ReserveCount() == 0;
        bool p2Empty = GetPlayer2ReserveCount() == 0;

        if (p1Empty && p2Empty)
            return "Draw!";

        if (p1Empty)
            return "Player 1 Wins!";

        if (p2Empty)
            return "Player 2 Wins!";

        return "";
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

        MagnetPiece.Owner collector =
            currentTurn == TurnOwner.Player1
            ? MagnetPiece.Owner.Player1
            : MagnetPiece.Owner.Player2;

        ReserveLayout targetReserve =
            currentTurn == TurnOwner.Player1
            ? player1ReserveLayout
            : player2ReserveLayout;

        foreach (var piece in cluster)
        {
            if (piece == null)
                continue;

            // In case this piece was already registered in the other reserve earlier,
            // remove it from both reserve lists before re-registering.
            player1ReserveLayout.UnregisterPiece(piece);
            player2ReserveLayout.UnregisterPiece(piece);

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