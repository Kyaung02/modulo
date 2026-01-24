#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class RecipeCsvImporterWindow : EditorWindow
{
    [MenuItem("Modulo/Recipe CSV Importer")]
    public static void ShowWindow()
    {
        GetWindow<RecipeCsvImporterWindow>("Recipe CSV Importer");
    }

    public TextAsset csvFile;
    public RecipeDatabase targetDatabase;
    public string targetFolder = "Assets/Resources/ScriptableObject/Words";

    private void OnGUI()
    {
        GUILayout.Label("CSV Recipe Importer", EditorStyles.boldLabel);
        
        csvFile = (TextAsset)EditorGUILayout.ObjectField("Recipe CSV", csvFile, typeof(TextAsset), false);
        targetDatabase = (RecipeDatabase)EditorGUILayout.ObjectField("Target Database", targetDatabase, typeof(RecipeDatabase), false);
        targetFolder = EditorGUILayout.TextField("Target Folder", targetFolder);

        if (GUILayout.Button("Import from CSV"))
        {
            if (csvFile && targetDatabase)
            {
                ImportCsv();
            }
            else
            {
                Debug.LogError("Assign CSV file and Target Database first!");
            }
        }
        
        GUILayout.Label("Note: CSV format should be 'Result,InputA,InputB'", EditorStyles.helpBox);
    }

    private void ImportCsv()
    {
        string[] lines = csvFile.text.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        // Dictionary to populate WordData cache
        Dictionary<string, WordData> nameToAsset = new Dictionary<string, WordData>();
        int newWords = 0;
        int newRecipes = 0;

        // 1. First Pass: Collect all unique words & Create Assets
        HashSet<string> allWords = new HashSet<string>();
        
        // Base elements check
        string[] bases = { "Water", "Fire", "Wind", "Earth" };
        foreach(var b in bases) allWords.Add(b);

        List<string[]> validRows = new List<string[]>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if(string.IsNullOrEmpty(line)) continue;
            
            // Allow Header skip if needed (detect "Input" or "First")
            if (i == 0 && (line.ToLower().Contains("first") || line.ToLower().Contains("input") || line.ToLower().Contains("result"))) continue;

            // Split by comma, handling quotes if necessary (Simplified split)
            string[] parts = line.Split(',');
            if (parts.Length >= 3)
            {
                // Format: Result, InputA, InputB
                string r = parts[0].Trim();
                string a = parts[1].Trim();
                string b = parts[2].Trim();
                
                if(!string.IsNullOrEmpty(a)) allWords.Add(a);
                if(!string.IsNullOrEmpty(b)) allWords.Add(b);
                if(!string.IsNullOrEmpty(r)) allWords.Add(r);
                
                validRows.Add(new string[]{r, a, b});
            }
        }

        Debug.Log($"Found {allWords.Count} unique words in CSV.");

        // Create Assets
        foreach (string w in allWords)
        {
            // Sprite matching
            Sprite icon = GetSpriteForWord(w);
            Color col = GetColorForWord(w);
            
            WordData asset = EnsureWord(w, icon, col);
            nameToAsset[w] = asset;
            if (asset != null) newWords++; 
        }

        // 2. Second Pass: Register Recipes
        foreach (var row in validRows)
        {
            string r = row[0];
            string a = row[1];
            string b = row[2];

            if (nameToAsset.TryGetValue(a, out WordData wa) &&
                nameToAsset.TryGetValue(b, out WordData wb) &&
                nameToAsset.TryGetValue(r, out WordData wr))
            {
                RegisterRecipe(wa, wb, wr);
                newRecipes++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(targetDatabase);
        Debug.Log($"CSV Import Complete! Processed {lines.Length} lines. Created/Updated Words: {allWords.Count}, Registered Recipes: {newRecipes}");
    }

    private Sprite GetSpriteForWord(string word)
    {
        // Try strict load from Assets/Sprites/Emojis
        // This relies on the file name matching the word (Input "Fire" -> "Fire.png")
        // User downloaded emojis using "Title Case" names in previous step script, so it should match.
        string path = $"Assets/Sprites/Emojis/{word}.png";
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        
        if (s == null)
        {
            // Try lowercase if failed
            path = $"Assets/Sprites/Emojis/{word.ToLower()}.png";
            s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        
        return s;
    }

    private Color GetColorForWord(string word)
    {
        string lower = word.ToLower();
        if (lower == "water") return Color.blue;
        if (lower == "fire") return Color.red;
        if (lower == "wind") return Color.cyan;
        if (lower == "earth") return new Color(0.6f, 0.4f, 0.2f);
        
        int hash = word.GetHashCode();
        Random.InitState(hash);
        return Color.HSVToRGB(Random.value, 0.6f, 0.9f); 
    }

    private WordData EnsureWord(string name, Sprite icon, Color defaultColor)
    {
        // Sanitize name for filename
        string invalid = new string(Path.GetInvalidFileNameChars());
        string safeName = name;
        foreach (char c in invalid) safeName = safeName.Replace(c, '_');
        
        string path = $"{targetFolder}/{safeName}.asset";
        WordData word = AssetDatabase.LoadAssetAtPath<WordData>(path);
        
        if (word == null)
        {
            word = ScriptableObject.CreateInstance<WordData>();
            word.id = name;
            word.wordName = name;
            word.emoji = ""; // Clear emoji or set to ? if needed
            word.wordIcon = icon;
            word.wordColor = defaultColor;
            
            Directory.CreateDirectory(targetFolder);
            AssetDatabase.CreateAsset(word, path);
        }
        else
        {
            // Update existing if icon missing
            if (word.wordIcon == null) word.wordIcon = icon;
        }
        return word;
    }

    private void RegisterRecipe(WordData wa, WordData wb, WordData wr)
    {
        if (!targetDatabase) return;
        
        SerializedObject so = new SerializedObject(targetDatabase);
        SerializedProperty recipesProp = so.FindProperty("recipes");
        
        // Check for duplicates (Simple O(N))
        // Since we import blindly, we likely rely on user clearing DB or simply appending.
        // Append logic:
        int index = recipesProp.arraySize;
        recipesProp.InsertArrayElementAtIndex(index);
        SerializedProperty newItem = recipesProp.GetArrayElementAtIndex(index);
        
        newItem.FindPropertyRelative("inputA").objectReferenceValue = wa;
        newItem.FindPropertyRelative("inputB").objectReferenceValue = wb;
        newItem.FindPropertyRelative("output").objectReferenceValue = wr;
        
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
