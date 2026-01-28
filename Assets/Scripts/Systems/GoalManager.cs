using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.Events;

public class GoalManager : NetworkBehaviour
{
    public static GoalManager Instance { get; private set; }

    [System.Serializable]
    public class LevelGoal
    {
        public WordData targetWord;
        public int requiredCount;
        public List<int> prerequisiteIndices; // Indices of goals that must be completed first
        public UnityEvent onComplete;
    }

    [Header("Settings")]
    public LevelGoal[] levels; // Rename conceptually to "All Goals" but keep name for Inspector link preservation

    [Header("State")]
    private NetworkList<int> _goalProgressCounts; // Stores progress count for each level index
    private NetworkList<int> _completedGoalIndices;

    // Local UI state
    public int PinnedGoalIndex { get; set; } = -1;

    public event Action OnGoalUpdated; // UI update event
    public event Action OnLevelComplete;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        _goalProgressCounts = new NetworkList<int>();
        _completedGoalIndices = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[GoalManager] OnNetworkSpawn Called. IsServer: {IsServer}");
        _goalProgressCounts.OnListChanged += OnProgressChanged;
        _completedGoalIndices.OnListChanged += OnCompletedListChanged;
        
        ComponentsUnlocked.OnListChanged += OnUnlockChanged;
        
        if (IsServer)
        {
             InitLock();
             // Ensure progress list size matches levels
             if (levels != null)
             {
                 for (int i = 0; i < levels.Length; i++)
                 {
                     if (i >= _goalProgressCounts.Count) _goalProgressCounts.Add(0);
                 }
             }
        }

        OnGoalUpdated?.Invoke(); // Initial sync
    }

    public override void OnNetworkDespawn()
    {
        _goalProgressCounts.OnListChanged -= OnProgressChanged;
        _completedGoalIndices.OnListChanged -= OnCompletedListChanged;
        ComponentsUnlocked.OnListChanged -= OnUnlockChanged;
    }

    private void OnProgressChanged(NetworkListEvent<int> changeEvent)
    {
        OnGoalUpdated?.Invoke();
    }

    private void OnCompletedListChanged(NetworkListEvent<int> changeEvent)
    {
        OnGoalUpdated?.Invoke();
        OnLevelComplete?.Invoke();

        // Fire local events for everyone (Server & Client) when a goal is officially marked complete
        if (changeEvent.Type == NetworkListEvent<int>.EventType.Add)
        {
            // Do not fire events during RestoreState to prevent duplicates
            if (IsRestoring) return;

            int index = changeEvent.Value;
            if (levels != null && index >= 0 && index < levels.Length)
            {
                Debug.Log($"[GoalManager] Goal {index} Unlocked/Completed Event Fired (Local)");
                levels[index].onComplete?.Invoke();
            }
        }
    }

    /// <summary>
    /// Returns the locally pinned goal for UI display
    /// </summary>
    public LevelGoal GetPinnedGoal()
    {
        if (levels != null && PinnedGoalIndex >= 0 && PinnedGoalIndex < levels.Length)
        {
            return levels[PinnedGoalIndex];
        }
        return null;
    }

    public int GetProgress(int index)
    {
        if (index >= 0 && index < _goalProgressCounts.Count)
            return _goalProgressCounts[index];
        return 0;
    }

    public bool IsGoalCompleted(int index)
    {
        return _completedGoalIndices.Contains(index);
    }

    public bool IsGoalUnlocked(int index)
    {
        if (index < 0 || index >= levels.Length) return false;
        
        LevelGoal goal = levels[index];
        if (goal.prerequisiteIndices == null || goal.prerequisiteIndices.Count == 0) return true;

        foreach (int reqIndex in goal.prerequisiteIndices)
        {
            if (!_completedGoalIndices.Contains(reqIndex)) return false;
        }
        return true;
    }

    public void SubmitWord(WordData word)
    {
        // Only Server verifies goals
        if (!IsServer) return; 

        if (levels == null) return;

        // Check against ALL unlocked and incomplete goals
        for (int i = 0; i < levels.Length; i++)
        {
            if (IsGoalCompleted(i)) continue;
            if (!IsGoalUnlocked(i)) continue;

            LevelGoal goal = levels[i];
            if (word == goal.targetWord)
            {
                // Increment progress
                if (i < _goalProgressCounts.Count)
                {
                    int current = _goalProgressCounts[i];
                    _goalProgressCounts[i] = current + 1;

                    if (_goalProgressCounts[i] >= goal.requiredCount)
                    {
                        CompleteGoal(i);
                    }
                }
            }
        }
    }

    private void CompleteGoal(int index)
    {
        // Server Only logic calling this
        Debug.Log($"Goal {index} Complete!");
        
        // Invoke Level-specific callback


        if (!_completedGoalIndices.Contains(index))
            _completedGoalIndices.Add(index);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DebugCompleteGoalServerRpc(int index)
    {
        if (index < 0 || index >= levels.Length) return;
        
        // Fill progress
        if (index < _goalProgressCounts.Count)
        {
            _goalProgressCounts[index] = levels[index].requiredCount;
        }
        
        // Trigger completion
        if (!IsGoalCompleted(index))
        {
            CompleteGoal(index);
        }
    }
    
    public bool IsRestoring { get; private set; }

    /// <summary>
    /// 저장된 상태 복원 (서버 전용)
    /// </summary>
    public void RestoreState(List<int> progressList, List<int> completedGoals)
    {
        if (!IsServer) return;
        
        IsRestoring = true;
        try
        {
            _goalProgressCounts.Clear();
            if (progressList != null)
            {
                foreach(var p in progressList) _goalProgressCounts.Add(p);
            }
            // Resize if needed (e.g. game updated with more levels)
            if (levels != null)
            {
                while (_goalProgressCounts.Count < levels.Length) _goalProgressCounts.Add(0);
            }
            
            _completedGoalIndices.Clear();
            if (completedGoals != null)
            {
                foreach(var g in completedGoals) _completedGoalIndices.Add(g);
            }

            Debug.Log($"[GoalManager] State restored. Completed: {_completedGoalIndices.Count}");

            // Re-trigger completion events to restore game state (unlocks, bools, etc.)
            foreach (int index in _completedGoalIndices)
            {
                if (levels != null && index >= 0 && index < levels.Length)
                {
                    //Debug.Log($"[GoalManager] Restoring completion effect for goal {index}");
                }
            }
        }
        finally
        {
            IsRestoring = false;
        }
    }

    private NetworkList<int> ComponentsUnlocked = new NetworkList<int>();
    
    // Callback for NetworkList changes (Clients + Server)
    private void OnUnlockChanged(NetworkListEvent<int> changeEvent)
    {
        if (BuildUI.Instance != null && BuildUI.Instance.isActiveAndEnabled)
        {
            BuildUI.Instance.CreateSlots();
        }
    }

    public void InitLock()
    {
        ComponentsUnlocked.Clear();
        if (BuildManager.Instance != null && BuildManager.Instance.availableComponents != null)
        {
            for(int i=0;i<BuildManager.Instance.availableComponents.Length;i++)
            {
                ComponentsUnlocked.Add(0);
            }
        }
        UnlockComponent(0);
        UnlockComponent(1);
        Debug.Log("Unlockables Initialized");
    }
    public void UnlockComponent(int componentId)
    {
        if (!IsServer) return;
        if(componentId < ComponentsUnlocked.Count)
            ComponentsUnlocked[componentId] = 1;
        
        if (BuildUI.Instance != null && BuildUI.Instance.isActiveAndEnabled)
            BuildUI.Instance.CreateSlots();
    }
    public int CheckUnlock(int componentId)
    {
        if(componentId>=ComponentsUnlocked.Count) return 0;
        return ComponentsUnlocked[componentId];
    }
}
