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
        if (gameManager.CurrentState == GameManager.TurnState.GameOver)
        {
            turnText.gameObject.SetActive(false);
            return;
        }

        turnText.gameObject.SetActive(true);

        string turnLabel = gameManager.CurrentTurn == GameManager.TurnOwner.Player1
            ? "Player 1"
            : "Player 2";

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
        p1CountText.text = $"P1: {gameManager.GetPlayer1ReserveCount()}";
        p2CountText.text = $"P2: {gameManager.GetPlayer2ReserveCount()}";
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