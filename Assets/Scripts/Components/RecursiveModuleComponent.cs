using UnityEngine;

public class RecursiveModuleComponent : ComponentBase
{
    // 1x1 External Size
    public override int GetWidth() => 1;
    public override int GetHeight() => 1;

    [Header("Inner World")]
    [HideInInspector] public ModuleManager innerGrid; // Auto-generated
    private Transform innerWorldRoot; 

    private void Awake()
    {
        InitializeInnerWorld();
    }

    private void InitializeInnerWorld()
    {
        // ... (Existing code) ...
        innerWorldRoot = new GameObject($"InnerWorld_{GetInstanceID()}").transform;
        
        // Move far away to allow visual separation (or use layers later)
        innerWorldRoot.position = new Vector3(1000 + (GetInstanceID() % 100) * 100, 1000 + (GetInstanceID() / 100) * 100, 0);
        
        innerGrid = innerWorldRoot.gameObject.AddComponent<ModuleManager>();
        innerGrid.width = 7;
        innerGrid.height = 7;
        innerGrid.cellSize = 1.0f;
        innerGrid.originPosition = new Vector2(-3.5f, -3.5f);
        
        // Link parent
        innerGrid.parentManager = _assignedManager;
        
        // Auto-add Visualizer so we can see the grid
        var viz = innerWorldRoot.gameObject.AddComponent<GridVisualizer>();
        // Optional: Set formatting or color for inner world? 
        // viz.lineMaterial = ... (We rely on default missing or setup)
        
        CreateInternalPorts();
    }
        


    private void CreateInternalPorts()
    {
        // Create 4 ports
        // Up: (3, 6)
        SpawnPort(new Vector2Int(3, 6), Direction.Up, Direction.Down);
        // Right: (6, 3)
        SpawnPort(new Vector2Int(6, 3), Direction.Right, Direction.Left);
        // Down: (3, 0)
        SpawnPort(new Vector2Int(3, 0), Direction.Down, Direction.Up);
        // Left: (0, 3)
        SpawnPort(new Vector2Int(0, 3), Direction.Left, Direction.Right);
    }

    private void SpawnPort(Vector2Int gridPos, Direction wallDir, Direction facingDir)
    {
        // We need a PortComponent Prefab ideally, or create dynamic
        GameObject portObj = new GameObject($"Port_{wallDir}");
        portObj.transform.SetParent(innerGrid.transform);
        PortComponent port = portObj.AddComponent<PortComponent>();
        
        // Manually setup visual? (Simple Cube for now)
        GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Quad);
        vis.transform.SetParent(portObj.transform);
        vis.transform.localPosition = Vector3.zero;
        Destroy(vis.GetComponent<Collider>()); // Visual only
        if (vis.GetComponent<Renderer>()) 
            vis.GetComponent<Renderer>().material.color = Color.magenta;

        // Force register to inner grid
        // We can't rely on Start() auto-finding parent because we just created it.
        // But we parented it to innerGrid logic?
        // ComponentBase Start() looks for ModuleManager in parent.
        // portObj.parent = innerGrid.transform.
        // So it should work!
        
        // Set Position
        portObj.transform.position = innerGrid.GridToWorldPosition(gridPos.x, gridPos.y);

        // Configure Logic
        port.Configure(this, wallDir);
        
        // Force Rotation so it pushes INWARDS
        // ComponentBase rotation is private setter for now...
        // We assume we can rotate it via Rotate() method or we need to expose setter?
        // We can call Rotate() until it matches.
        int targetRot = (int)facingDir;
        while ((int)port.RotationIndex != targetRot)
        {
            port.Rotate();
        }
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

    private PortComponent FindPort(Direction dir)
    {
        // Inefficient lookup, better cache
        // Inner grid size 7x7.
        Vector2Int targetPos = Vector2Int.zero;
        switch(dir)
        {
             case Direction.Up: targetPos = new Vector2Int(3, 6); break;
             case Direction.Down: targetPos = new Vector2Int(3, 0); break;
             case Direction.Left: targetPos = new Vector2Int(0, 3); break;
             case Direction.Right: targetPos = new Vector2Int(6, 3); break;
        }
        
        ComponentBase c = innerGrid.GetComponentAt(targetPos);
        return c as PortComponent;
    }
    
    // --- Interaction ---
    
    // Allow entering the module
    void OnMouseDown()
    {
        // Simple double click or mode switch logic?
        // Using BuildManager to switch context
        // We need to access BuildManager singleton or find it.
        BuildManager bm = FindFirstObjectByType<BuildManager>();
        if (bm != null)
        {
            Debug.Log($"Entering Module {name}...");
            
            // Move Camera to Inner World
            Camera.main.transform.position = new Vector3(innerGrid.originPosition.x + 3.5f, innerGrid.originPosition.y + 3.5f, -10);
            Camera.main.orthographicSize = 5; // Reset zoom
            
            // Set Context
            bm.SetActiveManager(innerGrid);
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
