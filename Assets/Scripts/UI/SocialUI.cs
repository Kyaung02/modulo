using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;
using Network;

namespace UI
{
    public class SocialUI : MonoBehaviour
    {
        [Header("Player List")]
        public Transform playerListContainer;
        public GameObject playerListItemPrefab;
        public float listUpdateInterval = 1.0f;
        public Button serverOpenButton;
        public TextMeshProUGUI serverStatusText;
        
        private float _lastListUpdate;
        private List<GameObject> _spawnedPlayerItems = new List<GameObject>();

        private void Start()
        {
            UpdatePlayerList();
            if (serverOpenButton != null)
            {
                serverOpenButton.onClick.AddListener(OnServerOpenClicked);
            }
            UpdateServerStatus();
        }

        private void Update()
        {
            if (Time.time - _lastListUpdate > listUpdateInterval)
            {
                UpdatePlayerList();
                UpdateServerStatus();
                _lastListUpdate = Time.time;
            }
        }

        private void UpdateServerStatus()
        {
            if (serverStatusText == null) return;
            
            bool isOnline = LobbyManager.Instance.IsSessionActive;
            serverStatusText.text = isOnline ? "Online Server" : "Offline / Local";
            
            if (serverOpenButton)
            {
                // Only enable if we are Host and NOT online yet
                serverOpenButton.interactable = NetworkManager.Singleton.IsHost && !isOnline;
                serverOpenButton.gameObject.SetActive(NetworkManager.Singleton.IsHost && !isOnline); // Hide if already online
            }
        }

        private async void OnServerOpenClicked()
        {
            if (serverOpenButton) serverOpenButton.interactable = false;
            
            // 1. Save current state (if needed, assume SaveSystem handles auto-save or persisted state)
            // SaveSystem.SaveGame(); // Optional

            // 2. Switch to Online
            bool success = await LobbyManager.Instance.PromoteLocalToOnline("My Game Room", 4);
            
            if (success)
            {
                Debug.Log("Server Opened Successfully!");
            }
            else
            {
                Debug.LogError("Failed to Open Server");
                if (serverOpenButton) serverOpenButton.interactable = true;
            }
            
            UpdateServerStatus();
        }

        private void UpdatePlayerList()
        {
            if (playerListContainer == null || playerListItemPrefab == null) return;
            if (NetworkManager.Singleton == null) return;

            // Clear old items
            foreach (var item in _spawnedPlayerItems) Destroy(item);
            _spawnedPlayerItems.Clear();

            // Only show list if connected
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer) return;

            // Iterate all connected players
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                
                var pc = client.PlayerObject.GetComponent<NetworkPlayerController>();
                if (pc == null) continue;

                GameObject obj = Instantiate(playerListItemPrefab, playerListContainer);
                PlayerListItem item = obj.GetComponent<PlayerListItem>();
                
                bool isSelf = (client.ClientId == NetworkManager.Singleton.LocalClientId);
                item.Setup(pc.playerName.Value.ToString(), pc.currentViewId.Value, isSelf, pc.playerColor.Value);
                
                _spawnedPlayerItems.Add(obj);
            }
        }
    }
}
