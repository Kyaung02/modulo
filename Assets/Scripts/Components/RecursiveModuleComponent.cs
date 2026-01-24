using UnityEngine;
using System.Collections.Generic;

public class RecursiveModuleComponent : ComponentBase
{
    // 1x1 External Size
    public override int GetWidth() => 1;
    public override int GetHeight() => 1;

    /// <summary> Module cannot be rotated; no-op. </summary>
    public override void Rotate() { }

    [Header("Inner World")]
    [HideInInspector] public ModuleManager innerGrid; // Auto-generated
    private Transform innerWorldRoot; 

    protected override void Start()
    {
        base.Start(); // Important for registration
        
        // Ensure we have a collider for clikcing
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        
        // Size it to the cell (1.0f default)
        col.size = new Vector2(GetWidth() * ModuleManager.Instance.cellSize, GetHeight() * ModuleManager.Instance.cellSize);
        // Pivot is bottom-left (0,0) usually? Or center?
        // Sprite is usually centered. ComponentBase pivot is Grid Position (Bottom Left of logical cell).
        // If Visual is centered at (0.5, 0.5), Collider should be there too.
        // Assuming Visual is Child(0).
        col.offset = new Vector2(0.5f, 0.5f); 
        
        // Prevent creating inner worlds for prefabs or preview objects
        // Simple check: if we are not in a valid scene or don't have a manager yet (though base.Start tries to find one)
        if (gameObject.scene.rootCount != 0) 
        {
            InitializeInnerWorld();
        }
    }

    private void InitializeInnerWorld()
    {
        // ... (Existing code) ...
        // Create a completely NEW Object. Do NOT use Instantiate logic if it carries children.
        // Wait, 'new GameObject' creates an empty one.
        // But if the user put children under 'innerWorldRoot' in the PREFAB inspector, they might be copied?
        // Ah, innerWorldRoot is a private field, initialized here.
        // But what if 'this.transform' has children? 
        // The issue: Maybe the user is seeing the *outer* module's children (like Visuals based on prefab) inside the inner world?
        // No, innerWorldRoot is moved 1000 units away.
        
        // Let's make sure we are destroying children of the NEW innerWorldRoot (which should be empty usually).
        // But if the issue is that "InnerWorld" logic is somehow referencing existing objects?
        
        innerWorldRoot = new GameObject($"InnerWorld_{System.Guid.NewGuid()}").transform;
        
        // Move VERY far away to avoid collisions or visual overlap with other modules
        // Use a random or hashed offset
        innerWorldRoot.position = new Vector3(Random.Range(10000, 90000), Random.Range(10000, 90000), 0);
        
        innerGrid = innerWorldRoot.gameObject.AddComponent<ModuleManager>();
        innerGrid.width = 7;
        innerGrid.height = 7;
        innerGrid.cellSize = 1.0f;
        innerGrid.originPosition = new Vector2(-3.5f, -3.5f);
        
        // Ensure no children exist (e.g. if we instantiated a prefab that had stuff)
        // Ensure no children exist (e.g. if we instantiated a prefab that had stuff)
        // Note: new GameObject shouldn't have children.
        // But IF innerGrid logic somehow attached things?
        // Let's clear aggressively just in case any visualizers auto-attached.
        
        int childCount = innerGrid.transform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(innerGrid.transform.GetChild(i).gameObject);
        }
        
        // Link parent
        innerGrid.parentManager = _assignedManager;
        innerGrid.ownerComponent = this; // Back-link for navigation
        
        // Auto-add Visualizer so we can see the grid
        var viz = innerWorldRoot.gameObject.AddComponent<GridVisualizer>();
        
        // Inherit visual settings from parent or global visualizer to maintain style
        GridVisualizer parentViz = null;
        if (_assignedManager != null) parentViz = _assignedManager.GetComponentInChildren<GridVisualizer>();
        if (parentViz == null) parentViz = FindFirstObjectByType<GridVisualizer>();
        
        if (parentViz != null)
        {
            viz.lineMaterial = parentViz.lineMaterial;
            // viz.orderInLayer = parentViz.orderInLayer; // Keep default or copy if needed
        }
        
        CreateInternalPorts();
        
        // Depth Check: Only create preview if depth < 2 (Optimization)
        // We need to know current depth. We can crawl up parents.
        int depth = 0;
        ModuleManager current = _assignedManager;
        while (current != null && current.parentManager != null)
        {
            depth++;
            current = current.parentManager;
        }
        
        if (depth < 2)
        {
            CreatePreview();
        }
    }
    
    [Header("Preview Config")]
    private Camera _previewCamera;
    private RenderTexture _previewTexture;
    private GameObject _previewDisplay;
    
    private void CreatePreview()
    {
        // 1. Create Render Texture
        _previewTexture = new RenderTexture(512, 512, 16); // 512x512 resolution
        _previewTexture.Create();
        
        // 2. Create Inner Camera
        GameObject camObj = new GameObject("CCTV_Camera");
        camObj.transform.SetParent(innerWorldRoot);
        _previewCamera = camObj.AddComponent<Camera>();
        
        // Setup Camera Position/View
        // Center of 7x7 grid is (3.5, 3.5) relative to origin.
        Vector3 center = innerGrid.originPosition + new Vector2(3.5f, 3.5f);
        _previewCamera.transform.localPosition = new Vector3(center.x, center.y, -10f);
        _previewCamera.orthographic = true;
        _previewCamera.orthographicSize = 3.5f; // Exactly cover 7x7 (Height 7 / 2 = 3.5)
        _previewCamera.targetTexture = _previewTexture;
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        
        // Match Main Camera background for seamless look
        if (Camera.main != null)
        {
            _previewCamera.backgroundColor = Camera.main.backgroundColor;
        }
        else
        {
             _previewCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f); // Fallback
        }
        
        // Exclude UI layer if needed? For now default mask.
        
        CreateInnerBackground();
        
        // 3. Create Display Quad on the Module (Outside)
        _previewDisplay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _previewDisplay.name = "Preview_Display";
        _previewDisplay.transform.SetParent(transform);
        _previewDisplay.transform.localPosition = Vector3.zero; // Center top
        _previewDisplay.transform.localRotation = Quaternion.identity;
        _previewDisplay.transform.localScale = new Vector3(0.95f, 0.95f, 1f); // 0.95x0.95 to match requested scale
        
        // Scale to match aspect ratio if needed, but 1x1 is square.
        
        // Remove Collider (Module has its own)
        Destroy(_previewDisplay.GetComponent<Collider>());
        
        // Assign Texture
        Renderer r = _previewDisplay.GetComponent<Renderer>();
        r.material = new Material(Shader.Find("Unlit/Texture")); // Simple shader
        r.material.mainTexture = _previewTexture;
        
        // Layering
        // Ensure the display is slightly above key visual so it doesn't z-fight
        _previewDisplay.transform.localPosition = new Vector3(0, 0, -0.1f);
    }

    private void CreateInternalPorts()
    {
        // Create 4 ports ON THE WALLS (Outside 0-6 range)
        // Up Wall: (3, 7)
        SpawnPort(new Vector2Int(3, 7), Direction.Up, Direction.Down);
        // Right Wall: (7, 3)
        SpawnPort(new Vector2Int(7, 3), Direction.Right, Direction.Left);
        // Down Wall: (3, -1)
        SpawnPort(new Vector2Int(3, -1), Direction.Down, Direction.Up);
        // Left Wall: (-1, 3)
        SpawnPort(new Vector2Int(-1, 3), Direction.Left, Direction.Right);
    }

    // --- Export Logic (Inner -> Port) ---
    // Since Components push to neighbors, we need to ensure the components at the edge 
    // can "see" the wall ports as neighbors even if they are out of bounds.
    // ComponentBase.OnTick uses assignedManager.GetComponentAt(targetPos).
    // The ModuleManager usually returns null for out of bounds.
    // We need to bridge this.
    
    // Solution: We don't need to change RecursiveModuleComponent logic much if ModuleManager can return Wall Ports.
    // BUT modifying ModuleManager to support out-of-bounds is risky.
    
    // Alternative: The internal components (Movers) pushing to the wall will fail to find a target.
    // So the item stays on the Mover.
    // RecursiveModuleComponent needs to WATCH the edges.
    
    protected override void OnTick(long tickCount)
    {
        base.OnTick(tickCount);
        
        // Scan edges for items trying to leave?
        // Actually, better approach:
        // Let the PortComponent be "Registered" in a special dictionary in ModuleManager or RecursiveModule?
        // Or simply: Iterate ports and check if neighbor has item facing them?
        // No, ComponentBase PUSHES.
        
        // Hack: Make ModuleManager return the Port for specific out-of-bound coordinates.
        // But ModuleManager logic is generic.
        
        // Let's use the RecursiveModule OnTick to manually Pull from edges?
        // If a Mover at (3,6) is facing Up, and has an item.
        // It tries to push to (3,7). Manager returns null. Push fails.
        // So we need to check (3,6).
        
        CheckEdgeExports();
    }
    
    private void CheckEdgeExports()
    {
        // Check 4 edges
        CheckExport(new Vector2Int(3, 6), Direction.Up);
        CheckExport(new Vector2Int(6, 3), Direction.Right);
        CheckExport(new Vector2Int(3, 0), Direction.Down);
        CheckExport(new Vector2Int(0, 3), Direction.Left);
    }
    
    private void CheckExport(Vector2Int gridPos, Direction dir)
    {
        ComponentBase comp = innerGrid.GetComponentAt(gridPos);
        if (comp != null && comp.HeldWord != null)
        {
            // check if component is facing the wall
            // Actually, we should check if component is TRYING to output this way.
            // GetOutputDirection() depends on rotation.
            
            // Allow Mover to push into wall port.
            if (comp.GetOutputDirection() == GetVector(dir))
            {
                // Move item to Port immediately (since Mover failed to push)
                WordData word = comp.HeldWord;
                
                // Which port?
                PortComponent port = FindPort(dir);
                if (port != null)
                {
                    // Manually transfer
                    // Port handles ExportItem logic itself essentially
                    if (ExportItem(word, dir))
                    {
                        comp.ClearHeldWord();
                        // Trigger visual update for comp?
                        // comp.UpdateVisuals() is protected.
                        // We rely on next tick or force update if we could.
                        // But we can't call Protected methods.
                        // Wait, ComponentBase.HeldWord is public? No, we need to check accessor.
                        // HeldWord is public property. UpdateVisuals is protected.
                        // Setting HeldWord usually doesn't trigger visual update in base.
                        // We need a way to notify component it lost item.
                        // ComponentBase needs public ClearItem()? 
                        // Or just set to null, and component visual refheshes next tick? 
                        // Usually Visuals are event based or Update based?
                        // Based on code, UpdateVisuals is called manually.
                        // We might leave a 'ghost' item visual until next action.
                        // Let's ignore visual glitch for a moment or fix ComponentBase.
                    }
                }
            }
        }
    }
    
    private Vector2Int GetVector(Direction dir)
    {
         switch(dir) {
             case Direction.Up: return Vector2Int.up;
             case Direction.Right: return Vector2Int.right;
             case Direction.Down: return Vector2Int.down;
             case Direction.Left: return Vector2Int.left;
         }
         return Vector2Int.zero;
    }
    private Dictionary<Direction, PortComponent> _ports = new Dictionary<Direction, PortComponent>();

    private void SpawnPort(Vector2Int gridPos, Direction wallDir, Direction facingDir)
    {
        GameObject portObj = new GameObject($"Port_{wallDir}");
        portObj.transform.SetParent(innerGrid.transform);
        PortComponent port = portObj.AddComponent<PortComponent>();
        
        // Cache it for lookup
        if (!_ports.ContainsKey(wallDir)) _ports.Add(wallDir, port);
        
        // Visuals - Thin Black Line on Wall
        GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Quad);
        vis.transform.SetParent(portObj.transform);
        
        // Scale: Wide but thinner (Strip)
        vis.transform.localScale = new Vector3(1.0f, 0.15f, 1.0f);
        // Position: Offset to the "Front" edge (Up, before rotation)
        // Since Port rotates to face inward, this Up edge will rotate to touch the grid boundary.
        vis.transform.localPosition = new Vector3(0, 0.425f, -0.01f);
        
        Destroy(vis.GetComponent<Collider>()); // Visual only
        
        var ren = vis.GetComponent<Renderer>();
        if (ren) 
        {
            ren.material = new Material(Shader.Find("Sprites/Default")); // Use Sprite/Unlit shader for flat black
            ren.material.color = Color.black; 
            ren.sortingOrder = 5; // On top of grid
        }

        // Position: Use valid GridToWorld even if out of bounds (Grid logic supports math)
        portObj.transform.position = innerGrid.GridToWorldPosition(gridPos.x, gridPos.y);

        // Configure Logic
        port.Configure(this, wallDir);
        
        // IMPORTANT: We do NOT register this to the Grid System because it's out of bounds.
        
        int targetRot = (int)facingDir;
        while ((int)port.RotationIndex != targetRot)
        {
            port.Rotate();
        }
    }
    
    private PortComponent FindPort(Direction dir)
    {
        if (_ports.ContainsKey(dir)) return _ports[dir];
        return null;
    }

    // --- IO Logic ---

    // External -> Internal
    // Item coming from Outside into this Module
    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        // Identify entry direction relative to module
        Vector2Int localDir = WorldToLocalDirection(direction);
        
        // Map Local Entry Direction to Wall
        // Entering from Bottom (Up) -> Bottom Wall (Down Port)
        // Entering from Left (Right) -> Left Wall (Left Port)
        
        Direction targetWall = Direction.Down; // Default
        
        if (localDir == Vector2Int.up) targetWall = Direction.Down;
        else if (localDir == Vector2Int.down) targetWall = Direction.Up;
        else if (localDir == Vector2Int.right) targetWall = Direction.Left;
        else if (localDir == Vector2Int.left) targetWall = Direction.Right;
            
        // Find the port in Inner Grid
        PortComponent port = FindPort(targetWall);
        if (port != null)
        {
            return port.ImportItem(word);
        }
        
        return false;
    }

    // Internal -> External
    // Called by Port when item tries to leave inner world
    public bool ExportItem(WordData word, Direction fromWall)
    {
        // Map Inner Wall to Outer Output Direction
        // Top Wall (Up) -> Output Up (Local) -> World Up
        
        Vector2Int localOutDir = Vector2Int.zero;
        switch (fromWall)
        {
            case Direction.Up: localOutDir = Vector2Int.up; break;
            case Direction.Right: localOutDir = Vector2Int.right; break;
            case Direction.Down: localOutDir = Vector2Int.down; break;
            case Direction.Left: localOutDir = Vector2Int.left; break;
        }
        
        // Output to World
        Vector2Int worldOutDir = LocalToWorldDirection(localOutDir);
        Vector2Int targetPos = GridPosition + worldOutDir;
        
        // Check neighbor
        if (_assignedManager != null)
        {
            ComponentBase neighbor = _assignedManager.GetComponentAt(targetPos);
            if (neighbor != null)
            {
                return neighbor.AcceptWord(word, worldOutDir, targetPos);
            }
        }
        
        return false;
    }


    
    // --- Outer World Preview (Glass Floor) ---
    private Camera _outerCamera;
    private RenderTexture _outerTexture;
    private GameObject _innerBackgroundQuad;
    
    // Called inside InitializeInnerWorld
    private void CreateInnerBackground()
    {
        // Create a large quad behind the inner world to display the outer world
        _innerBackgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _innerBackgroundQuad.name = "Background_OuterWorld";
        _innerBackgroundQuad.transform.SetParent(innerWorldRoot);
        
        // Center of 7x7 grid is (3.5, 3.5). 
        // We want it slightly behind everything (z = 10)
        Vector3 center = innerGrid.originPosition + new Vector2(3.5f, 3.5f);
        _innerBackgroundQuad.transform.localPosition = new Vector3(center.x, center.y, 10f);
        
        // Size: 21x21 (3x the grid size to match the 3x Outer Camera view)
        _innerBackgroundQuad.transform.localScale = new Vector3(21f, 21f, 1f);
        _innerBackgroundQuad.transform.localRotation = Quaternion.identity; 
        
        Destroy(_innerBackgroundQuad.GetComponent<Collider>());
        
        // Ensure it renders behind everything
        var ren = _innerBackgroundQuad.GetComponent<Renderer>();
        ren.sortingOrder = -100;
        
        // Material setup delayed until texture is created
        _innerBackgroundQuad.SetActive(false); // Hidden by default
    }

    private void ToggleOuterPreview(bool active)
    {
        if (active)
        {
            if (_outerTexture == null)
            {
                _outerTexture = new RenderTexture(512, 512, 16);
                _outerTexture.Create();
            }
            
            if (_outerCamera == null)
            {
                GameObject camObj = new GameObject("Outer_Watcher_Cam");
                camObj.transform.SetParent(transform); 
                camObj.transform.localPosition = new Vector3(0, 0, -10f); 
                
                _outerCamera = camObj.AddComponent<Camera>();
                _outerCamera.orthographic = true;
                _outerCamera.targetTexture = _outerTexture;
                _outerCamera.clearFlags = CameraClearFlags.SolidColor;
                
                // Match Main Camera styling
                if (Camera.main != null)
                    _outerCamera.backgroundColor = Camera.main.backgroundColor;
                else
                    _outerCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f); 
                    
                _outerCamera.orthographicSize = 1.5f; // 3x3 area (Radius 1.5)
            }
            _outerCamera.gameObject.SetActive(true);
            
            if (_innerBackgroundQuad != null)
            {
                _innerBackgroundQuad.SetActive(true);
                var ren = _innerBackgroundQuad.GetComponent<Renderer>();
                if (ren.material.name != "OuterPreviewMat") 
                {
                    Material mat = new Material(Shader.Find("Unlit/Texture"));
                    mat.name = "OuterPreviewMat";
                    mat.mainTexture = _outerTexture;
                    ren.material = mat;
                }
            }
            
            // PreviewDisplay logic moved to EnterModule callback to prevend black-screen during zoom
        }
        else
        {
            if (_outerCamera != null) _outerCamera.gameObject.SetActive(false);
            if (_innerBackgroundQuad != null) _innerBackgroundQuad.SetActive(false);
            // PreviewDisplay logic moved to ExitModule
        }
    }

    // Allow entering the module
    // Public method called by BuildManager
    public void EnterModule()
    {
        // Check if innerGrid is initialized
        if (innerGrid == null)
        {
            Debug.LogError($"[RecursiveModule] Cannot enter module {name}: innerGrid is null!");
            return;
        }
        
        BuildManager bm = FindFirstObjectByType<BuildManager>();
        if (bm == null)
        {
            Debug.LogError("[RecursiveModule] Cannot find BuildManager!");
            return;
        }

        // ENABLE OUTER PREVIEW
        ToggleOuterPreview(true);
        
        // Freeze local CCTV immediately to prevent visual feedback loop / weirdness during zoom
        if (_previewCamera != null) _previewCamera.enabled = false;
        
        Debug.Log($"[RecursiveModule] Entering Module {name}...");
        
        // Target Inner Position - use the actual world position of innerGrid center
        Vector3 innerCenter = new Vector3(
            innerGrid.transform.position.x + innerGrid.originPosition.x + (innerGrid.width * innerGrid.cellSize * 0.5f),
            innerGrid.transform.position.y + innerGrid.originPosition.y + (innerGrid.height * innerGrid.cellSize * 0.5f),
            Camera.main.transform.position.z
        );
        
        if (CameraController.Instance != null)
        {
            StartCoroutine(CameraController.Instance.TransitionEnterModule(transform.position, innerCenter, () => {
                bm.SetActiveManager(innerGrid);
                if (_previewDisplay != null) _previewDisplay.SetActive(false); // Hide only after transition
            }));
        }
        else
        {
            // Fallback
            Camera.main.transform.position = innerCenter;
            Camera.main.orthographicSize = 5; 
            bm.SetActiveManager(innerGrid);
            if (_previewDisplay != null) _previewDisplay.SetActive(false);
        }
    }

    public void ExitModule()
    {
        // DISABLE OUTER PREVIEW
        ToggleOuterPreview(false);
        
        if (_previewDisplay != null) _previewDisplay.SetActive(true); // Show immediately on exit start
        // Unfreeze local CCTV
        if (_previewCamera != null) _previewCamera.enabled = true;
    }

    // Deprecated: Interaction is now handled by BuildManager
    // void OnMouseDown() { ... }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        if (_previewTexture != null)
        {
            _previewTexture.Release();
        }
        
        if (innerWorldRoot != null)
        {
            Destroy(innerWorldRoot.gameObject);
        }
    }

    // Helper: World <-> Local conversion copy from others
    // Need to centralize this in ComponentBase really... 
    // But overriding for now.
    private Vector2Int WorldToLocalDirection(Vector2Int worldDir)
    {
        int r = (int)RotationIndex;
        int x = worldDir.x;
        int y = worldDir.y;
        for (int i=0; i<r; i++) { int temp = x; x = -y; y = temp; }
        return new Vector2Int(x, y);
    }
    
    private Vector2Int LocalToWorldDirection(Vector2Int localDir)
    {
         int x = localDir.x;
         int y = localDir.y;
         for (int i=0; i<(int)RotationIndex; i++) { int temp = x; x = y; y = -temp; }
         return new Vector2Int(x, y);
    }
}
