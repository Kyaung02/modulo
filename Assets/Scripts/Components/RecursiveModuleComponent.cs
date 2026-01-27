using UnityEngine;
using System.Collections.Generic;

using Unity.Netcode;

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

    // Networked Inner World Position
    private NetworkVariable<Vector3> _netInnerWorldPos = new NetworkVariable<Vector3>(Vector3.zero);
    
    // Depth tracking for dynamic visibility
    private int _depth = 0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); // Important for registration
        
        // Listen for Inner World Position
        _netInnerWorldPos.OnValueChanged += OnInnerWorldPosChanged;

        // Ensure we have a collider for clikcing
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        
        //we must assume here that all cellsizes are equal, otherwise change to assignedmanager
        if (ModuleManager.Instance != null)
        {
             // Size it to the cell (1.0f default)
             col.size = new Vector2(GetWidth() * ModuleManager.Instance.cellSize, GetHeight() * ModuleManager.Instance.cellSize);
             col.offset = new Vector2(0.5f, 0.5f); 
        }

        // Cleanup Legacy/Ghost Ports that might exist on the Prefab to prevent duplicates
        var existingPorts = GetComponentsInChildren<PortComponent>();
        foreach(var p in existingPorts)
        {
            if (p.transform.parent == transform) // Only direct children
            {
                Destroy(p.gameObject);
            }
        }
        
        if (IsServer) 
        {
            // Server initializes inner world immediately
            Vector3 randomPos = new Vector3(Random.Range(10000, 90000), Random.Range(10000, 90000), 0);
            _netInnerWorldPos.Value = randomPos; // Triggers Client callback
            InitializeInnerWorld(randomPos);
        }
        else
        {
            // Client checks if already set
            if (_netInnerWorldPos.Value != Vector3.zero)
            {
                InitializeInnerWorld(_netInnerWorldPos.Value);
            }
        }
    }
    
    public override void OnNetworkDespawn()
    {
        _netInnerWorldPos.OnValueChanged -= OnInnerWorldPosChanged;
        
        // Cleanup inner world
        if (innerWorldRoot != null)
        {
             Destroy(innerWorldRoot.gameObject);
        }
        
        // Cleanup Ports (Server Only)
        if (IsServer)
        {
            foreach(var p in _spawnedPorts)
            {
                if (p != null && p.IsSpawned) p.Despawn();
            }
            _spawnedPorts.Clear();
        }

        base.OnNetworkDespawn();
    }
    
    private void OnInnerWorldPosChanged(Vector3 oldPos, Vector3 newPos)
    {
        if (newPos != Vector3.zero && innerWorldRoot == null)
        {
            InitializeInnerWorld(newPos);
        }
    }

    private void InitializeInnerWorld(Vector3 position)
    {
        if (innerWorldRoot != null) return; // Already init

        innerWorldRoot = new GameObject($"InnerWorld_{name}_{NetworkObjectId}").transform;
        innerWorldRoot.position = position;
        
        innerGrid = innerWorldRoot.gameObject.AddComponent<ModuleManager>();
        innerGrid.width = 7;
        innerGrid.height = 7;
        innerGrid.cellSize = 1.0f;
        innerGrid.originPosition = new Vector2(-3.5f, -3.5f);
        
        // Clean children
        int childCount = innerGrid.transform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(innerGrid.transform.GetChild(i).gameObject);
        }
        
        // Link parent
        // Note: _assignedManager might be null on Client if parent module isn't found yet, 
        // but finding by position in ComponentBase fixes that. 
        // RecursiveModule's _assignedManager logic assumes it's registered.
        // We set innerGrid's parentManager to our _assignedManager later or now?
        // Since we are creating innerGrid, we know it's "us".
        innerGrid.parentManager = _assignedManager; 
        innerGrid.ownerComponent = this; 
        
        // Auto-add Visualizer
        var viz = innerWorldRoot.gameObject.AddComponent<GridVisualizer>();
        
        // Inherit style
        GridVisualizer parentViz = null;
        if (_assignedManager != null) parentViz = _assignedManager.GetComponentInChildren<GridVisualizer>();
        if (parentViz == null) parentViz = FindFirstObjectByType<GridVisualizer>();
        
        if (parentViz != null)
        {
            viz.lineMaterial = parentViz.lineMaterial;
        }
        
        // Ports Logic: Spawn on Server, Logic on Both
        if (IsServer)
        {
            CreateInternalPorts();
        }
        
        // Depth Check
        // We calculate depth but NO LONGER limit creation. 
        // Visibility is handled dynamically in Update to support "1 level out, 2 levels in" view.
        _depth = GetManagerDepth(_assignedManager);
        
        CreatePreview();
    }
    // ... (CreatePreview same)
    
    // ...

    private List<NetworkObject> _spawnedPorts = new List<NetworkObject>();

    private void SpawnPort(Vector2Int gridPos, Direction wallDir, Direction facingDir)
    {
        // Networked Spawn version
        // We need a Prefab for Port? Or Instantiate ScriptableObject?
        // Or create empty and AddComponent(NetworkObject)?
        // Netcode requires NetworkObject to be prefab for dynamic spawn usually? No.
        // But dynamic NO must be registered in prefab list? 
        // Actually, best way is to have a "PortPrefab" registered.
        // If user hasn't created one, we can fail.
        // Or: Construct it dynamically and simply rely on Dynamic spawning (might fail if not registered prefab).
        // Let's assume user has a "Port" prefab or we use a fallback?
        
        // Load Prefab (Requires NetworkAutoSetup to have run)
        GameObject prefab = Resources.Load<GameObject>("NetworkPrefabs/PortComponent");
        if (prefab == null)
        {
            Debug.LogError("Missing PortComponent prefab! Please run Modulo > Setup Network Prefabs in menu.");
            return;
        }

        GameObject portObj = Instantiate(prefab);
        portObj.name = $"Port_{wallDir}";
        
        // Do NOT parent NetworkObject to non-NetworkObject. Leave at root.
        // We track it manually for cleanup.

        PortComponent port = portObj.GetComponent<PortComponent>();
        
        // Cache it for lookup
        if (!_ports.ContainsKey(wallDir)) _ports.Add(wallDir, port);
        
        // Position: Use valid GridToWorld even if out of bounds.
        // Now using Port prefab local offset (1.0f) combined with rotation to align with wall.
        portObj.transform.position = innerGrid.GridToWorldPosition(gridPos.x, gridPos.y);

        // Configure Logic
        port.Configure(this, wallDir);
        
        // Prepare for Network Spawn (Crucial to avoid (0,0) registration)
        port.PrepareForSpawn(gridPos, facingDir);
        
        var no = portObj.GetComponent<NetworkObject>();
        no.Spawn();
        _spawnedPorts.Add(no);
        
        // Manual Link (Since we are at root)
        port.SetManager(innerGrid); // Set on Server immediately
        port.SetParentModule(this); // Share via NetworkVariable for Client
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
            // Debug.Log($"[RecursiveModule] Importing item to Port {targetWall}");
            bool result = port.ImportItem(word);
            if (!result) Debug.LogWarning($"[RecursiveModule] Port {targetWall} rejected item.");
            return result;
        }
        else
        {
            Debug.LogError($"[RecursiveModule] Could not find Port for Wall {targetWall}! (Dir: {direction}, Local: {localDir})");
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
                return neighbor.AcceptWord(word, worldOutDir, GridPosition);
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
    // Public method called by BuildManager
    public void EnterModule()
    {
        // Debug Init Status
        if (innerGrid == null)
        {
            Debug.LogError($"[RecursiveModule] Cannot enter module {name}: innerGrid is null! IsServer={IsServer}, NetPos={_netInnerWorldPos.Value}");
            // Attempt Late Init if possible
            if (_netInnerWorldPos.Value != Vector3.zero)
            {
                 Debug.LogWarning("[RecursiveModule] Attempting late lazy-init...");
                 InitializeInnerWorld(_netInnerWorldPos.Value);
            }
            if (innerGrid == null) return;
        }
        
        BuildManager bm = FindFirstObjectByType<BuildManager>();
        if (bm == null)
        {
            Debug.LogError("[RecursiveModule] Cannot find BuildManager!");
            return;
        }

        // Do NOT enable outer preview yet (avoid recursion during zoom)
        // Do NOT freeze local CCTV yet (keep updating texture during zoom)
        
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
                
                // Transition Complete Actions:
                // 1. Enable Glass Floor (Outer Preview)
                ToggleOuterPreview(true);
                
                // 2. Hide Module Exterior Display (we are inside now)
                if (_previewDisplay != null) _previewDisplay.SetActive(false); 
                
                // 3. Disable CCTV Camera (save perf, we see real thing now)
                if (_previewCamera != null) _previewCamera.enabled = false;
            }));
        }
        else
        {
            // Fallback
            Camera.main.transform.position = innerCenter;
            Camera.main.orthographicSize = 5; 
            bm.SetActiveManager(innerGrid);
            
            ToggleOuterPreview(true);
            if (_previewDisplay != null) _previewDisplay.SetActive(false);
            if (_previewCamera != null) _previewCamera.enabled = false;
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
    
    public override void OnDestroy()
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

    // WorldToLocalDirection removed (Inherited)
    
    private Vector2Int LocalToWorldDirection(Vector2Int localDir)
    {
         int x = localDir.x;
         int y = localDir.y;
         for (int i=0; i<(int)RotationIndex; i++) { int temp = x; x = y; y = -temp; }
         return new Vector2Int(x, y);
    }

    private int GetManagerDepth(ModuleManager manager)
    {
        int d = 0;
        ModuleManager current = manager;
        while (current != null && current.parentManager != null)
        {
            d++;
            current = current.parentManager;
        }
        return d;
    }

    private void Update()
    {
        // Dynamic Camera Culling to optimize performance
        // Rule: Visible if module is within "1 level outside and 2 levels inside" window relative to player.
        
        if (BuildManager.Instance == null || BuildManager.Instance.activeManager == null) return;
        if (innerGrid == null) return; // Not initialized

        // 1. Determine Active Depth (where the player IS)
        // Optimization note: Maybe cache this on BuildManager or ModuleManager?
        int activeDepth = GetManagerDepth(BuildManager.Instance.activeManager);
        
        // 2. Determine Visibility
        // If I am at activeDepth (Sibling on the same grid) -> Show my internals (Depth+1)
        // If I am at activeDepth + 1 (Inside a Sibling) -> Show my internals (Depth+2)
        // "2 levels inside" means we want to see (Active+1) and (Active+2) contents.
        // My Depth is the grid I sit on.
        
        bool inRange = (_depth >= activeDepth && _depth <= activeDepth + 1);
        
        // Special Case: If I AM the container the player is inside, disable my CCTV 
        // (Player sees my insides directly, not through the screen)
        bool isCurrentContainer = (innerGrid == BuildManager.Instance.activeManager);
        
        bool shouldEnable = inRange && !isCurrentContainer;

        if (_previewCamera != null)
        {
            if (_previewCamera.enabled != shouldEnable) 
                _previewCamera.enabled = shouldEnable;
        }
    }
}
