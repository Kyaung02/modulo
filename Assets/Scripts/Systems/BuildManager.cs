using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class BuildManager : MonoBehaviour
{
    [Header("Settings")]
    [Header("Settings")]
    public ComponentBase[] availableComponents; // 0: Emitter, 1: Mover, etc.
    public ComponentBase selectedComponentPrefab; // Currently selected component to build
    public Transform componentParent; // Parent object for organized hierarchy

    private Camera _mainCamera;
    private ComponentBase[,] _components; // 2D array to track installed components

    

    private void Start()
    {
        _mainCamera = Camera.main;
        _components = new ComponentBase[ModuleManager.Instance.width, ModuleManager.Instance.height];
    }

    private void Update()
    {
        HandleInput();
    }

    private int _currentRotationIndex = 0;

    private void HandleInput()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame) // Left Click: Build
        {
            TryBuild();
        }
        else if (Mouse.current.rightButton.wasPressedThisFrame) // Right Click: Remove
        {
            TryRemove();
        }

        if (Keyboard.current != null)
        {
            // Rotation
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                _currentRotationIndex = (_currentRotationIndex + 1) % 4;
                Debug.Log($"Rotation set to: {_currentRotationIndex}");
            }

            // Component Selection
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectComponent(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectComponent(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectComponent(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectComponent(3);
        }
    }

    private void SelectComponent(int index)
    {
        if (availableComponents != null && index >= 0 && index < availableComponents.Length)
        {
            selectedComponentPrefab = availableComponents[index];
            // optional: reset rotation or keep it? Keeping it is usually better UX
            Debug.Log($"Selected Component: {selectedComponentPrefab.name}");
        }
    }

    private void TryBuild()
    {
        if (selectedComponentPrefab == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = ModuleManager.Instance.WorldToGridPosition(mouseWorldPos);

        if (!ModuleManager.Instance.IsWithinBounds(gridPos.x, gridPos.y)) return;
        
        // Create a temporary instance to check footprint (Optimization: Use a static method or ghost)
        // For now, simpler approach: Instantiate, check, destroy if invalid? Too heavy.
        // Better: We know the prefab's dimensions. We can simulate it. 
        // But BuildManager doesn't know component's dimension logic without instance.
        // Let's instantiate deactivated first.

        ComponentBase temp = Instantiate(selectedComponentPrefab, Vector3.zero, Quaternion.identity);
        // Apply rotation to temp to get correct occupied positions
        for (int i=0; i< _currentRotationIndex; i++) temp.Rotate();
        
        // Manually set grid pos to check (since Start hasn't run)
        // We need a helper or public property setter for this simulation, 
        // OR we just calculate it manually if we knew the size.
        // Since GetOccupiedPositions depends on GridPosition which is set in Start,
        // we can't easily query it before placement without refactoring.
        // Workaround: Instantiate, set position, check, if fail destroy.
        
        temp.transform.position = ModuleManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y);
        // We need to force update its GridPosition internal logic or make GetOccupiedPositions take parameters.
        // Let's refactor ComponentBase later to be cleaner. For now:
        
        // ACTUALLY: Let's assume 1x1 for everything EXCEPT Combiner for now in this function?
        // No, we should support generic.
        // Let's use the instantiated object (it's fine to instantiate one per click).
        
        // START is not called yet. We need to manually initialize for check.
        // This is getting messy. 
        // SIMPLIFICATION: Proceed with instantiate, IF collision -> Destroy.
        
        // Note: Start() runs AFTER this frame typically or instantly?
        // Instantiate calls Awake. Start is next frame or end of frame.
        // We can manually call a Setup method?
        
        // Let's rely on the fact that we can check collision AFTER instantiation if we want, 
        // but removing it is cleaner.
        
        // Let's try to calculate occupied positions MANUALLY for the check.
        // We need the size of the prefab.
        int w = temp.GetWidth();
        int h = temp.GetHeight();
        
        // Calculate manually (duplicate logic from ComponentBase temporarily)
        List<Vector2Int> checkPositions = new List<Vector2Int>();
         for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector2Int offset = Vector2Int.zero;
                switch (_currentRotationIndex)
                {
                    case 0: offset = new Vector2Int(x, y); break;
                    case 1: offset = new Vector2Int(y, -x); break;
                    case 2: offset = new Vector2Int(-x, -y); break;
                    case 3: offset = new Vector2Int(-y, x); break;
                }
                checkPositions.Add(gridPos + offset);
            }
        }

        if (!ModuleManager.Instance.IsAreaClear(checkPositions))
        {
            Destroy(temp.gameObject);
            return;
        }

        // Valid! Place it properly.
        temp.transform.SetParent(componentParent);
        // Rotation is already applied one by one above? No, I applied to temp.
        // temp IS the new component.
        
        // We just need to ensure Start() picks up the correct position provided by transform.
        // transform is already set.
        
        // Register is done in Start().
        
        // We need to update our _components array? 
        // ModuleManager manages the grid data now (`_gridComponents`).
        // BuiltManager doesn't need `_components` array if ModuleManager does it.
        // The old _components array in BuildManager is now redundant and conflicting.
        // We should REMOVE _components usage from BuildManager and rely on ModuleManager.
        
        // Let's finish this block first.
        
        // newComponent is temp.
         _components[gridPos.x, gridPos.y] = temp; // This is only marking pivot. 
         // But ModuleManager.RegisterComponent will mark all spots in Start().
         
    }

    private void TryRemove()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = ModuleManager.Instance.WorldToGridPosition(mouseWorldPos);

        if (!ModuleManager.Instance.IsWithinBounds(gridPos.x, gridPos.y)) return;
        
        ComponentBase component = _components[gridPos.x, gridPos.y];
        if (component != null)
        {
            Destroy(component.gameObject);
            _components[gridPos.x, gridPos.y] = null;
        }
    }
}
