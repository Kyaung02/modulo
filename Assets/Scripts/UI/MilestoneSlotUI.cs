using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MilestoneSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text titleText; // Restored Title Text
    public TMP_Text buttonText; // Explicit reference for button label
    public TMP_Text progressText;
    public GameObject descriptionObject; // To hide description when locked
    public ItemIconUI iconUI;
    public Button selectButton;
    public Image statusBackground; // Optional: changing color based on status

    [Header("Settings")]
    public int targetGoalIndex; // Set this in Inspector for manual layout

    private MilestoneUI _manager;

    public void Init(MilestoneUI manager)
    {
        _manager = manager;
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    public void RefreshUI()
    {
        if (GoalManager.Instance == null) return;
        if (GoalManager.Instance.levels == null || targetGoalIndex < 0 || targetGoalIndex >= GoalManager.Instance.levels.Length) 
        {
            // Invalid slot or index
            gameObject.SetActive(false);
            return;
        }
        
        gameObject.SetActive(true);

        var goal = GoalManager.Instance.levels[targetGoalIndex];
        bool isUnlocked = GoalManager.Instance.IsGoalUnlocked(targetGoalIndex);
        bool isCompleted = GoalManager.Instance.IsGoalCompleted(targetGoalIndex);
        bool isPinned = (GoalManager.Instance.PinnedGoalIndex == targetGoalIndex);

        // Progress display
        if (isUnlocked)
        {
            int current = GoalManager.Instance.GetProgress(targetGoalIndex);
            if (progressText != null) progressText.text = $"{current} / {goal.requiredCount}";
            if (iconUI != null)
            {
                iconUI.gameObject.SetActive(true);
                if (goal.targetWord != null) iconUI.SetWord(goal.targetWord);
            }
        }
        else
        {
            if (progressText != null) progressText.text = "???";
            if (iconUI != null) iconUI.gameObject.SetActive(false);
        }

        if (descriptionObject != null) descriptionObject.SetActive(isUnlocked);
        
        // Title Text
        if (titleText != null)
        {
            if (isUnlocked && goal.targetWord != null) titleText.text = goal.targetWord.wordName;
            else titleText.text = "Unknown Milestone";
        }

        if (selectButton != null)
        {
            selectButton.interactable = isUnlocked && !isCompleted; 
            
            // Only update button text if explicitly assigned
            if (buttonText != null)
            {
                if (isPinned) buttonText.text = "Pinned";
                else if (isCompleted) buttonText.text = "Done";
                else if (isUnlocked) buttonText.text = "Pin";
                else buttonText.text = "Locked";
            }
        }
        
        // Highlight active (Pinned)
        if (isPinned && statusBackground != null) statusBackground.color = Color.green; 
        else if (statusBackground != null) statusBackground.color = Color.white;
    }

    private void OnSelectClicked()
    {
        if (_manager != null)
            _manager.PinMilestone(targetGoalIndex);
    }
}
