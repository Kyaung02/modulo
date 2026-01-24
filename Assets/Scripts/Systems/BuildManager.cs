using UnityEngine;
using UnityEngine.InputSystem;

public class BuildManager : MonoBehaviour
{

    [Header("Settings")]
    [Header("Settings")]
    public ComponentBase[] availableComponents; // 0: Emitter, 1: Mover, etc.
    public ComponentBase selectedComponentPrefab; // Currently selected component to build
    public Transform componentParent; // Parent object for organized hierarchy

    private Camera _mainCamera;
    private ComponentBase[,] _components; // 2D array to track installed components
    
    // Preview Manager Reference
    public PreviewManager previewManager;

    public int _currentRotationIndex = 0;

    private void Start()
    {
        _mainCamera = Camera.main;
        _components = new ComponentBase[ModuleManager.Instance.width, ModuleManager.Instance.height];
        
        if (previewManager == null)
        {
            previewManager = FindFirstObjectByType<PreviewManager>();
        }

        if (previewManager == null) Debug.LogError("BuildManager: Could not find PreviewManager in Scene!");
        else Debug.Log("BuildManager: Successfully connected to PreviewManager.");
    }

    private void Update()
    {
        HandleInput();
        if (previewManager != null)
        {
            if (selectedComponentPrefab != null)
                 previewManager.UpdatePreview(selectedComponentPrefab, _currentRotationIndex);
        }
    }


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
        if (_components[gridPos.x, gridPos.y] != null) return; // Already occupied

        // Instantiate and place
        Vector3 spawnPos = ModuleManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y);
        ComponentBase newComponent = Instantiate(selectedComponentPrefab, spawnPos, Quaternion.identity, componentParent);
        
        // Apply Rotation
        for (int i = 0; i < _currentRotationIndex; i++)
        {
            newComponent.Rotate();
        }

        _components[gridPos.x, gridPos.y] = newComponent;
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
