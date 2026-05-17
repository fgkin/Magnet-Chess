using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("HUD Text")]
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI p1CountText;
    [SerializeField] private TextMeshProUGUI p2CountText;
    [SerializeField] private TextMeshProUGUI p3CountText;
    [SerializeField] private TextMeshProUGUI p4CountText;
    [SerializeField] private TextMeshProUGUI localPlayerText;

    [Header("Label Backgrounds")]
    [SerializeField] private Image p1LabelBackground;
    [SerializeField] private Image p2LabelBackground;
    [SerializeField] private Image p3LabelBackground;
    [SerializeField] private Image p4LabelBackground;
    [SerializeField] private Image turnTimerBackground;

    [Header("Player Colors")]
    [SerializeField] private Color player1Color = new Color(0.2f, 0.55f, 1f);
    [SerializeField] private Color player2Color = new Color(1f, 0.25f, 0.25f);
    [SerializeField] private Color player3Color = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color player4Color = new Color(0.25f, 1f, 0.35f);

    [Header("Visibility")]
    [SerializeField] private Color normalBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private float activeBackgroundAlpha = 0.75f;
    [SerializeField] private float inactiveBackgroundAlpha = 0.45f;
    [SerializeField] private float normalFontSize = 34f;
    [SerializeField] private float activeFontSize = 40f;

    [Header("Game Over")]
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TextMeshProUGUI restartVoteText;

    private void Update()
    {
        if (gameManager == null)
            return;

        UpdateTurnText();
        UpdateTimerText();
        UpdateTurnTimerBackground();
        UpdateReserveCounts();
        UpdateGameOverUI();
        UpdateLocalPlayerText();
    }

    private void UpdateTurnText()
    {
        if (turnText == null)
            return;

        if (gameManager.CurrentState == GameManager.TurnState.GameOver)
        {
            turnText.gameObject.SetActive(false);
            return;
        }

        int currentPlayerNumber = GetCurrentPlayerNumber();

        turnText.gameObject.SetActive(true);
        turnText.text = $"P{currentPlayerNumber} TURN";
        turnText.color = GetPlayerColor(currentPlayerNumber);
        turnText.fontStyle = FontStyles.Bold;
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
            return;

        if (gameManager.CurrentState == GameManager.TurnState.GameOver)
        {
            timerText.gameObject.SetActive(false);
            return;
        }

        int currentPlayerNumber = GetCurrentPlayerNumber();
        int seconds = Mathf.CeilToInt(gameManager.CurrentTurnTimeRemaining);

        timerText.gameObject.SetActive(true);
        timerText.text = $"{seconds}s";
        timerText.color = GetPlayerColor(currentPlayerNumber);
        timerText.fontStyle = FontStyles.Bold;
    }

    private void UpdateReserveCounts()
    {
        SetPlayerLabel(p1CountText, p1LabelBackground, 1);
        SetPlayerLabel(p2CountText, p2LabelBackground, 2);
        SetPlayerLabel(p3CountText, p3LabelBackground, 3);
        SetPlayerLabel(p4CountText, p4LabelBackground, 4);
    }

    private void SetPlayerLabel(TextMeshProUGUI text, Image background, int playerNumber)
    {
        bool playerIsActive = playerNumber <= gameManager.ActivePlayerCount;

        if (text != null)
            text.gameObject.SetActive(playerIsActive);

        if (background != null)
            background.gameObject.SetActive(playerIsActive);

        if (!playerIsActive)
            return;

        bool isCurrentTurn = playerNumber == GetCurrentPlayerNumber();
        Color playerColor = GetPlayerColor(playerNumber);

        if (text != null)
        {
            int count = gameManager.GetReserveCountForPlayer(playerNumber);

            text.text = $"P{playerNumber} · {count}";
            text.color = playerColor;
            text.fontStyle = isCurrentTurn ? FontStyles.Bold : FontStyles.Normal;
            text.fontSize = isCurrentTurn ? activeFontSize : normalFontSize;
        }

        if (background != null)
        {
            Color bgColor = normalBackgroundColor;
            bgColor.a = isCurrentTurn ? activeBackgroundAlpha : inactiveBackgroundAlpha;

            background.color = bgColor;

            float scale = isCurrentTurn ? 1.12f : 1f;
            background.rectTransform.localScale = Vector3.one * scale;
        }
    }

    private void UpdateTurnTimerBackground()
    {
        if (turnTimerBackground == null)
            return;

        bool shouldShow = gameManager.CurrentState != GameManager.TurnState.GameOver;
        turnTimerBackground.gameObject.SetActive(shouldShow);

        if (!shouldShow)
            return;

        int currentPlayerNumber = GetCurrentPlayerNumber();
        Color bgColor = normalBackgroundColor;
        bgColor.a = activeBackgroundAlpha;

        turnTimerBackground.color = bgColor;
    }

    private void UpdateGameOverUI()
    {
        bool isGameOver = gameManager.CurrentState == GameManager.TurnState.GameOver;

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(isGameOver);

            if (isGameOver)
                gameOverText.text = gameManager.GetWinnerUILabel();
        }

        if (restartButton != null)
            restartButton.gameObject.SetActive(isGameOver);

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(isGameOver);

        if (restartVoteText != null)
        {
            bool showVoteText = isGameOver && gameManager.IsOnlineGame();
            restartVoteText.gameObject.SetActive(showVoteText);

            if (showVoteText)
            {
                restartVoteText.text =
                    $"Restart votes: {gameManager.RestartVoteCount}/{gameManager.RestartRequiredCount}";
            }
        }
    }

    private int GetCurrentPlayerNumber()
    {
        return gameManager.CurrentTurn switch
        {
            GameManager.TurnOwner.Player1 => 1,
            GameManager.TurnOwner.Player2 => 2,
            GameManager.TurnOwner.Player3 => 3,
            GameManager.TurnOwner.Player4 => 4,
            _ => 1
        };
    }

    private Color GetPlayerColor(int playerNumber)
    {
        return playerNumber switch
        {
            1 => player1Color,
            2 => player2Color,
            3 => player3Color,
            4 => player4Color,
            _ => Color.white
        };
    }

    private void UpdateLocalPlayerText()
    {
        if (localPlayerText == null)
            return;

        if (gameManager == null || !gameManager.IsOnlineGame())
        {
            localPlayerText.gameObject.SetActive(false);
            return;
        }

        localPlayerText.gameObject.SetActive(true);

        int playerNumber = GetLocalPlayerNumber();

        localPlayerText.text = $"YOU ARE P{playerNumber}";
        localPlayerText.color = GetPlayerColor(playerNumber);
        localPlayerText.fontStyle = FontStyles.Bold;
    }

    private int GetLocalPlayerNumber()
    {
        if (gameManager == null)
            return 1;

        return gameManager.GetLocalPlayerNumber();
    }
}