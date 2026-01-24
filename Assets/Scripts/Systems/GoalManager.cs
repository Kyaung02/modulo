using UnityEngine;
using System;

public class GoalManager : MonoBehaviour
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
    public int currentLevelIndex = 0;
    public int currentDeliverCount = 0;

    public event Action OnGoalUpdated; // UI update event
    public event Action OnLevelComplete;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        // Initial Update
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
        LevelGoal goal = GetCurrentGoal();
        
        // Check if submitted word matches target
        if (word == goal.targetWord)
        {
            currentDeliverCount++;
            OnGoalUpdated?.Invoke();

            if (currentDeliverCount >= goal.requiredCount)
            {
                CompleteLevel();
            }
        }
    }

    private void CompleteLevel()
    {
        Debug.Log($"Level {currentLevelIndex} Complete!");
        
        currentLevelIndex++;
        currentDeliverCount = 0;
        
        OnLevelComplete?.Invoke();
        OnGoalUpdated?.Invoke();
        
        if (currentLevelIndex >= levels.Length)
        {
            Debug.Log("All Levels Completed! You are a master of words.");
        }
    }
}
