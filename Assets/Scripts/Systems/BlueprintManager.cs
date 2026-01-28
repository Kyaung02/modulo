using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class BlueprintManager : MonoBehaviour
{
    public static BlueprintManager Instance { get; private set; }

    [Header("Settings")]
    public int maxBlueprints = 10;
    public Camera captureCamera; // Assign a camera for clean captures (or use existing)
    
    [Header("Data")]
    public List<BlueprintData> blueprints = new List<BlueprintData>();
    public int selectedBlueprintIndex = -1;

    public event System.Action OnBlueprintListChanged;
    public event System.Action<int> OnBlueprintSelected; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    public BlueprintData GetSelectedBlueprint()
    {
        if (selectedBlueprintIndex >= 0 && selectedBlueprintIndex < blueprints.Count)
        {
            return blueprints[selectedBlueprintIndex];
        }
        return null;
    }

    public void SelectBlueprint(int index)
    {
        if (index >= 0 && index < blueprints.Count)
        {
            selectedBlueprintIndex = index;
            OnBlueprintSelected?.Invoke(index);
            // Notify BuildManager? Done via Polling or Event in BuildManager
            
            // Auto Select the component prefab in BuildManager
            if (BuildManager.Instance != null)
            {
                 // We need a public method in BuildManager to select by index WITHOUT resetting rotation etc?
                 // Or just use existing SelectComponent
                 BuildManager.Instance.ForceSelectComponent(blueprints[index].prefabIndex);
            }
        }
        else
        {
            selectedBlueprintIndex = -1;
            OnBlueprintSelected?.Invoke(-1);
        }
    }
    
    public void DeleteBlueprint(int index)
    {
        if (index >= 0 && index < blueprints.Count)
        {
            blueprints.RemoveAt(index);
            if (selectedBlueprintIndex == index) SelectBlueprint(-1);
            else if (selectedBlueprintIndex > index) selectedBlueprintIndex--; // Shift
            
            OnBlueprintListChanged?.Invoke();
        }
    }

    public void ClearBlueprints()
    {
        blueprints.Clear();
        selectedBlueprintIndex = -1;
        OnBlueprintListChanged?.Invoke();
    }

    public void AddBlueprint(string name, string json, int prefabIndex, Sprite sprite)
    {
        BlueprintData bp = new BlueprintData();
        bp.name = name;
        bp.snapshotJson = json;
        bp.prefabIndex = prefabIndex;
        bp.previewSprite = sprite;
        blueprints.Add(bp);
        OnBlueprintListChanged?.Invoke();
    }

    public void CaptureBlueprint(ComponentBase target, string jsonSnapshot, int prefabIndex)
    {
        if (target == null) return;
        
        // 1. Capture Image
        Sprite preview = CaptureStaticPreview(target);
        
        // 2. Create Data
        BlueprintData bp = new BlueprintData();
        bp.name = target.name.Replace("(Clone)", "").Trim() + " " + (blueprints.Count + 1);
        bp.snapshotJson = jsonSnapshot;
        bp.prefabIndex = prefabIndex;
        bp.previewSprite = preview;
        
        // 3. Add to List
        blueprints.Add(bp);
        OnBlueprintListChanged?.Invoke();
        
        // Auto select new
        SelectBlueprint(blueprints.Count - 1);
    }
    
    private Sprite CaptureStaticPreview(ComponentBase target)
    {
        // For RecursiveModule, we want to capture its Inner World view.
        // For Normal Component, we might just capture the icon? Or a close up?
        // Current requirement: "Image of the module (without worditems)"
        
        // Strategy: 
        // A. If RecursiveModule, use its internal CCTV camera logic but force a render to a temporary texture.
        // B. If Normal Component, maybe just use its Sprite? (Simple fallback)
        
        if (target is RecursiveModuleComponent rm)
        {
            return CaptureRecursiveModule(rm);
        }
        else
        {
            // Fallback: Just return generic sprite or capture scene?
            // For now return null (UI can show default icon)
            return null; 
        }
    }
    
    private Sprite CaptureRecursiveModule(RecursiveModuleComponent rm)
    {
        if (rm.innerGrid == null) return null;
        
        // 1. Setup Capture Camera
        // We can reuse the RecursiveModule's _previewCamera if it exists, or create a temp one.
        // Using a centralized 'captureCamera' is cleaner to avoid modifying Module state too much.
        
        if (captureCamera == null)
        {
            GameObject camObj = new GameObject("Blueprint_Capture_Cam");
            captureCamera = camObj.AddComponent<Camera>();
            captureCamera.enabled = false; // We manual render
            captureCamera.orthographic = true;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            if(Camera.main != null) captureCamera.backgroundColor = Camera.main.backgroundColor;
        }
        
        // 2. Position Camera
        // Center of 7x7
        Vector3 center = rm.innerGrid.originPosition + new Vector2(3.5f, 3.5f);
        // We need world position of inner grid.
        Vector3 worldCenter = rm.innerGrid.transform.position + center;
        
        captureCamera.transform.position = new Vector3(worldCenter.x, worldCenter.y, -20);
        captureCamera.orthographicSize = 3.6f; // Slightly larger than 3.5
        
        // 3. Hide Words
        // Find all WordVisualizers in innerGrid and hide them
        WordVisualizer[] words = rm.innerGrid.GetComponentsInChildren<WordVisualizer>();
        List<bool> loadedStates = new List<bool>();
        foreach(var w in words)
        {
            loadedStates.Add(w.gameObject.activeSelf);
            w.gameObject.SetActive(false); // Hide!
        }
        
        // 4. Render
        int res = 256;
        RenderTexture rt = RenderTexture.GetTemporary(res, res, 16);
        captureCamera.targetTexture = rt;
        captureCamera.Render();
        
        // 5. Read to Texture2D
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
        tex.Apply();
        
        // Cleanup
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        
        // Restore Words
        for(int i=0; i<words.Length; i++)
        {
            if(words[i] != null) words[i].gameObject.SetActive(loadedStates[i]);
        }
        
        // 6. Create Sprite
        return Sprite.Create(tex, new Rect(0,0,res,res), new Vector2(0.5f, 0.5f));
    }
}
