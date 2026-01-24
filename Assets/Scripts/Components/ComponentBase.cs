using UnityEngine;

public class ComponentBase : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }
    public int RotationIndex { get; private set; } // 0: Up, 1: Right, 2: Down, 3: Left

    public WordData HeldWord { get; protected set; } // The word currently in this component

    protected virtual void Start()
    {
        // Align to grid on start
        GridPosition = ModuleManager.Instance.WorldToGridPosition(transform.position);
        transform.position = ModuleManager.Instance.GridToWorldPosition(GridPosition.x, GridPosition.y);
        
        // Register to Manager
        ModuleManager.Instance.RegisterComponent(this);

        // Register to TickManager
        if (TickManager.Instance != null)
        {
            TickManager.Instance.OnTick += OnTick;
        }
    }

    // Returns true if the component successfully accepted the word
    public virtual bool AcceptWord(WordData word, Vector2Int direction)
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
                if (targetComponent.AcceptWord(HeldWord, GetOutputDirection()))
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
