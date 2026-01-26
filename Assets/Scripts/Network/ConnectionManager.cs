using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Network
{
    public class ConnectionManager : MonoBehaviour
    {
        [Header("UI References")]
        public Button hostButton;
        public Button serverButton;
        public Button clientButton;
        public GameObject connectionPanel; // Optional: Panel containing buttons

        private void Start()
        {
            // Auto-assign listeners if buttons function
            if (hostButton) hostButton.onClick.AddListener(StartHost);
            if (serverButton) serverButton.onClick.AddListener(StartServer);
            if (clientButton) clientButton.onClick.AddListener(StartClient);
        }

        public void StartHost()
        {
            Debug.Log("Starting Host...");
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Host Started Successfully");
                HideUI();
            }
            else
            {
                Debug.LogError("Failed to start Host");
            }
        }

        public void StartServer()
        {
            Debug.Log("Starting Server...");
            if (NetworkManager.Singleton.StartServer())
            {
                Debug.Log("Server Started Successfully");
                HideUI();
            }
            else
            {
                Debug.LogError("Failed to start Server");
            }
        }

        public void StartClient()
        {
            Debug.Log("Starting Client...");
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Client Started Successfully");
                HideUI();
            }
            else
            {
                Debug.LogError("Failed to start Client");
            }
        }

        private void HideUI()
        {
            if (connectionPanel)
            {
                connectionPanel.SetActive(false);
            }
            else
            {
                if (hostButton) hostButton.gameObject.SetActive(false);
                if (serverButton) serverButton.gameObject.SetActive(false);
                if (clientButton) clientButton.gameObject.SetActive(false);
            }
        }
    }
}
