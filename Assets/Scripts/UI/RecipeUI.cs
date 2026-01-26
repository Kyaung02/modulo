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

        // Default closed
        SetOpen(false);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        
        // KeyCode를 Key로 변환
        Key key = (Key)toggleKey;
        
        // G 키를 직접 확인 (디버깅용)
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            // Debug.Log("G key pressed - Toggling Recipe UI");
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

        // Populate
        foreach (var recipe in recipeDatabase.recipes)
        {
            // Basic validation
            if (recipe.inputA == null || recipe.inputB == null || recipe.output == null) continue;

            // Prevent duplicate entries for same output if database has duplicates
            // But we might want to show multiple ways to make something? For now assuming 1 way.
            
            GameObject newRow = Instantiate(rowPrefab, listContent);
            RecipeRowUI rowUI = newRow.GetComponent<RecipeRowUI>();
            if (rowUI != null)
            {
                rowUI.SetRecipe(recipe.inputA, recipe.inputB, recipe.output);
            }
            
            _spawnedRows.Add(newRow);
            
            // Map the OUTPUT word to this row
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
