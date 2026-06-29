using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRoomUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MultiplayerLobbyManager lobbyManager;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI readyCountText;
    [SerializeField] private TextMeshProUGUI startInfoText;

    [Header("Buttons")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;

    private TextMeshProUGUI readyButtonText;

    private void Awake()
    {
        if (readyButton != null)
            readyButtonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (lobbyManager != null)
            lobbyManager.OnLobbyUpdated += RefreshUI;

        RefreshUI();
    }

    private void OnDisable()
    {
        if (lobbyManager != null)
            lobbyManager.OnLobbyUpdated -= RefreshUI;
    }

    public void ToggleReady()
    {
        lobbyManager.ToggleReady();
    }

    public void StartGame()
    {
        lobbyManager.StartGame();
    }

    public void LeaveLobby()
    {
        lobbyManager.LeaveLobbyAndReturnToBrowser();
    }

    private void RefreshUI()
    {
        if (lobbyManager == null || !lobbyManager.IsInLobby)
            return;

        lobbyNameText.text = $"Lobi: {lobbyManager.CurrentLobbyName}";
        playerCountText.text =
            $"Oyuncular: {lobbyManager.CurrentPlayerCount}/{lobbyManager.MaxLobbyPlayers}";

        readyCountText.text =
            $"Hazır: {lobbyManager.ReadyPlayerCount}/{lobbyManager.CurrentPlayerCount}";

        if (readyButtonText != null)
            readyButtonText.text = lobbyManager.IsLocalPlayerReady ? "Hazır Değil" : "Hazır";

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(lobbyManager.IsLobbyHost);
            startGameButton.interactable = lobbyManager.CanHostStartGame;
        }

        if (startInfoText != null)
        {
            if (lobbyManager.IsLobbyHost &&
                !lobbyManager.AreAllLobbyPlayersNetworkConnected)
            {
                startInfoText.text = "Oyuncular bağlanıyor...";
            }
            else if (lobbyManager.CanHostStartGame)
            {
                startInfoText.text = "Tüm oyuncular hazır.";
            }
            else
            {
                startInfoText.text = "Tüm oyuncuların hazır olması bekleniyor.";
            }
        }
    }
}