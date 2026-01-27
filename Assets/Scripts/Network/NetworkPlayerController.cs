using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Network
{
    // Ensure this script requires a NetworkObject component
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>("Player");
        // 0 = Root, >0 = NetworkObjectId of the Module we are inside
        public NetworkVariable<ulong> currentViewId = new NetworkVariable<ulong>(0);
        public NetworkVariable<Color> playerColor = new NetworkVariable<Color>(Color.white);

        [Header("Visuals")]
        [SerializeField] private Renderer playerRenderer;
        
        // Called when the object is spawned on the network
        public override void OnNetworkSpawn()
        {
            if (playerRenderer == null) 
                playerRenderer = GetComponent<Renderer>();

            playerColor.OnValueChanged += OnPlayerColorChanged;
            
            // Initial Sync
            if (IsOwner)
            {
                // Set default name (e.g. "Player 123")
                playerName.Value = $"Player {OwnerClientId}";
            }
            
             if (IsServer)
            {
                // Assign Random Color
                playerColor.Value = new Color(Random.value, Random.value, Random.value);
            }
            
            UpdatePlayerColor(playerColor.Value);
        }
        
        public override void OnNetworkDespawn() 
        {
            playerColor.OnValueChanged -= OnPlayerColorChanged;
            base.OnNetworkDespawn();
        }

        private void OnPlayerColorChanged(Color oldColor, Color newColor)
        {
            UpdatePlayerColor(newColor);
        }

        private void UpdatePlayerColor(Color c)
        {
            if (playerRenderer == null) return;
            playerRenderer.material.color = c;
        }

        private void Update()
        {
            // Only control your own player
            if (!IsOwner) return;

            CheckCurrentView();
        }
        
        // Track where the player is looking/working
        private void CheckCurrentView()
        {
            if (BuildManager.Instance == null) return;
            
            ulong newViewId = 0;
            var activeMgr = BuildManager.Instance.activeManager;
            
            // If activeManager is NOT the root instance
            if (activeMgr != null && activeMgr != ModuleManager.Instance)
            {
                // Find the RecursiveModule that OWNS this manager
                if (activeMgr.ownerComponent != null)
                {
                    newViewId = activeMgr.ownerComponent.NetworkObjectId;
                }
            }
            
            if (currentViewId.Value != newViewId)
            {
                CheckCurrentViewServerRpc(newViewId);
            }
        }

        [ServerRpc]
        private void CheckCurrentViewServerRpc(ulong newId)
        {
            currentViewId.Value = newId;
        }


    }
}
