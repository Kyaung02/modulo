using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Multiplayer;
using Network;

public class LobbyListUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform container;
    public LobbyItemUI itemTemplate;
    public Button refreshButton;
    public Button closeButton;
    public GameObject rootPanel; // The visual panel to toggle

    private void Awake()
    {
        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        
        if (refreshButton) refreshButton.onClick.AddListener(RefreshList);
        if (closeButton) closeButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        rootPanel.SetActive(true);
        RefreshList();
    }

    public void Hide()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    public async void RefreshList()
    {
        // Clear old items
        foreach (Transform child in container)
        {
            if (child == itemTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        IList<ISessionInfo> sessions = await LobbyManager.Instance.GetLobbyList();

        foreach (ISessionInfo session in sessions)
        {
            LobbyItemUI item = Instantiate(itemTemplate, container);
            item.gameObject.SetActive(true);
            item.Initialize(session, OnJoinLobby);
        }
    }

    private async void OnJoinLobby(ISessionInfo session)
    {
        Debug.Log($"Joining Lobby: {session.Name}");
        bool success = await LobbyManager.Instance.JoinLobby(session);
        
        if (this == null) return;

        if (success)
        {
            Hide();
            // NetworkMenuUI handles status updates via NetworkManager callbacks usually, 
            // but we might want to notify it of success here if needed.
        }
        else
        {
            Debug.LogError("Failed to join lobby.");
        }
    }
}
