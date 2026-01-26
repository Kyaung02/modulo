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
        
        // Default IP
        if (ipInputField) ipInputField.text = "127.0.0.1";
        if (portInputField) portInputField.text = "7777";
        
        UpdateStatus("Ready");
    }

    private void StartNewGame()
    {
        UpdateStatus("Starting New Game...");
        SaveSystem.PendingLoadData = null; // 새 게임이므로 로드 데이터 없음
        StartHostInternal();
    }
    
    private void StartLoadGame()
    {
        UpdateStatus("Loading Save File...");
        
        // 저장 파일 로드
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
    
    private void StartHostInternal()
    {
        if (ConfigureTransport())
        {
            if (NetworkManager.Singleton.StartHost())
            {
                UpdateStatus("Host Started. Loading Game...");
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
        UpdateStatus("Connecting...");
        
        if (ConfigureTransport())
        {
            if (NetworkManager.Singleton.StartClient())
            {
                UpdateStatus("Connecting...");
                // Scene will sync automatically upon connection
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
        transport.ConnectionData.Address = ip;
        
        // Port
        ushort port = 7777;
        if (portInputField != null && !string.IsNullOrEmpty(portInputField.text))
        {
            if (ushort.TryParse(portInputField.text, out ushort parsed)) port = parsed;
        }
        transport.ConnectionData.Port = port;
        
        Debug.Log($"[NetworkMenu] Configured Transport: {ip}:{port}");
        return true;
    }

    private void UpdateStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log($"[NetworkMenu]: {msg}");
    }
}
