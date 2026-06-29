using UnityEngine;

public class MainMenuNavigation : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyBrowserPanel;
    [SerializeField] private GameObject lobbyRoomPanel;
    [SerializeField] private GameObject tutorialPanel;

    private void Start()
    {
        ShowMainMenu();

        if (PlayerPrefs.GetInt("TutorialSeen", 0) == 0)
            OpenTutorial();
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        lobbyBrowserPanel.SetActive(false);
        lobbyRoomPanel.SetActive(false);

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
    }

    public void OpenLobbyBrowser()
    {
        mainMenuPanel.SetActive(false);
        lobbyBrowserPanel.SetActive(true);
        lobbyRoomPanel.SetActive(false);
    }

    public void OpenLobbyRoom()
    {
        mainMenuPanel.SetActive(false);
        lobbyBrowserPanel.SetActive(false);
        lobbyRoomPanel.SetActive(true);
    }

    //Teaching kids how to play the game
    public void OpenTutorial()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (lobbyBrowserPanel != null)
            lobbyBrowserPanel.SetActive(false);

        if (lobbyRoomPanel != null)
            lobbyRoomPanel.SetActive(false);

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        PlayerPrefs.SetInt("TutorialSeen", 1);
        PlayerPrefs.Save();
    }
}