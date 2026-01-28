using UnityEngine;
using UnityEngine.UI;

public class BlueprintUISetup : MonoBehaviour
{
    // Call this from a button or run periodically if missing
    private void Start()
    {
        SetupBlueprintSystem();
    }

    public void SetupBlueprintSystem()
    {
        // 1. Ensure BlueprintManager exists
        if (FindFirstObjectByType<BlueprintManager>() == null)
        {
            GameObject managerObj = new GameObject("BlueprintManager");
            managerObj.AddComponent<BlueprintManager>();
            Debug.Log("Created BlueprintManager");
        }

        // 2. Find Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas found! Please create a UI Canvas first.");
            return;
        }

        // 3. Create Blueprint UI Panel if missing
        if (FindFirstObjectByType<BlueprintUI>() == null)
        {
            GameObject panelObj = new GameObject("BlueprintPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            
            // Positioning (Right side)
            RectTransform rect = panelObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(200, 0); // Width 200
            
            // Background
            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.5f);

            // Scroll View Structure
            GameObject scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(panelObj.transform, false);
            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(10, 10);
            scrollRect.offsetMax = new Vector2(-10, -10);
            
            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            
            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewRect = viewport.AddComponent<RectTransform>();
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            Image viewImg = viewport.AddComponent<Image>(); // Mask needs image
            viewImg.color = new Color(1,1,1,0.01f); // Transparent but raycast target? No needs Mask.
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            
            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 300); // Dynamic height
            
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160, 160);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            sr.content = contentRect;
            sr.viewport = viewRect;

            // Add BlueprintUI Component
            BlueprintUI ui = panelObj.AddComponent<BlueprintUI>();
            ui.contentRoot = contentRect;
            
            // Generate Prefab (In memory)
            ui.blueprintItemPrefab = CreateItemPrefab();
            
            Debug.Log("Created Blueprint UI");
        }
    }
    
    // Create a temporary prefab-like object (or real prefab if in editor, but runtime is safer here)
    // We will attach this to the script at runtime
    private GameObject CreateItemPrefab()
    {
        // Actually we can't save assets easily. 
        // We will create a disabled Game Object in scene to act as prefab.
        GameObject prefabObj = new GameObject("BlueprintItem_Prefab");
        prefabObj.transform.SetParent(this.transform); // Hide under setup script
        prefabObj.SetActive(false);
        
        RectTransform rect = prefabObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160, 160);
        
        Image bg = prefabObj.AddComponent<Image>();
        bg.color = Color.white;
        
        Button btn = prefabObj.AddComponent<Button>();
        btn.targetGraphic = bg;
        
        // Icon Image
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(prefabObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(10, 10);
        iconRect.offsetMax = new Vector2(-10, -10);
        
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        
        // Raycast padding removal?
        iconImg.raycastTarget = false;
        
        return prefabObj;
    }
}
