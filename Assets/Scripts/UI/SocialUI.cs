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
        
        private float _lastListUpdate;
        private List<GameObject> _spawnedPlayerItems = new List<GameObject>();

        private void Start()
        {
            UpdatePlayerList();
        }

        private void Update()
        {
            if (Time.time - _lastListUpdate > listUpdateInterval)
            {
                UpdatePlayerList();
                _lastListUpdate = Time.time;
            }
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
