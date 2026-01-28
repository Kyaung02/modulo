using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Added for New Input System
using TMPro; // Add Namespace

public class GoalUI : MonoBehaviour
{
    [Header("UI Elements")]
    public ItemIconUI targetIcon;
    public TMP_Text progressText; // Changed to TMP_Text for TextMeshPro support
    public TMP_Text goalNameText; // <-- Added this
    public GameObject levelCompletePanel; // Optional: Show when level is done
    
    [Header("Milestone Integration")]
    public Button milestoneButton;
    public MilestoneUI milestoneWindow;

    public static GoalUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (milestoneButton != null && milestoneWindow != null)
        {
            milestoneButton.onClick.AddListener(() => milestoneWindow.ToggleWindow());
        }

        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.OnGoalUpdated += UpdateUI;
            GoalManager.Instance.OnLevelComplete += OnLevelComplete;
            
            // Initial update
            UpdateUI();
        }
        
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        UpdateUI(); // Ensure text is cleared if nothing connected yet
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && milestoneWindow != null)
        {
            milestoneWindow.ToggleWindow();
        }
    }

    public void ForceUpdateUI()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (GoalManager.Instance == null) return;

        GoalManager.LevelGoal goal = GoalManager.Instance.GetPinnedGoal();
        
        if (goal != null)
        {
            // Update Icon
            if (targetIcon != null)
            {
                targetIcon.gameObject.SetActive(true);
                targetIcon.SetWord(goal.targetWord);
            }

            // Update Text
            if (progressText != null)
            {
                if (goal.targetWord != null)
                {
                    // Get specific progress for this pinned goal
                    int progress = GoalManager.Instance.GetProgress(GoalManager.Instance.PinnedGoalIndex);
                    progressText.text = $"{progress} / {goal.requiredCount}";
                }
                else
                {
                   progressText.text = "Complete!";
                }
            }
            
            if (goalNameText != null)
            {
                 if (goal.targetWord != null) goalNameText.text = goal.targetWord.wordName;
                 else goalNameText.text = "Milestone";
            }
        }
        else
        {
            // Nothing pinned
            if (targetIcon != null) targetIcon.gameObject.SetActive(false);
            if (progressText != null) progressText.text = "Pin a Goal";
            if (goalNameText != null) goalNameText.text = "Open Milestones";
        }
    }

    private void OnLevelComplete()
    {
        // Simple visual feedback
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
            Invoke("HideLevelComplete", 2.0f);
        }
    }

    private void HideLevelComplete()
    {
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.OnGoalUpdated -= UpdateUI;
            GoalManager.Instance.OnLevelComplete -= OnLevelComplete;
        }
    }
}
