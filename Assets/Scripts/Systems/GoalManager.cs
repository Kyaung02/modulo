using UnityEngine;
using System;
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
        public UnityEvent onComplete;
    }

    [Header("Settings")]
    public LevelGoal[] levels; // Define levels in Inspector

    [Header("State")]
    private NetworkVariable<int> _netLevelIndex = new NetworkVariable<int>(0);
    private NetworkVariable<int> _netDeliverCount = new NetworkVariable<int>(0);

    public int currentLevelIndex => _netLevelIndex.Value;
    public int currentDeliverCount => _netDeliverCount.Value;

    public event Action OnGoalUpdated; // UI update event
    public event Action OnLevelComplete;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[GoalManager] OnNetworkSpawn Called. IsServer: {IsServer}");
        _netLevelIndex.OnValueChanged += OnStateChanged;
        _netDeliverCount.OnValueChanged += OnStateChanged;
        
        ComponentsUnlocked.OnListChanged += OnUnlockChanged;
        
        if (IsServer)
        {
             InitLock();
        }

        OnGoalUpdated?.Invoke(); // Initial sync
    }

    public override void OnNetworkDespawn()
    {
        _netLevelIndex.OnValueChanged -= OnStateChanged;
        _netDeliverCount.OnValueChanged -= OnStateChanged;
        ComponentsUnlocked.OnListChanged -= OnUnlockChanged;
    }

    private void OnStateChanged(int oldVal, int newVal)
    {
        OnGoalUpdated?.Invoke();
    }

    public LevelGoal GetCurrentGoal()
    {
        if (levels != null && currentLevelIndex < levels.Length)
        {
            return levels[currentLevelIndex];
        }
        return new LevelGoal(); // Empty if completed all
    }

    public void SubmitWord(WordData word)
    {
        if (!IsServer) return; // Only Server verifies goals

        LevelGoal goal = GetCurrentGoal();
        
        // Check if submitted word matches target
        if (word == goal.targetWord)
        {
            _netDeliverCount.Value++;
            // OnGoalUpdated invoked via NetworkVariable callback

            if (_netDeliverCount.Value >= goal.requiredCount)
            {
                CompleteLevel();
            }
        }
    }

    private void CompleteLevel()
    {
        // Server Only logic calling this
        Debug.Log($"Level {currentLevelIndex} Complete!");
        
        // Invoke Level-specific callback
        if (levels != null && currentLevelIndex < levels.Length)
        {
            levels[currentLevelIndex].onComplete?.Invoke();
        }
        
        _netLevelIndex.Value++;
        _netDeliverCount.Value = 0;
        
        NotifyLevelCompleteClientRpc();
    }
    
    [ClientRpc]
    private void NotifyLevelCompleteClientRpc()
    {
        OnLevelComplete?.Invoke();
    }
    
    /// <summary>
    /// 저장된 상태 복원 (서버 전용)
    /// </summary>
    public void SetState(int levelIndex, int deliverCount)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[GoalManager] SetState can only be called on Server!");
            return;
        }
        
        _netLevelIndex.Value = levelIndex;
        _netDeliverCount.Value = deliverCount;
        Debug.Log($"[GoalManager] State restored: Level {levelIndex}, Count {deliverCount}");
    }

    private NetworkList<int> ComponentsUnlocked = new NetworkList<int>();
    
    // Callback for NetworkList changes (Clients + Server)
    private void OnUnlockChanged(NetworkListEvent<int> changeEvent)
    {
        // Whenever the unlock list changes (Init or Update), refresh UI
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
        ComponentsUnlocked[componentId] = 1;
        BuildUI.Instance.CreateSlots();
    }
    public int CheckUnlock(int componentId)
    {
        if(componentId>=ComponentsUnlocked.Count) return 0;
        return ComponentsUnlocked[componentId];
    }
}
