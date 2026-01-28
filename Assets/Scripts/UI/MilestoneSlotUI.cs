using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MilestoneSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text progressText;
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
        int current = GoalManager.Instance.GetProgress(targetGoalIndex);
        if (progressText != null) progressText.text = $"{current} / {goal.requiredCount}";
        
        if (iconUI != null && goal.targetWord != null) iconUI.SetWord(goal.targetWord);

        if (selectButton != null)
        {
            selectButton.interactable = isUnlocked; 
            
            TMP_Text btnText = selectButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                if (isPinned) btnText.text = "Pinned";
                else if (isCompleted) btnText.text = "Done";
                else if (isUnlocked) btnText.text = "Pin";
                else btnText.text = "Locked";
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
