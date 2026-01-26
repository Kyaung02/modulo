using UnityEngine;
using System;
using Unity.Netcode;

public class GoalManager : NetworkBehaviour
{
    public static GoalManager Instance { get; private set; }

    [System.Serializable]
    public struct LevelGoal
    {
        public WordData targetWord;
        public int requiredCount;
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
        _netLevelIndex.OnValueChanged += OnStateChanged;
        _netDeliverCount.OnValueChanged += OnStateChanged;
        
        OnGoalUpdated?.Invoke(); // Initial sync
    }

    public override void OnNetworkDespawn()
    {
        _netLevelIndex.OnValueChanged -= OnStateChanged;
        _netDeliverCount.OnValueChanged -= OnStateChanged;
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
        
        _netLevelIndex.Value++;
        _netDeliverCount.Value = 0;
        
        NotifyLevelCompleteClientRpc();
    }
    
    [ClientRpc]
    private void NotifyLevelCompleteClientRpc()
    {
        OnLevelComplete?.Invoke();
    }

}
