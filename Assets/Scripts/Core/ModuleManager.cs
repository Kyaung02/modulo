using UnityEngine;

public class ModuleManager : MonoBehaviour
{
    public static ModuleManager Instance { get; private set; }

    [Header("Grid Settings")]
    public int width = 7;
    public int height = 7;
    public float cellSize = 1.0f;
    public Vector2 originPosition = new Vector2(-3.5f, -3.5f); // Centers the 7x7 grid

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Vector3 GridToWorldPosition(int x, int y)
    {
        return new Vector3(x * cellSize + originPosition.x + (cellSize * 0.5f), y * cellSize + originPosition.y + (cellSize * 0.5f), 0);
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - originPosition.x) / cellSize);
        int y = Mathf.FloorToInt((worldPosition.y - originPosition.y) / cellSize);
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
        _gridComponents[component.GridPosition.x, component.GridPosition.y] = component;
    }

    public void UnregisterComponent(ComponentBase component)
    {
        if (_gridComponents != null)
            _gridComponents[component.GridPosition.x, component.GridPosition.y] = null;
    }

    public ComponentBase GetComponentAt(Vector2Int pos)
    {
        if (!IsWithinBounds(pos.x, pos.y) || _gridComponents == null) return null;
        return _gridComponents[pos.x, pos.y];
    }

    // Debugging Gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = new Vector3(x * cellSize + originPosition.x, originPosition.y, 0);
            Vector3 end = new Vector3(x * cellSize + originPosition.x, originPosition.y + height * cellSize, 0);
            Gizmos.DrawLine(start, end);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 start = new Vector3(originPosition.x, y * cellSize + originPosition.y, 0);
            Vector3 end = new Vector3(originPosition.x + width * cellSize, y * cellSize + originPosition.y, 0);
            Gizmos.DrawLine(start, end);
        }
    }
}
