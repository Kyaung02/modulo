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

    private void CleanupNetwork()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[NetworkMenu] Shutting down existing network session...");
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void StartNewGame()
    {
        CleanupNetwork();
        UpdateStatus("Starting New Game...");
        SaveSystem.PendingLoadData = null; 
        StartHostInternal();
    }
    
    private void StartLoadGame()
    {
        CleanupNetwork();
        UpdateStatus("Loading Save File...");
        
        if (SaveSystem.LoadSaveFile())
        {
            UpdateStatus("Save File Loaded. Starting Host...");
            StartHostInternal();
        }
        else
        {
            UpdateStatus("Failed to Load Save File!");
        }
    }

    private void Update()
    {
        // 서버가 얼었는지(Frozen) 확인하기 위한 시각적 지표
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient))
        {
             if (statusText != null && statusText.text.Contains("Running"))
             {
                 // 기존 텍스트 유지를 위해 별도 UI가 있으면 좋지만, 여기서는 임시로 뒤에 시간을 붙임
                 // 실제로는 너무 자주 바뀌면 읽기 힘드니 1초마다 변경하거나, 별도의 표시가 나음.
                 // 여기서는 간단히 로그가 아닌 화면상 확인용으로는 복잡하니 패스하거나 간단한 틱만 표시
             }
        }
    }

    // 서버가 살아있는지 확인하는 코루틴 시작
    private System.Collections.IEnumerator ServerHeartbeat()
    {
        while (true)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                // 호스트/클라이언트가 멈추지 않았음을 보여주는 로그
                string type = NetworkManager.Singleton.IsHost ? "Host" : "Client";
                Debug.Log($"[{type} Heartbeat] Time: {Time.time:F1} (If this stops, App is Frozen)");
            }
            yield return new WaitForSeconds(1.0f);
        }
    }

    private void StartHostInternal()
    {
        UpdateStatus("Starting Host...");
        if (ConfigureTransport())
        {
            Debug.Log($"[NetworkMenu] Attempting to Start Host at {GetLocalIPAddress()}...");
            if (NetworkManager.Singleton.StartHost())
            {
                UpdateStatus("Host Started. Loading Game...");
                StartCoroutine(ServerHeartbeat()); // 하트비트 시작
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            else
            {
                UpdateStatus("Failed to Start Host (Bind Error?)");
            }
        }
    }

    private void StartClient()
    {
        CleanupNetwork();
        UpdateStatus("Connecting...");
        
        if (ConfigureTransport())
        {
            if (NetworkManager.Singleton.StartClient())
            {
                UpdateStatus("Connecting...");
            }
            else
            {
                UpdateStatus("Failed to Start Client");
            }
        }
    }
    
    private bool ConfigureTransport()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) 
        {
            UpdateStatus("Error: No UnityTransport found");
            return false;
        }

        // IP
        string ip = ipInputField != null ? ipInputField.text : "127.0.0.1";
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
        // Port
        ushort port = 7777;
        if (portInputField != null && !string.IsNullOrEmpty(portInputField.text))
        {
            if (ushort.TryParse(portInputField.text, out ushort parsed)) port = parsed;
        }

        // SetConnectionData를 사용하여 설정 적용 및 ServerListenAddress를 0.0.0.0으로 명시
        transport.SetConnectionData(ip, port, "0.0.0.0");
        
        Debug.Log($"[NetworkMenu] Configured Transport: {ip}:{port}");
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
