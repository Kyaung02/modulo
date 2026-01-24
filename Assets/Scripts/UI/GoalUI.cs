using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add Namespace

public class GoalUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image targetIcon;
    public TMP_Text progressText; // Changed to TMP_Text for TextMeshPro support
    public GameObject levelCompletePanel; // Optional: Show when level is done

    private void Start()
    {
        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.OnGoalUpdated += UpdateUI;
            GoalManager.Instance.OnLevelComplete += OnLevelComplete;
            
            // Initial update
            UpdateUI();
        }
        
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
    }

    private void UpdateUI()
    {
        if (GoalManager.Instance == null) return;

        GoalManager.LevelGoal goal = GoalManager.Instance.GetCurrentGoal();
        
        // Update Icon
        if (targetIcon != null)
        {
            if (goal.targetWord != null)
            {
                targetIcon.sprite = goal.targetWord.wordIcon;
                targetIcon.color = Color.white;
                targetIcon.enabled = true;
            }
            else
            {
                // No goal or finished
                targetIcon.enabled = false; 
            }
        }

        // Update Text
        if (progressText != null)
        {
            if (goal.targetWord != null)
            {
                progressText.text = $"{GoalManager.Instance.currentDeliverCount} / {goal.requiredCount}";
            }
            else
            {
                progressText.text = "All Goals Complete!";
            }
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
