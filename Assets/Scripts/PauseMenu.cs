using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameManager gameManager;

    private void Update()
    {
        if (gameManager == null)
            return;

        if (!gameManager.IsOnlineGame())
            return;

        if (pausePanel != null)
            pausePanel.SetActive(gameManager.IsGamePaused);
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