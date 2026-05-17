using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TextMeshProUGUI countdownText;

    private void Update()
    {
        if (gameManager == null)
            return;

        if (gameManager.IsOnlineGame())
        {
            if (pausePanel != null)
                pausePanel.SetActive(gameManager.IsGamePaused);

            if (countdownText != null)
            {
                int countdown = gameManager.PauseCountdown;
                countdownText.gameObject.SetActive(countdown > 0);
                countdownText.text = countdown > 0 ? countdown.ToString() : "";
            }
        }
    }

    public void PauseGame()
    {
        if (gameManager != null && gameManager.IsOnlineGame())
        {
            gameManager.RequestSetPause(true);
            return;
        }

        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (gameManager != null && gameManager.IsOnlineGame())
        {
            gameManager.RequestSetPause(false);
            return;
        }

        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;

        if (gameManager != null)
        {
            gameManager.RequestLeaveOrEndOnlineGame();
            return;
        }

        SceneManager.LoadScene("MainMenu");
    }
}