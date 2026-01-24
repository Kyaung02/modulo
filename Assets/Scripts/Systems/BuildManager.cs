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
    
    // Dynamic Manager Reference
    public ModuleManager activeManager; 

    private void Start()
    {
        _mainCamera = Camera.main;
        // Default to global instance
        if (activeManager == null) activeManager = ModuleManager.Instance;
        
        // _components array in BuildManager is redundant if ModuleManager handles it.
        // We should rely on activeManager for logic.
    }

    // Call this when entering/exiting modules
    public void SetActiveManager(ModuleManager manager)
    {
        activeManager = manager;
        // Notify UI or Camera to update?
        // UI event could be useful here.
    }

    public void ExitCurrentModule()
    {
        if (activeManager != null && activeManager.parentManager != null)
        {
            Debug.Log("Exiting Module...");
            SetActiveManager(activeManager.parentManager);
            
            // Move Camera back (Simple logic: center on parent grid)
            // Or ideally store camera state. For now simplify:
            Camera.main.transform.position = new Vector3(activeManager.originPosition.x + 3.5f, activeManager.originPosition.y + 3.5f, -10);
            Camera.main.orthographicSize = 5;
        }
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

    public event System.Action<int> OnComponentSelected;

    private void SelectComponent(int index)
    {
        if (availableComponents != null && index >= 0 && index < availableComponents.Length)
        {
            selectedComponentPrefab = availableComponents[index];
            // optional: reset rotation or keep it? Keeping it is usually better UX
            Debug.Log($"Selected Component: {selectedComponentPrefab.name}");
            
            OnComponentSelected?.Invoke(index);
        }
    }

    private void TryBuild()
    {
        if (selectedComponentPrefab == null) return;
        if (activeManager == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = activeManager.WorldToGridPosition(mouseWorldPos);

        if (!activeManager.IsWithinBounds(gridPos.x, gridPos.y)) return;
        
        // Check for validity using simulated footprint
        ComponentBase temp = Instantiate(selectedComponentPrefab, Vector3.zero, Quaternion.identity);
        for (int i=0; i< _currentRotationIndex; i++) temp.Rotate();
        
        int w = temp.GetWidth();
        int h = temp.GetHeight();
        
        List<Vector2Int> checkPositions = new List<Vector2Int>();
         for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                // Logic copy from ComponentBase (Create static helper later!)
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

        if (!activeManager.IsAreaClear(checkPositions))
        {
            Destroy(temp.gameObject);
            return;
        }

        // Valid! Place it properly.
        // We must set parent to the active manager's transform (or a container inside it)
        // Optimization: ModuleManager should have a public Transform componentContainer
        // For now, assume activeManager.transform is the parent.
        temp.transform.SetParent(activeManager.transform);
        
        temp.transform.position = activeManager.GridToWorldPosition(gridPos.x, gridPos.y);
        
        // Rotation is already applied to temp.
        
        // Note: ComponentBase.Start() will run and register itself to GetComponentInParent<ModuleManager>().
        // Since we parented it to activeManager, it will find it!
    }

    private void TryRemove()
    {
        if (activeManager == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = activeManager.WorldToGridPosition(mouseWorldPos);

        if (!activeManager.IsWithinBounds(gridPos.x, gridPos.y)) return;
        
        ComponentBase component = activeManager.GetComponentAt(gridPos);
        if (component != null)
        {
            Destroy(component.gameObject);
            // Unregister handled in OnDestroy
        }
    }
}
