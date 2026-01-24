using UnityEngine;
using System.Collections.Generic;

public class ComponentBase : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }
    public int RotationIndex { get; private set; } // 0: Up, 1: Right, 2: Down, 3: Left

    public WordData HeldWord { get; protected set; } // The word currently in this component

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
                    case 0: offset = new Vector2Int(x, y); break;
                    case 1: offset = new Vector2Int(y, -x); break; // x becomes y, y becomes -x
                    case 2: offset = new Vector2Int(-x, -y); break;
                    case 3: offset = new Vector2Int(-y, x); break;
                }
                positions.Add(GridPosition + offset);
            }
        }
        return positions;
    }

    protected virtual void Start()
    {
        // Align to grid on start using pivot (GridPosition)
        GridPosition = ModuleManager.Instance.WorldToGridPosition(transform.position);
        
        // Note: For multi-cell, visual position might need offset if pivot is corner
        transform.position = ModuleManager.Instance.GridToWorldPosition(GridPosition.x, GridPosition.y); 
        
        // Register to Manager (Now handles multiple cells)
        ModuleManager.Instance.RegisterComponent(this);
        
        // Register to TickManager
        if (TickManager.Instance != null)
        {
            TickManager.Instance.OnTick += OnTick;
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
            ComponentBase targetComponent = ModuleManager.Instance.GetComponentAt(targetPos);

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
        if (ModuleManager.Instance != null)
        {
            ModuleManager.Instance.UnregisterComponent(this);
        }

        if (TickManager.Instance != null)
        {
            TickManager.Instance.OnTick -= OnTick;
        }
    }

    public void Rotate()
    {
        RotationIndex = (RotationIndex + 1) % 4;
        transform.rotation = Quaternion.Euler(0, 0, -90 * RotationIndex);
    }
    
    public Vector2Int GetOutputDirection()
    {
        switch (RotationIndex)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.right;
            case 2: return Vector2Int.down;
            case 3: return Vector2Int.left;
            default: return Vector2Int.up;
        }
    }
}
