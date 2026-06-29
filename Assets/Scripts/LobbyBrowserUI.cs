using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyBrowserUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MultiplayerLobbyManager lobbyManager;
    [SerializeField] private Transform lobbyListContent;
    [SerializeField] private LobbyRowUI lobbyRowPrefab;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private readonly List<GameObject> spawnedRows = new();
    private Lobby selectedLobby;

    private void OnEnable()
    {
        RefreshLobbyList();
    }

    public async void RefreshLobbyList()
    {
        SetStatus("Lobiler yenileniyor...");
        ClearLobbyRows();

        selectedLobby = null;

        if (joinLobbyButton != null)
            joinLobbyButton.interactable = false;

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        QueryFilter.FieldOptions.AvailableSlots,
                        "0",
                        QueryFilter.OpOptions.GT
                    )
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);

            foreach (Lobby lobby in response.Results)
            {
                LobbyRowUI row = Instantiate(lobbyRowPrefab, lobbyListContent);
                row.Setup(lobby, SelectLobby);
                spawnedRows.Add(row.gameObject);
            }

            SetStatus(response.Results.Count == 0
                ? "Uygun lobi bulunamadı."
                : $"{response.Results.Count} lobi bulundu.");
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            SetStatus("Refresh failed: " + e.Message);
        }
    }

    private void SelectLobby(Lobby lobby)
    {
        selectedLobby = lobby;

        if (joinLobbyButton != null)
            joinLobbyButton.interactable = true;

       SetStatus($"Seçilen lobi: {lobby.Name}");
    }

    public void JoinSelectedLobby()
    {
        if (selectedLobby == null)
        {
            SetStatus("Önce bir lobi seç.");    
            return;
        }

        lobbyManager.JoinLobbyById(selectedLobby.Id);
    }

    private void ClearLobbyRows()
    {
        foreach (GameObject row in spawnedRows)
        {
            if (row != null)
                Destroy(row);
        }

        spawnedRows.Clear();
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
            statusText.text = message;
    }
}