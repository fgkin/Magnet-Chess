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

    [Header("Game Over")]
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    private void Update()
    {
        if (gameManager == null)
            return;

        UpdateTurnText();
        UpdateTimerText();
        UpdateReserveCounts();
        UpdateGameOverUI();
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

        turnText.gameObject.SetActive(true);

        string turnLabel = gameManager.CurrentTurn switch
        {
            GameManager.TurnOwner.Player1 => "Player 1",
            GameManager.TurnOwner.Player2 => "Player 2",
            GameManager.TurnOwner.Player3 => "Player 3",
            GameManager.TurnOwner.Player4 => "Player 4",
            _ => "Player"
        };

        turnText.text = $"Turn: {turnLabel}";
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

        timerText.gameObject.SetActive(true);

        int seconds = Mathf.CeilToInt(gameManager.CurrentTurnTimeRemaining);
        timerText.text = $"Time: {seconds}";
    }

    private void UpdateReserveCounts()
    {
        SetCountText(p1CountText, 1);
        SetCountText(p2CountText, 2);
        SetCountText(p3CountText, 3);
        SetCountText(p4CountText, 4);
    }

    private void SetCountText(TextMeshProUGUI text, int playerNumber)
    {
        if (text == null)
            return;

        bool playerIsActive = playerNumber <= gameManager.ActivePlayerCount;
        text.gameObject.SetActive(playerIsActive);

        if (!playerIsActive)
            return;

        int count = gameManager.GetReserveCountForPlayer(playerNumber);
        text.text = $"P{playerNumber}: {count}";
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
    }
}