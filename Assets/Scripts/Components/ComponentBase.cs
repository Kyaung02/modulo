using UnityEngine;
using System.Collections.Generic;

public class ComponentBase : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }
    
    [Header("Settings")]
    [SerializeField] 
    protected Direction _rotationIndex = Direction.Up;

    public Direction RotationIndex 
    { 
        get => _rotationIndex; 
        private set => _rotationIndex = value; 
    }

    public WordData HeldWord { get; protected set; } // The word currently in this component

    // Public method to clear held word (for external components)
    public void ClearHeldWord()
    {
        HeldWord = null;
        UpdateVisuals();
    }

    public virtual int GetWidth() => 1;
    public virtual int GetHeight() => 1;

    public List<Vector2Int> GetOccupiedPositions()
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        int w = GetWidth();
        int h = GetHeight();

        // 0: Up (Normal), 1: Right (90 deg CW), 2: Down, 3: Left
        // Need to rotate the local footprint (0,0) to (w-1, h-1) based on RotationIndex
        
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                // Simple rotation logic
                Vector2Int offset = Vector2Int.zero;
                switch (RotationIndex)
                {
                    case Direction.Up: offset = new Vector2Int(x, y); break;
                    case Direction.Right: offset = new Vector2Int(y, -x); break; // x becomes y, y becomes -x
                    case Direction.Down: offset = new Vector2Int(-x, -y); break;
                    case Direction.Left: offset = new Vector2Int(-y, x); break;
                }
                positions.Add(GridPosition + offset);
            }
        }
        return positions;
    }

    protected ModuleManager _assignedManager;

    protected virtual void Start()
    {
        // 1. Find our manager if not assigned
        if (_assignedManager == null)
        {
            _assignedManager = GetComponentInParent<ModuleManager>();
            
            // Fallback to global instance if not found in parent (Legacy support)
            if (_assignedManager == null)
            {
                _assignedManager = ModuleManager.Instance;
            }
        }
        
        if (_assignedManager == null)
        {
            Debug.LogError($"Component {name} could not find a ModuleManager!");
            return;
        }

        SnapToGrid();
        
        // Register to Manager (Now handles multiple cells)
        _assignedManager.RegisterComponent(this);
        
        // Register to TickManager
        if (TickManager.Instance != null)
        {
            TickManager.Instance.OnTick += OnTick;
        }
    }

    [ContextMenu("Snap to Grid")]
    public void SnapToGrid()
    {
        if (_assignedManager != null)
        {
            // Align to grid using pivot (GridPosition)
            GridPosition = _assignedManager.WorldToGridPosition(transform.position);
            
            // Note: For multi-cell, visual position might need offset if pivot is corner
            transform.position = _assignedManager.GridToWorldPosition(GridPosition.x, GridPosition.y); 
        }
    }

    // Returns true if the component successfully accepted the word
    public virtual bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        if (HeldWord == null)
        {
            HeldWord = word;
            UpdateVisuals();
            return true;
        }
        return false;
    }

    protected virtual void OnTick(long tickCount)
    {
        // Default behavior: Try to push word to the next component
        if (HeldWord != null)
        {
            Vector2Int targetPos = GridPosition + GetOutputDirection();
            // Use local manager
            ComponentBase targetComponent = _assignedManager != null ? _assignedManager.GetComponentAt(targetPos) : null;

            if (targetComponent != null)
            {
                // Try to give the word to the target
                if (targetComponent.AcceptWord(HeldWord, GetOutputDirection(), targetPos))
                {
                    HeldWord = null; // Successfully passed the word
                    UpdateVisuals();
                }
            }
        }
    }

    protected virtual void UpdateVisuals()
    {
        // Override for visual updates
    }


    protected virtual void OnDestroy()
    {
        if (_assignedManager != null)
        {
            _assignedManager.UnregisterComponent(this);
        }

        if (TickManager.Instance != null)
        {
            TickManager.Instance.OnTick -= OnTick;
        }
    }

    public virtual void Rotate()
    {
        RotationIndex = (Direction)(((int)RotationIndex + 1) % 4);
        UpdateRotationVisual();
    }
    
    protected virtual void OnValidate()
    {
        // Apply rotation in Editor when Inspector value changes
        UpdateRotationVisual();
    }

    private void UpdateRotationVisual()
    {
        transform.rotation = Quaternion.Euler(0, 0, -90 * (int)RotationIndex);
    }
    
    public Vector2Int GetOutputDirection()
    {
        switch (RotationIndex)
        {
            case Direction.Up: return Vector2Int.up;
            case Direction.Right: return Vector2Int.right;
            case Direction.Down: return Vector2Int.down;
            case Direction.Left: return Vector2Int.left;
            default: return Vector2Int.up;
        }
    }

}
