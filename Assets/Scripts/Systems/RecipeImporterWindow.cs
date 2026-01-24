#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class RecipeImporterWindow : EditorWindow
{
    [MenuItem("Modulo/Recipe Importer")]
    public static void ShowWindow()
    {
        GetWindow<RecipeImporterWindow>("Recipe Importer");
    }

    public TextAsset recipesJson;
    public RecipeDatabase targetDatabase;
    public string targetKeywords = ""; // Comma separated targets
    public int maxGenerations = 3;
    public string targetFolder = "Assets/Resources/ScriptableObject/Words";

    // BFS State
    private HashSet<string> knownWords;
    
    private void OnGUI()
    {
        GUILayout.Label("Infinite Craft Recipe Importer", EditorStyles.boldLabel);
        
        recipesJson = (TextAsset)EditorGUILayout.ObjectField("Recipes JSON", recipesJson, typeof(TextAsset), false);
        targetDatabase = (RecipeDatabase)EditorGUILayout.ObjectField("Target Database", targetDatabase, typeof(RecipeDatabase), false);
        maxGenerations = EditorGUILayout.IntSlider("Generations", maxGenerations, 1, 10);
        targetKeywords = EditorGUILayout.TextField("Target Keywords (Optional)", targetKeywords);
        targetFolder = EditorGUILayout.TextField("Target Folder", targetFolder);

        if (GUILayout.Button("Import & Generate"))
        {
            if (recipesJson && targetDatabase)
            {
                Import();
            }
        }
    }

    // ... (Inner classes same)

    private void Import()
    {
        string json = recipesJson.text;
        
        // 1. Parse Items (Names)
        ItemsWrapper itemWrapper = JsonUtility.FromJson<ItemsWrapper>(json);
        if (itemWrapper == null || itemWrapper.items == null || itemWrapper.items.Length == 0) return;
        string[] allItemNames = itemWrapper.items;

        // 2. Parse Recipes (Index Triplets)
        List<int[]> allRecipes = new List<int[]>();
        int recipesIndex = json.IndexOf("\"recipes\"");
        if (recipesIndex == -1) return;

        int arrayStart = json.IndexOf('[', recipesIndex);
        int arrayEnd = json.LastIndexOf(']');
        
        if (arrayStart != -1 && arrayEnd > arrayStart)
        {
            string recipesContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            string[] recipeStrings = recipesContent.Split(']');
            foreach (var rs in recipeStrings)
            {
                string clean = rs.Replace("[", "").Replace(",", " ").Trim();
                if (string.IsNullOrEmpty(clean)) continue;
                string[] parts = clean.Split(new char[]{' '}, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int b) && int.TryParse(parts[2], out int c))
                {
                    allRecipes.Add(new int[]{a, b, c});
                }
            }
        }
        
        Debug.Log($"Loaded {allRecipes.Count} recipes.");

        // 3. Logic Selection: Target Backtracking OR Generation BFS
        if (!string.IsNullOrEmpty(targetKeywords))
        {
            ImportTargets(targetKeywords, allItemNames, allRecipes);
        }
        else
        {
            ImportGenerations(allItemNames, allRecipes);
        }
        
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(targetDatabase);
    }

    private void ImportTargets(string keywords, string[] allItemNames, List<int[]> allRecipes)
    {
        // 1. Identify Target IDs
        List<string> targets = keywords.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        HashSet<int> targetIndices = new HashSet<int>();
        Dictionary<string, int> nameToId = new Dictionary<string, int>();
        
        for(int i=0; i<allItemNames.Length; i++) nameToId[allItemNames[i]] = i;
        
        foreach(var t in targets)
        {
            if(nameToId.TryGetValue(t, out int id)) targetIndices.Add(id);
            else Debug.LogWarning($"Target '{t}' not found in JSON.");
        }

        // 2. Valid Recipes Map (Output -> List of Recipes)
        Dictionary<int, List<int[]>> outputToRecipes = new Dictionary<int, List<int[]>>();
        foreach(var r in allRecipes)
        {
            if(!outputToRecipes.ContainsKey(r[2])) outputToRecipes[r[2]] = new List<int[]>();
            outputToRecipes[r[2]].Add(r);
        }

        // 3. Backtracking (Find dependencies)
        HashSet<int> requiredWords = new HashSet<int>();
        HashSet<string> requiredRecipesSignature = new HashSet<string>(); // "A,B,R" to avoid duplicate recipe entries in list
        List<int[]> recipesToImport = new List<int[]>();
        Queue<int> toVisit = new Queue<int>(targetIndices);

        // Include Base 4 always
        int[] baseIds = new int[] {-1,-1,-1,-1};
        if(nameToId.ContainsKey("Water")) baseIds[0] = nameToId["Water"];
        if(nameToId.ContainsKey("Fire")) baseIds[1] = nameToId["Fire"];
        if(nameToId.ContainsKey("Wind")) baseIds[2] = nameToId["Wind"];
        if(nameToId.ContainsKey("Earth")) baseIds[3] = nameToId["Earth"];

        foreach(var b in baseIds) if(b!=-1) requiredWords.Add(b);

        while(toVisit.Count > 0)
        {
            int current = toVisit.Dequeue();
            if(requiredWords.Contains(current) && !targetIndices.Contains(current)) continue; // Already processed dependencies
            requiredWords.Add(current);
            
            // If it's a base element, stop
            if(baseIds.Contains(current)) continue;

            // Find a recipe to make 'current'
            // Heuristic: Shortest recipe? First one?
            // Infinite Craft has "First Discovery" but we just want ANY valid path.
            // We pick the first valid recipe we find.
            if(outputToRecipes.ContainsKey(current))
            {
                var recipes = outputToRecipes[current];
                if(recipes.Count > 0)
                {
                    // Pick the first one for simplicity (or try to optimize depth?)
                    // For now, Pick [0]
                    var r = recipes[0];
                    
                    // Add dependencies
                    if(!requiredWords.Contains(r[0])) toVisit.Enqueue(r[0]);
                    if(!requiredWords.Contains(r[1])) toVisit.Enqueue(r[1]);
                    
                    // Store this recipe
                    string sig = $"{r[0]},{r[1]},{r[2]}";
                    if(!requiredRecipesSignature.Contains(sig))
                    {
                        requiredRecipesSignature.Add(sig);
                        recipesToImport.Add(r);
                    }
                }
            }
        }

        Debug.Log($"Backtracking complete. Needs {requiredWords.Count} words and {recipesToImport.Count} recipes.");

        // 4. Generate Assets and Register
        Dictionary<int, WordData> indexToWord = new Dictionary<int, WordData>();
        
        foreach(var idx in requiredWords)
        {
            string n = allItemNames[idx];
            string emoji = "📦";
            Color col = Color.white;
            if (n == "Water") { emoji = "💧"; col = Color.blue; }
            if (n == "Fire") { emoji = "🔥"; col = Color.red; }
            if (n == "Wind") { emoji = "💨"; col = Color.cyan; }
            if (n == "Earth") { emoji = "🌍"; col = new Color(0.6f, 0.4f, 0.2f); }
            else if (!string.IsNullOrEmpty(n)) emoji = n.Substring(0,1);

            indexToWord[idx] = EnsureWord(n, emoji, col);
        }

        foreach(var r in recipesToImport)
        {
            if(indexToWord.ContainsKey(r[0]) && indexToWord.ContainsKey(r[1]) && indexToWord.ContainsKey(r[2]))
            {
                RegisterRecipe(indexToWord[r[0]], indexToWord[r[1]], indexToWord[r[2]]);
            }
        }
    }

    private void ImportGenerations(string[] allItemNames, List<int[]> allRecipes)
    {
        // ... (Previous logic moved here)
        Dictionary<int, WordData> indexToWord = new Dictionary<int, WordData>();
        HashSet<int> knownIndices = new HashSet<int>();
        
        // Base Indices
        for (int i = 0; i < allItemNames.Length; i++)
        {
            string n = allItemNames[i];
            if (n == "Water" || n == "Fire" || n == "Wind" || n == "Earth")
            {
                knownIndices.Add(i);
                string emoji = "📦";
                Color col = Color.white;
                if (n == "Water") { emoji = "💧"; col = Color.blue; }
                if (n == "Fire") { emoji = "🔥"; col = Color.red; }
                if (n == "Wind") { emoji = "💨"; col = Color.cyan; }
                if (n == "Earth") { emoji = "🌍"; col = new Color(0.6f, 0.4f, 0.2f); }
                indexToWord[i] = EnsureWord(n, emoji, col);
            }
        }

        // BFS
        for (int gen = 0; gen < maxGenerations; gen++)
        {
            int newWordsInGen = 0;
            HashSet<int> nextGenIndices = new HashSet<int>(knownIndices);
            
            foreach (var r in allRecipes)
            {
                if (knownIndices.Contains(r[0]) && knownIndices.Contains(r[1]))
                {
                    if (!knownIndices.Contains(r[2]))
                    {
                        string name = allItemNames[r[2]];
                        string emoji = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
                        indexToWord[r[2]] = EnsureWord(name, emoji, Color.white);
                        nextGenIndices.Add(r[2]);
                        newWordsInGen++;
                    }
                    if (indexToWord.ContainsKey(r[0]) && indexToWord.ContainsKey(r[1]) && indexToWord.ContainsKey(r[2]))
                    {
                        RegisterRecipe(indexToWord[r[0]], indexToWord[r[1]], indexToWord[r[2]]);
                    }
                }
            }
            knownIndices = nextGenIndices;
            if (newWordsInGen == 0) break;
        }
    }
    
    [System.Serializable]
    public class ItemsWrapper
    {
        public string[] items;
    }
    
    private WordData EnsureWord(string name, string defaultEmoji, Color defaultColor)
    {
        string path = $"{targetFolder}/{name}.asset";
        WordData word = AssetDatabase.LoadAssetAtPath<WordData>(path);
        
        if (word == null)
        {
            // Create New
            word = ScriptableObject.CreateInstance<WordData>();
            word.id = name;
            word.wordName = name;
            word.emoji = defaultEmoji;
            word.wordColor = defaultColor;
            
            // Ensure directory
            Directory.CreateDirectory(targetFolder);
            
            AssetDatabase.CreateAsset(word, path);
        }
        else
        {
            // Update existing if emoji missing (optional)
            if (string.IsNullOrEmpty(word.emoji)) word.emoji = defaultEmoji;
        }

        return word;
    }

    private void RegisterRecipe(WordData wa, WordData wb, WordData wr)
    {
        if (wa == null || wb == null || wr == null) return;

        SerializedObject so = new SerializedObject(targetDatabase);
        SerializedProperty recipesProp = so.FindProperty("recipes");
        
        int index = recipesProp.arraySize;
        recipesProp.InsertArrayElementAtIndex(index);
        SerializedProperty newItem = recipesProp.GetArrayElementAtIndex(index);
        
        newItem.FindPropertyRelative("inputA").objectReferenceValue = wa;
        newItem.FindPropertyRelative("inputB").objectReferenceValue = wb;
        newItem.FindPropertyRelative("output").objectReferenceValue = wr;
        
        so.ApplyModifiedPropertiesWithoutUndo(); // Faster
    }
}
#endif
