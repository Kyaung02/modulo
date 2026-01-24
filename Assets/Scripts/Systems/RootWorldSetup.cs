using UnityEngine;
using System.Collections.Generic;

public class RootWorldSetup : MonoBehaviour
{
    [Header("Elemental Sources")]
    public WordData fireWord;
    public WordData waterWord;
    public WordData earthWord;
    public WordData windWord;

    private void Start()
    {
        // Ensure we are operating on the Root ModuleManager
        ModuleManager rootManager = ModuleManager.Instance;
        if (rootManager == null)
        {
            Debug.LogError("RootWorldSetup: No ModuleManager found!");
            return;
        }

        // Only setup if this manager has NO owner component (meaning it IS the root)
        if (rootManager.ownerComponent != null)
        {
            Debug.LogWarning("RootWorldSetup: ModuleManager has owner, seemingly not root. Skipping.");
            return;
        }

        SetupRootPorts(rootManager);
    }

    private void SetupRootPorts(ModuleManager grid)
    {
        // Top Wall (Up) -> Fire
        SpawnSourcePort(grid, new Vector2Int(3, 7), Direction.Up, Direction.Down, fireWord, Color.red);
        
        // Right Wall -> Water
        SpawnSourcePort(grid, new Vector2Int(7, 3), Direction.Right, Direction.Left, waterWord, Color.blue);
        
        // Bottom Wall (Down) -> Earth
        SpawnSourcePort(grid, new Vector2Int(3, -1), Direction.Down, Direction.Up, earthWord, new Color(0.6f, 0.4f, 0.2f));
        
        // Left Wall -> Wind
        SpawnSourcePort(grid, new Vector2Int(-1, 3), Direction.Left, Direction.Right, windWord, Color.cyan);
    }

    private void SpawnSourcePort(ModuleManager grid, Vector2Int gridPos, Direction wallDir, Direction facingDir, WordData word, Color col)
    {
        GameObject portObj = new GameObject($"RootSource_{wallDir}_{word?.name}");
        portObj.transform.SetParent(grid.transform);
        PortComponent port = portObj.AddComponent<PortComponent>();
        
        // Visuals - Thin Colored Line on Wall (Source Style)
        GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Quad);
        vis.transform.SetParent(portObj.transform);
        
        // Scale: Wide but thinner 
        vis.transform.localScale = new Vector3(1.0f, 0.2f, 1.0f);
        vis.transform.localPosition = new Vector3(0, 0.4f, -0.01f);
        
        Destroy(vis.GetComponent<Collider>()); 
        
        var ren = vis.GetComponent<Renderer>();
        if (ren) 
        {
            ren.material = new Material(Shader.Find("Sprites/Default")); 
            ren.material.color = col; 
            ren.sortingOrder = 5; 
        }

        // Position
        portObj.transform.position = grid.GridToWorldPosition(gridPos.x, gridPos.y);

        // Configure Logic
        port.parentModule = null; // No parent for Root
        port.wallDirection = wallDir;
        port.infiniteSourceWord = word;
        
        // Rotation
        int targetRot = (int)facingDir;
        while ((int)port.RotationIndex != targetRot)
        {
            port.Rotate();
        }
    }
}
