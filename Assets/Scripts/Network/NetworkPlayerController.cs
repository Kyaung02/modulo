using Unity.Netcode;
using UnityEngine;

namespace Network
{
    // Ensure this script requires a NetworkObject component
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Visuals")]
        [SerializeField] private Renderer playerRenderer;
        
        // Called when the object is spawned on the network
        public override void OnNetworkSpawn()
        {
            if (playerRenderer == null) 
                playerRenderer = GetComponent<Renderer>();

            UpdatePlayerColor();
        }

        private void UpdatePlayerColor()
        {
            if (playerRenderer == null) return;

            if (IsOwner)
            {
                // Local player is Blue
                playerRenderer.material.color = Color.blue;
            }
            else
            {
                // Remote players are Red
                playerRenderer.material.color = Color.red;
            }
        }

        private void Update()
        {
            // Only control your own player
            if (!IsOwner) return;

            HandleMovement();
        }

        private void HandleMovement()
        {
            Vector3 moveDir = Vector3.zero;

            // Simple WASD movement
            if (Input.GetKey(KeyCode.W)) moveDir.y += 1f;
            if (Input.GetKey(KeyCode.S)) moveDir.y -= 1f;
            if (Input.GetKey(KeyCode.A)) moveDir.x -= 1f;
            if (Input.GetKey(KeyCode.D)) moveDir.x += 1f;

            if (moveDir != Vector3.zero)
            {
                // Move based on direction, speed, and time
                transform.position += moveDir.normalized * moveSpeed * Time.deltaTime;
            }
        }
    }
}
