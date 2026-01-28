using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class NetworkMenuUI : MonoBehaviour
{
    [Header("UI References")]
    public Button newGameButton; // Host 버튼 대신 사용
    public Button loadGameButton; // 저장된 게임 로드
    public Button joinButton;
    public TMP_InputField ipInputField;
    public TMP_InputField portInputField; // New
    public TextMeshProUGUI statusText;

    [Header("Settings")]
    public string gameSceneName = "GameScene"; 

    private void Start()
    {
        // 로컬 테스트 시 창이 비활성화되어도 멈추지 않도록 설정 (중요: 접속 끊김 방지)
        Application.runInBackground = true;

        if (newGameButton != null)
            newGameButton.onClick.AddListener(StartNewGame);
        
        if (loadGameButton != null)
        {
            loadGameButton.onClick.AddListener(StartLoadGame);
            // 저장 파일이 있을 때만 활성화
            loadGameButton.interactable = SaveSystem.SaveFileExists();
        }
        
        if (joinButton != null)
            joinButton.onClick.AddListener(StartClient);
        
        // Show Local IP for convenience
        string localIP = GetLocalIPAddress();
        Debug.Log($"[NetworkMenu] Your Local IP is: {localIP}");
        if (statusText) statusText.text = $"Ready (Your IP: {localIP})";

        // Default IP
        if (ipInputField) ipInputField.text = "127.0.0.1";
        if (portInputField) portInputField.text = "7777";
        
        // Debug Callbacks & Connection Approval
        if (NetworkManager.Singleton != null)
        {
            // NetworkManager가 씬 전환 시 파괴되지 않도록 설정 (중요: 게임 씬으로 넘어가도 연결 유지)
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);

            // [Important] Force Protocol Version to be identical
            NetworkManager.Singleton.NetworkConfig.ProtocolVersion = 1; 

            // [CRITICAL FIX] Disable Prefab Hash Check
            // MPPM/Local Build mismatch의 주범인 프리팹 목록 검사를 끕니다.
            // 개발 단계에서만 사용하고 배포 시에는 true로 돌리는 것이 좋습니다.
            NetworkManager.Singleton.NetworkConfig.ForceSamePrefabs = false;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;

            // Connection Approval Check
            if (NetworkManager.Singleton.NetworkConfig.ConnectionApproval)
            {
                Debug.Log("[NetworkMenu] Connection Approval is ENABLED.");
                if (NetworkManager.Singleton.ConnectionApprovalCallback == null)
                {
                    Debug.Log("[NetworkMenu] Assigning default approval callback.");
                    NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
                }
            }
        
            // Log Current Prefab Hash for Debugging
            // (Only if internal API allows, otherwise skip)
        }
    }

    private string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    private async System.Threading.Tasks.Task CleanupNetwork()
    {
        if (NetworkManager.Singleton != null)
        {
            // Always try to shutdown to ensure Transport is reset
            Debug.Log("[NetworkMenu] Ensuring NetworkManager is shutdown...");
            NetworkManager.Singleton.Shutdown();
            
            // Wait for socket release
            await System.Threading.Tasks.Task.Delay(100);
        }
    }

    private bool _isBusy = false;

    private async void StartNewGame()
    {
        if (_isBusy) return;
        _isBusy = true;
        
        await CleanupNetwork();
        
        // Log Transport Settings
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            var data = transport.ConnectionData;
            Debug.Log($"[NetworkMenu] Transport attempting bind to: {data.Address}:{data.Port} (ServerIP: {data.ServerListenAddress})");
        }
        
        UpdateStatus("Authenticating...");
        await Network.LobbyManager.Instance.Authenticate();

        // 1. Authenticate (optional for local, but good for later upgrade)
        // await Network.LobbyManager.Instance.Authenticate(); 
        // -> Skipping auth for pure offline start speed, will auth when opening server.

        UpdateStatus("Starting Local Game...");
        SaveSystem.PendingLoadData = null; 
        
        // 2. Start Local Host
        // Use Port 0 (Random) to avoid "Address in use" errors during local testing
        // Use Port 0 (Random) to avoid "Address in use" errors during local testing
        if (transport != null) transport.SetConnectionData("127.0.0.1", 0);

        if (NetworkManager.Singleton.StartHost())
        {
            UpdateStatus("Local Host Started. Loading Scene...");
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
        else
        {
            UpdateStatus("Failed to Start Local Host.");
        }
        
        _isBusy = false;
    }
    
    private async void StartLoadGame()
    {
        await CleanupNetwork();
        UpdateStatus("Loading Save File...");

        if (SaveSystem.LoadSaveFile())
        {
            UpdateStatus("Save Loaded. Starting Local Game...");
           
            // await Network.LobbyManager.Instance.Authenticate(); // Defer until "Open Server"
            
            // Use Port 0 (Random)
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null) transport.SetConnectionData("127.0.0.1", 0);
            
            if (NetworkManager.Singleton.StartHost())
            {
                UpdateStatus("Local Host Started. Loading Scene...");
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
             else
            {
                UpdateStatus("Failed to Create Lobby.");
            }
            
            _isBusy = false;
        }
        else
        {
            UpdateStatus("Failed to Load Save File!");
            _isBusy = false;
        }
    }

    [Header("Lobby UI")]
    public LobbyListUI lobbyListUI;

    private async void StartClient()
    {
        await CleanupNetwork();
        UpdateStatus("Authenticating...");
        try
        {
            await Network.LobbyManager.Instance.Authenticate();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkMenu] Authentication failed: {e}");
            UpdateStatus("Authentication Failed");
            return;
        }

        if (lobbyListUI != null)
        {
            UpdateStatus("Opening Lobby List...");
            lobbyListUI.Show();
        }
        else
        {
            UpdateStatus("LobbyListUI not assigned! Trying Quick Join...");
            // 우선은 QuickJoin으로 구현 (방 목록 UI가 없으므로)
            bool success = await Network.LobbyManager.Instance.QuickJoinLobby();
            if (success)
            {
                UpdateStatus("Room Found! Connecting...");
            }
            else
            {
                UpdateStatus("No rooms found or Connection Failed.");
            }
        }
    }
    
    private bool ConfigureTransport()
    {
        // Relay를 사용할 때는 Transport 설정이 LobbyManager에서 자동으로 처리되므로
        // 이 메소드는 직접 IP 연결을 할 때만 유효합니다.
        // 현재 로직에서는 사용되지 않으나, 혹시 모를 로컬 폴백을 위해 남겨둡니다.
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return false;

        string ip = ipInputField != null ? ipInputField.text : "127.0.0.1";
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
        ushort port = 7777;
        if (portInputField != null && ushort.TryParse(portInputField.text, out ushort parsed)) port = parsed;

        transport.SetConnectionData(ip, port, "0.0.0.0");
        return true;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Allow everyone
        Debug.Log($"[NetworkMenu] Approving connection for Client {request.ClientNetworkId}");
        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkMenu] Client Connected: {clientId}");
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
            UpdateStatus($"Connected! (ID: {clientId})");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        string reason = NetworkManager.Singleton.DisconnectReason;
        if (string.IsNullOrEmpty(reason)) reason = "Unknown (Check Logs)";
        
        Debug.Log($"[NetworkMenu] Client Disconnected: {clientId}. Reason: {reason}");
        
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
            UpdateStatus($"Disconnected (ID: {clientId}): {reason}");
    }

    private void OnTransportFailure()
    {
        Debug.LogError("[NetworkMenu] Transport Failure!");
        UpdateStatus("Transport Failure");
    }

    private void UpdateStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log($"[NetworkMenu]: {msg}");
    }
}
