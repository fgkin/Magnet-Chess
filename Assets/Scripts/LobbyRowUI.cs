using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRowUI : MonoBehaviour
{
    private TextMeshProUGUI lobbyNameText;
    private TextMeshProUGUI playerCountText;
    private Button selectButton;

    private Lobby lobby;
    private Action<Lobby> onSelected;

    private void Awake()
    {
        selectButton = GetComponent<Button>();

        // Finds the two child TMP texts by their object names.
        Transform nameTransform = transform.Find("LobbyNameText");
        Transform countTransform = transform.Find("PlayerCountText");

        if (nameTransform != null)
            lobbyNameText = nameTransform.GetComponent<TextMeshProUGUI>();

        if (countTransform != null)
            playerCountText = countTransform.GetComponent<TextMeshProUGUI>();
    }

    public void Setup(Lobby newLobby, Action<Lobby> selectionCallback)
    {
        lobby = newLobby;
        onSelected = selectionCallback;

        if (lobbyNameText != null)
            lobbyNameText.text = lobby.Name;

        if (playerCountText != null)
            playerCountText.text = $"{lobby.Players.Count} / {lobby.MaxPlayers}";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(SelectLobby);
        }
    }

    private void SelectLobby()
    {
        if (lobby != null)
            onSelected?.Invoke(lobby);
    }
}