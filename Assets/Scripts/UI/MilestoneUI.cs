using UnityEngine;
using System.Collections.Generic;

public class MilestoneUI : MonoBehaviour
{
    [Header("References")]
    public GameObject windowPanel; // To toggle visibility
    public Transform contentContainer; // Where slots go

    // For manual layout, we find slots in children
    private List<MilestoneSlotUI> _activeSlots = new List<MilestoneSlotUI>();

    private void Start()
    {
        // Find existing slots in the container
        if (contentContainer != null)
        {
            _activeSlots = new List<MilestoneSlotUI>(contentContainer.GetComponentsInChildren<MilestoneSlotUI>(true));
            foreach (var slot in _activeSlots)
            {
                slot.Init(this);
            }
        }

        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.OnGoalUpdated += RefreshUI;
        }
        
        // Initial refresh
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.OnGoalUpdated -= RefreshUI;
        }
    }

    public void ToggleWindow()
    {
        if (windowPanel != null)
        {
            windowPanel.SetActive(!windowPanel.activeSelf);
            if (windowPanel.activeSelf) RefreshUI();
        }
    }

    // Call this from a button in GoalUI or Main UI
    public void OpenWindow()
    {
        if (windowPanel != null)
        {
            windowPanel.SetActive(true);
            RefreshUI();
        }
    }
    
    public void CloseWindow()
    {
        if (windowPanel != null) windowPanel.SetActive(false);
    }

    private void RefreshUI()
    {
        if (windowPanel != null && !windowPanel.activeSelf) return; // Optimization
        if (GoalManager.Instance == null) return;
        
        if (_activeSlots != null)
        {
            foreach (var slot in _activeSlots)
            {
                slot.RefreshUI();
            }
        }
    }

    public void PinMilestone(int index)
    {
        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.PinnedGoalIndex = index;
            // Force refresh of both this UI and HUD
            // We can hackishly invoke the event or just call update
            RefreshUI();
            
            // HUD needs update too. GoalManager event does that if we could trigger it,
            // but PinnedGoalIndex is local. 
            // So we can manually find HUD or add a Local events system.
            // Easiest: Just force GoalUI update via GoalManager event if allowed? 
            // Better: Dispatch a local event or let GoalUI update every frame (less efficient).
            
            // Simulating a state change usually triggers updates. 
            // Best way: GoalManager exposes 'OnGoalUpdated' which we can reuse
            // But we can't invoke it from outside if it's an event.
            // Let's just assume GoalUI listens to some update or we call it.
            
            // Actually, PinnedGoalIndex change doesn't trigger OnGoalUpdated in GoalManager
            // because it's a simple auto-property. 
            // Let's rely on valid "UpdateUI" calls or public method.
            if (GoalUI.Instance != null) GoalUI.Instance.ForceUpdateUI();
        }
    }
}
