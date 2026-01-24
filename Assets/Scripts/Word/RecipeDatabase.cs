using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeDatabase", menuName = "Modulo/Recipe Database")]
public class RecipeDatabase : ScriptableObject
{
    public List<RecipeData> recipes = new List<RecipeData>();

    // Optimization: Dictionary for O(1) lookup
    // Key: Combined hash or tuple of inputs, Value: Output WordData
    private Dictionary<(WordData, WordData), WordData> _recipeCache;

    public void Initialize()
    {
        _recipeCache = new Dictionary<(WordData, WordData), WordData>();

        foreach (var recipe in recipes)
        {
            if (recipe.inputA != null && recipe.inputB != null && recipe.output != null)
            {
                // Register both A+B and B+A to handle commutative property
                AddToCache(recipe.inputA, recipe.inputB, recipe.output);
                AddToCache(recipe.inputB, recipe.inputA, recipe.output);
            }
        }
        
        Debug.Log($"RecipeDatabase initialized with {recipes.Count} recipes.");
    }

    private void AddToCache(WordData a, WordData b, WordData result)
    {
        if (!_recipeCache.ContainsKey((a, b)))
        {
            _recipeCache.Add((a, b), result);
        }
    }

    public WordData GetOutput(WordData inputA, WordData inputB)
    {
        if (_recipeCache == null)
        {
            Initialize();
        }

        if (inputA == null || inputB == null) return null;

        if (_recipeCache.TryGetValue((inputA, inputB), out WordData output))
        {
            return output;
        }

        return null;
    }
}
