using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Netcode;

/// <summary>
/// 인게임 메뉴 UI (ESC 키로 토글)
/// 저장 및 타이틀로 돌아가기 기능 포함
/// </summary>
public class InGameMenuUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject menuPanel; // ESC로 토글될 패널
    public Button saveButton;
    public Button titleButton; // 타이틀로 돌아가기 버튼
    public TextMeshProUGUI statusText;
    
    [Header("Settings")]
    public string titleSceneName = "Title"; // 타이틀 씬 이름

    private bool _isMenuOpen = false;

    private void OnEnable()
    {
        SaveSystem.OnSaveComplete += OnSaveComplete;
    }
    
    private void OnDisable()
    {
        SaveSystem.OnSaveComplete -= OnSaveComplete;
    }
    
    private void Start()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveButtonClicked);
        }

        if (titleButton != null)
        {
            titleButton.onClick.AddListener(OnTitleButtonClicked);
        }
        
        // 초기 상태: 메뉴 닫힘
        SetMenuState(false);
        ClearStatus();
    }

    private void Update()
    {
        if (KeybindingManager.Instance != null && KeybindingManager.Instance.GetKeyDown(GameAction.ToggleMenu))
        {
            ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        SetMenuState(!_isMenuOpen);
    }

    private void SetMenuState(bool open)
    {
        _isMenuOpen = open;
        if (menuPanel != null)
        {
            menuPanel.SetActive(open);
        }
        
        // 메뉴가 열리면 커서 표시
        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    private void OnSaveButtonClicked()
    {
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.RequestSave();
            UpdateStatus("저장 중...");
        }
        else
        {
            UpdateStatus("SaveSystem을 찾을 수 없습니다!");
        }
    }

    private void OnTitleButtonClicked()
    {
        UpdateStatus("타이틀로 돌아가는 중...");
        
        // 네트워크 종료 및 파괴
        if (NetworkManager.Singleton != null)
        {
            GameObject netManagerObj = NetworkManager.Singleton.gameObject;
            NetworkManager.Singleton.Shutdown();
            Destroy(netManagerObj);
        }

        // 씬 로드
        SceneManager.LoadScene(titleSceneName);
    }
    
    private void OnSaveComplete(bool success, string message)
    {
        UpdateStatus(message);
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            // 기존 Invoke 취소
            CancelInvoke(nameof(ClearStatus));
            // 2초 후 메시지 지우기
            Invoke(nameof(ClearStatus), 2f);
        }
        Debug.Log($"[InGameMenuUI] {message}");
    }
    
    private void ClearStatus()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }
}
