using UnityEngine;
using System.Collections.Generic;

public class ModuleManager : MonoBehaviour
{
    public static ModuleManager Instance { get; private set; }

    [Header("Resources")]
    public RecipeDatabase recipeDatabase; // Assign in Inspector

    [Header("Grid Settings")]
    public int width = 7;
    public int height = 7;
    public float cellSize = 1.0f;
    public Vector2 originPosition = new Vector2(-3.5f, -3.5f); // Centers the 7x7 grid

    public ModuleManager parentManager; // Link to the outer world for recursion navigation
    public ComponentBase ownerComponent; // The component (RecursiveModule) that owns this inner world
    
    private void Awake()
    {
        // If this is the first manager (likely the main world), set it as Instance.
        // But do NOT destroy other instances, as they will be inner worlds.
        if (Instance == null)
        {
            Instance = this;
        }
        
        // Initialize other things if needed
    }

    public Vector3 GridToWorldPosition(int x, int y)
    {
        // Add transform.position to offset based on where this manager is located
        Vector3 worldOffset = transform.position;
        return worldOffset + new Vector3(x * cellSize + originPosition.x + (cellSize * 0.5f), y * cellSize + originPosition.y + (cellSize * 0.5f), 0);
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        // Subtract transform.position to get local pos relative to manager
        Vector3 localPos = worldPosition - transform.position;
        
        int x = Mathf.FloorToInt((localPos.x - originPosition.x) / cellSize);
        int y = Mathf.FloorToInt((localPos.y - originPosition.y) / cellSize);
        return new Vector2Int(x, y);
    }

    public bool IsWithinBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    // Component Lookup System
    private ComponentBase[,] _gridComponents;

    public void RegisterComponent(ComponentBase component)
    {
        if (_gridComponents == null) _gridComponents = new ComponentBase[width, height];
        
        foreach (var pos in component.GetOccupiedPositions())
        {
            if (IsWithinBounds(pos.x, pos.y))
            {
                _gridComponents[pos.x, pos.y] = component;
            }
        }
    }

    public void UnregisterComponent(ComponentBase component)
    {
        if (_gridComponents == null) return;

        foreach (var pos in component.GetOccupiedPositions())
        {
            if (IsWithinBounds(pos.x, pos.y) && _gridComponents[pos.x, pos.y] == component)
            {
                _gridComponents[pos.x, pos.y] = null;
            }
        }
    }

    public ComponentBase GetComponentAt(Vector2Int pos)
    {
        if (!IsWithinBounds(pos.x, pos.y) || _gridComponents == null) return null;
        return _gridComponents[pos.x, pos.y];
    }
    
    // Check if an area is clear for building
    public bool IsAreaClear(List<Vector2Int> positions)
    {
        foreach (var pos in positions)
        {
            if (!IsWithinBounds(pos.x, pos.y) || GetComponentAt(pos) != null)
            {
                return false;
            }
        }
        return true;
    }

    // Debugging Gizmos
    // Debugging Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        Vector3 offset = transform.position; // Current world position
        
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = offset + new Vector3(x * cellSize + originPosition.x, originPosition.y, 0);
            Vector3 end = offset + new Vector3(x * cellSize + originPosition.x, originPosition.y + height * cellSize, 0);
            Gizmos.DrawLine(start, end);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 start = offset + new Vector3(originPosition.x, y * cellSize + originPosition.y, 0);
            Vector3 end = offset + new Vector3(originPosition.x + width * cellSize, y * cellSize + originPosition.y, 0);
            Gizmos.DrawLine(start, end);
        }
    }
}
