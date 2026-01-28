using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class RecipeUI : MonoBehaviour
{
    public static RecipeUI Instance { get; private set; }

    [Header("References")]
    public RecipeDatabase recipeDatabase;
    public Transform listContent; // ScrollView Content Root
    public GameObject rowPrefab; // Prefab with RecipeRowUI component
    public ScrollRect scrollRect; // Reference to ScrollRect for scrolling

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.G;

    [Header("State")]
    private bool _isOpen = false;
    private List<GameObject> _spawnedRows = new List<GameObject>();
    private Dictionary<WordData, RectTransform> _recipeRowMap = new Dictionary<WordData, RectTransform>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Try to auto-find database if missing
        if (recipeDatabase == null && ModuleManager.Instance != null)
        {
            recipeDatabase = ModuleManager.Instance.recipeDatabase;
        }
        
        if (scrollRect == null)
        {
           scrollRect = GetComponentInChildren<ScrollRect>();
        }

        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.OnGoalUpdated += OnGoalUpdated;
        }

        // Default closed
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.OnGoalUpdated -= OnGoalUpdated;
        }
    }

    private void OnGoalUpdated()
    {
        if (_isOpen) RefreshList();
    }

    private void Update()
    {
        if (KeybindingManager.Instance != null && KeybindingManager.Instance.GetKeyDown(GameAction.ToggleRecipe))
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        SetOpen(!_isOpen);
    }

    public void SetOpen(bool open)
    {
        _isOpen = open;
        
        // Show/Hide Child Visuals (Keep script active for Update loop)
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(_isOpen);
        }

        if (_isOpen)
        {
            RefreshList();
        }
    }

    public void RefreshList()
    {
        if (recipeDatabase == null) return;
        if (listContent == null) return;
        if (rowPrefab == null) return;

        // Clear existing
        foreach (var obj in _spawnedRows)
        {
            Destroy(obj);
        }
        _spawnedRows.Clear();
        _recipeRowMap.Clear();

        // 1. Collect all root words (Unlocked Milestone Targets)
        HashSet<WordData> wordsToProcess = new HashSet<WordData>();
        if (GoalManager.Instance != null && GoalManager.Instance.levels != null)
        {
            for(int i=0; i < GoalManager.Instance.levels.Length; i++)
            {
                if (GoalManager.Instance.IsGoalUnlocked(i))
                {
                    var goal = GoalManager.Instance.levels[i];
                    if (goal.targetWord != null)
                    {
                        wordsToProcess.Add(goal.targetWord);
                    }
                }
            }
        }
        
        // 2. Recursive dependency search
        // We want to show recipes for 'wordsToProcess' AND their ingredients, recursively down.
        
        HashSet<RecipeData> recipesToShow = new HashSet<RecipeData>();
        Queue<WordData> queue = new Queue<WordData>(wordsToProcess);
        HashSet<WordData> visitedWords = new HashSet<WordData>(wordsToProcess); // Avoid infinite loops

        while (queue.Count > 0)
        {
            WordData currentOutput = queue.Dequeue();
            
            // Find recipes that produce this output
            foreach (var recipe in recipeDatabase.recipes)
            {
                if (recipe.output == currentOutput)
                {
                    // Add this recipe
                    if (!recipesToShow.Contains(recipe))
                    {
                        recipesToShow.Add(recipe);
                        
                        // Add inputs to queue if not visited
                        if (recipe.inputA != null && !visitedWords.Contains(recipe.inputA))
                        {
                            visitedWords.Add(recipe.inputA);
                            queue.Enqueue(recipe.inputA);
                        }
                        if (recipe.inputB != null && !visitedWords.Contains(recipe.inputB))
                        {
                            visitedWords.Add(recipe.inputB);
                            queue.Enqueue(recipe.inputB);
                        }
                    }
                }
            }
        }

        // 3. Populate UI
        foreach (var recipe in recipesToShow)
        {
            if (recipe.inputA == null || recipe.inputB == null || recipe.output == null) continue;

            GameObject newRow = Instantiate(rowPrefab, listContent);
            RecipeRowUI rowUI = newRow.GetComponent<RecipeRowUI>();
            if (rowUI != null)
            {
                rowUI.SetRecipe(recipe.inputA, recipe.inputB, recipe.output);
            }
            
            _spawnedRows.Add(newRow);
            
            if (!_recipeRowMap.ContainsKey(recipe.output))
            {
                _recipeRowMap.Add(recipe.output, newRow.GetComponent<RectTransform>());
            }
        }
    }

    public void ShowRecipe(WordData targetWord)
    {
        if (targetWord == null) return;

        // Open UI if closed
        if (!_isOpen)
        {
            SetOpen(true);
        }

        // Find the row
        if (_recipeRowMap.TryGetValue(targetWord, out RectTransform targetRow))
        {
            // Scroll to it
            SnapTo(targetRow);
            
            // Highlight effect
            RecipeRowUI rowUI = targetRow.GetComponent<RecipeRowUI>();
            if (rowUI != null)
            {
                rowUI.Highlight();
            }
        }
        else
        {
            Debug.Log($"No recipe found for {targetWord.wordName}");
        }
    }

    private void SnapTo(RectTransform target)
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();

        // Calculate the top Y position of the target relative to the content
        // Assuming content grows downwards (negative Y)
        // target.localPosition.y is the pivot position.
        // We want the Top edge.
        // Top = localY + (height * (1 - pivot.y))
        
        float heightOffset = target.rect.height * (1f - target.pivot.y);
        float targetTopY = target.localPosition.y + heightOffset;

        // Content Y position should be such that targetTopY is at Viewport Top (0 usually)
        // content.anchoredPosition.y = -targetTopY;
        
        // Use a simpler Vector2 assignment retaining X
        listContent.GetComponent<RectTransform>().anchoredPosition = 
            new Vector2(listContent.GetComponent<RectTransform>().anchoredPosition.x, 
                        -targetTopY);
    }
}
