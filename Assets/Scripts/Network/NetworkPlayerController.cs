using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Network
{
    // Ensure this script requires a NetworkObject component
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        public NetworkVariable<Unity.Collections.FixedString64Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString64Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
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
            currentViewId.OnValueChanged += OnViewIdChanged;
            
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
            UpdatePlayerPosition(currentViewId.Value);
        }
        
        public override void OnNetworkDespawn() 
        {
            playerColor.OnValueChanged -= OnPlayerColorChanged;
            currentViewId.OnValueChanged -= OnViewIdChanged;
            base.OnNetworkDespawn();
        }

        private void OnPlayerColorChanged(Color oldColor, Color newColor)
        {
            UpdatePlayerColor(newColor);
        }

        private void OnViewIdChanged(ulong oldId, ulong newId)
        {
            UpdatePlayerPosition(newId);
        }

        private void UpdatePlayerPosition(ulong viewId)
        {
            if (viewId == 0)
            {
                // Root
                transform.position = Vector3.zero; // Or specific spawn point
                return;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(viewId, out NetworkObject obj))
            {
                var mod = obj.GetComponent<RecursiveModuleComponent>();
                if (mod != null)
                {
                    // Ensure Inner World is available (might need to wait or it might be lazy init)
                    // On clients, RecursiveModule might not have init yet if we just joined.
                    // But usually module exists before we enter.
                    if (mod.innerGrid == null && mod.TryGetComponent(out RecursiveModuleComponent rmc))
                    {
                         // Try forcing init if we are here? 
                         // Or just use the calculated position based on netInnerWorldPos?
                         // Accessing private/internal logic from here is hard. 
                         // Let's rely on RecursiveModule public helper or common logic.
                         // RecursiveModule.InstantEnterModule logic calculates center:
                         // center = mod.transform.position + ... NO. 
                         // Center is based on innerGrid.transform.position.
                    }
                    
                    if (mod.innerGrid != null)
                    {
                        ModuleManager m = mod.innerGrid;
                        Vector3 center = new Vector3(
                            m.transform.position.x + m.originPosition.x + (m.width * m.cellSize * 0.5f),
                            m.transform.position.y + m.originPosition.y + (m.height * m.cellSize * 0.5f),
                            0f 
                        );
                        transform.position = center;
                    }
                }
            }
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
        
        private ulong _lastSentViewId = 0;

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
            
            // Check against committed NetVar AND locally sent request to avoid spam
            if (currentViewId.Value != newViewId && _lastSentViewId != newViewId)
            {
                _lastSentViewId = newViewId;
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
