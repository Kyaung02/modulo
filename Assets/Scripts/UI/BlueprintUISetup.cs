using UnityEngine;
using UnityEngine.UI;

public class BlueprintUISetup : MonoBehaviour
{
    // Call this from a button or run periodically if missing
    private void Start()
    {
        SetupBlueprintSystem();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        if (FindFirstObjectByType<BlueprintUISetup>() == null)
        {
            GameObject obj = new GameObject("CheckBlueprintSystem");
            obj.AddComponent<BlueprintUISetup>();
            DontDestroyOnLoad(obj); // Keep it around if needed, or let it just run Start
        }
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

        // 2. Setup Dedicated Canvas
        Canvas canvas = GameObject.Find("BlueprintCanvas")?.GetComponent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("BlueprintCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // On Top
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("Created BlueprintCanvas");
        }

        // 3. Find or Create Blueprint UI Panel
        GameObject panelObj = GameObject.Find("BPPanel");
        bool createdNew = false;
        
        if (panelObj == null)
        {
            // Auto creation removed as per user request
            Debug.Log("[BlueprintUISetup] BPPanel not found. Auto-creation disabled.");
            return;
        }

        if (panelObj.GetComponent<BlueprintUI>() == null)
        {
            // Setup Scroll View Structure inside panelObj if created new OR if existing panel is empty?
            // User likely prepared an empty panel. Let's add ScrollView structure blindly if it's missing?
            // Cleaner: Just create ScrollView as child.
            
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
            grid.cellSize = new Vector2(280, 120); // Wide cells
            grid.spacing = new Vector2(10, 10);
            grid.childAlignment = TextAnchor.UpperLeft; // Revert to Left
            grid.padding = new RectOffset(50, 0, 45, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            sr.content = contentRect;
            sr.viewport = viewRect;

            // Add BlueprintUI Component
            BlueprintUI ui = panelObj.AddComponent<BlueprintUI>();
            ui.contentRoot = contentRect;
            ui.blueprintItemPrefab = CreateItemPrefab(); // Create prefab anyway
            
             Debug.Log("Setup Blueprint UI on BPPanel");
        }
    }
    
    // Create a temporary prefab-like object (or real prefab if in editor, but runtime is safer here)
    // We will attach this to the script at runtime
    public GameObject CreateItemPrefab()
    {
        // Root Container (Invisible)
        GameObject prefabObj = new GameObject("BlueprintItem_Prefab");
        prefabObj.transform.SetParent(this.transform); 
        prefabObj.SetActive(false);
        
        RectTransform rect = prefabObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280, 120); // Total Size
        
        // 1. Select Button (Left Side)
        GameObject btnObj = new GameObject("SelectButton");
        btnObj.transform.SetParent(prefabObj.transform, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 0.5f);
        btnRect.anchorMax = new Vector2(0, 0.5f);
        btnRect.pivot = new Vector2(0, 0.5f);
        btnRect.sizeDelta = new Vector2(100, 100); // 100x100 Button
        btnRect.anchoredPosition = new Vector2(10, 0); // 10px Margin
        
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = Color.white;
        
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        
        // Icon (Inside Button)
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(5, 5); // Padding inside button
        iconRect.offsetMax = new Vector2(-5, -5);
        
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        
        // 2. Name Input (Right Side, Outside Button)
        GameObject inputObj = new GameObject("NameInput");
        inputObj.transform.SetParent(prefabObj.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 0.5f); 
        inputRect.anchorMax = new Vector2(0, 0.5f);
        inputRect.pivot = new Vector2(0, 0.5f);
        inputRect.sizeDelta = new Vector2(150, 40);
        inputRect.anchoredPosition = new Vector2(120, 0); // 10 margin + 100 icon + 10 gap
        
        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        InputField input = inputObj.AddComponent<InputField>();
        input.targetGraphic = inputBg;
        
        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5, 5);
        textRect.offsetMax = new Vector2(-5, -5);
        
        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = 14;
        text.supportRichText = false;
        
        input.textComponent = text;
        
        // Placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputObj.transform, false);
        RectTransform placeRect = placeholderObj.AddComponent<RectTransform>();
        placeRect.anchorMin = Vector2.zero;
        placeRect.anchorMax = Vector2.one;
        placeRect.offsetMin = new Vector2(5, 5);
        placeRect.offsetMax = new Vector2(-5, -5);
        
        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.color = new Color(1,1,1,0.5f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.fontSize = 14;
        placeholder.text = "Module Name...";
        input.placeholder = placeholder;
        
        return prefabObj;
    }
}
