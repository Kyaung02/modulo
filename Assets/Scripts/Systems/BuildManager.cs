using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class BuildManager : MonoBehaviour
{
    public static BuildManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("Settings")]
    [Header("Settings")]
    public ComponentBase[] availableComponents; // 0: Emitter, 1: Mover, etc.
    public ComponentBase selectedComponentPrefab; // Currently selected component to build
    public Transform componentParent; // Parent object for organized hierarchy

    public Camera _mainCamera;
    private ComponentBase[,] _components; // 2D array to track installed components
    
    // Preview Manager Reference
    public PreviewManager previewManager;

    public int _currentRotationIndex = 0;
    public int _currentFlipIndex = 0;
    public ModuleManager activeManager;

    /// <summary> RecursiveModule is never rotated; others use _currentRotationIndex. </summary>
    private int GetEffectiveRotationIndex()
    {
        return (selectedComponentPrefab is RecursiveModuleComponent) ? 0 : _currentRotationIndex;
    } 


    private void Start()
    {
        _mainCamera = Camera.main;
        
        // Default to global instance
        if (activeManager == null) activeManager = ModuleManager.Instance;
        
        // Ensure CameraController exists
        if (_mainCamera.GetComponent<CameraController>() == null)
        {
            _mainCamera.gameObject.AddComponent<CameraController>();
        }
        
        // _components array in BuildManager is redundant if ModuleManager handles it.
        // We should rely on activeManager for logic.

        _components = new ComponentBase[activeManager.width, activeManager.height];
        
        if (previewManager == null)
        {
            previewManager = FindFirstObjectByType<PreviewManager>();
        }

        if (previewManager == null) Debug.LogError("BuildManager: Could not find PreviewManager in Scene!");
        else Debug.Log("BuildManager: Successfully connected to PreviewManager.");
        SelectComponent(0);
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
            var parent = activeManager.parentManager;
            
            // 1. Module Position (Start of Exit)
            Vector3 modulePos = Vector3.zero;
            if (activeManager.ownerComponent != null)
            {
               modulePos = activeManager.ownerComponent.transform.position;
               
               // Hook for cleanup (Disable Outer Preview)
               if (activeManager.ownerComponent is RecursiveModuleComponent rm)
               {
                   rm.ExitModule();
               }
            }
            else
            {
               // Fallback: Use parent center if owner not found (shouldn't happen for recursive modules)
               modulePos = new Vector3(parent.originPosition.x + 3.5f, parent.originPosition.y + 3.5f, 0) + parent.transform.position;
            }

            // 2. Parent Center Position (End of Exit)
            Vector3 parentCenter = new Vector3(parent.originPosition.x + 3.5f, parent.originPosition.y + 3.5f, -10) + parent.transform.position;
            
            if (CameraController.Instance != null)
            {
                StartCoroutine(CameraController.Instance.TransitionExitModule(modulePos, parentCenter, () => {
                    SetActiveManager(parent);
                }));
            }
            else
            {
                // Fallback
                SetActiveManager(parent);
                Camera.main.transform.position = parentCenter;
                Camera.main.orthographicSize = 5;
            }
        }
    }

    private void Update()
    {
        HandleInput();
        if (previewManager != null)
        {
            previewManager.UpdatePreview(this);
        }
    }


    private void HandleInput()
    {
        // Block input if camera is transitioning
        if (CameraController.Instance != null && CameraController.Instance.IsTransitioning) return;

        if (Mouse.current == null) return;

        
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (selectedComponentPrefab != null)TryBuild();
            else TryInspect();
        }
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryRemove();
        }

        if (Keyboard.current != null)
        {
            // Rotation (disabled for RecursiveModule)
            if (Keyboard.current.rKey.wasPressedThisFrame && !(selectedComponentPrefab is RecursiveModuleComponent) && !(selectedComponentPrefab is null))
            {
                _currentRotationIndex = (_currentRotationIndex + 1) % 4;
                //Debug.Log($"Rotation set to: {_currentRotationIndex}");
            }

            // Interact
            if(Keyboard.current.eKey.wasPressedThisFrame){
                TryInteract();
            }

            // Flip (enabled only for IFlippable components)
            if(Keyboard.current.tKey.wasPressedThisFrame && selectedComponentPrefab is IFlippable){
                TryFlip();
            }

            // Clear Selection
            if(Keyboard.current.xKey.wasPressedThisFrame){
                selectedComponentPrefab = null;
                Debug.Log("Selection Cleared");
            }

            // Component Selection
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectComponent(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectComponent(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectComponent(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectComponent(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) SelectComponent(4);
            
        }
    }

    public event System.Action<int> OnComponentSelected;

    private void SelectComponent(int index)
    {
        _currentRotationIndex=0;
        _currentFlipIndex=0;
        if (availableComponents != null && index >= 0 && index < availableComponents.Length)
        {
            selectedComponentPrefab = availableComponents[index];
            Debug.Log($"Selected Component: {selectedComponentPrefab.name}");
            
            OnComponentSelected?.Invoke(index);
        }
    }

    public bool CheckCollision(ComponentBase component, Vector2Int gridPos)
    {
        if (selectedComponentPrefab == null) return true;
        if (activeManager == null) return true;

        if (!activeManager.IsWithinBounds(gridPos.x, gridPos.y)) return true;
        
        // Check for validity using simulated footprint
        int rot = GetEffectiveRotationIndex();
        ComponentBase temp = Instantiate(selectedComponentPrefab, Vector3.zero, Quaternion.identity);
        for (int i=0; i< rot; i++) temp.Rotate();
        
        int w = temp.GetWidth();
        int h = temp.GetHeight();
        
        List<Vector2Int> checkPositions = new List<Vector2Int>();
         for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                // Logic copy from ComponentBase (Create static helper later!)
                Vector2Int offset = Vector2Int.zero;
                switch (rot)
                {
                    case 0: offset = new Vector2Int(x, y); break;
                    case 1: offset = new Vector2Int(y, -x); break;
                    case 2: offset = new Vector2Int(-x, -y); break;
                    case 3: offset = new Vector2Int(-y, x); break;
                }
                checkPositions.Add(gridPos + offset);
            }
        }

        bool r_val=activeManager.IsAreaClear(checkPositions);
        Destroy(temp.gameObject);
        return !r_val;
    }

    private bool TryBuild(Vector2Int gridPos)
    {
        if (selectedComponentPrefab == null) return false;
        if (activeManager == null) return false;

        if (!activeManager.IsWithinBounds(gridPos.x, gridPos.y)) return false;
        
        ComponentBase temp = Instantiate(selectedComponentPrefab, Vector3.zero, Quaternion.identity);
        int w = temp.GetWidth();
        int h = temp.GetHeight();
        
        // Check for validity using simulated footprint
        int rot = GetEffectiveRotationIndex();
        for (int i=0; i< rot; i++) temp.Rotate();
        
        if(_currentFlipIndex==1){
            if(temp is IFlippable flippable){
                //Debug.Log("Flipping Component");
                flippable.isFlipped=1;
                Vector3 s = temp.transform.localScale;
                temp.transform.localScale = new Vector3(-1f*s.x, s.y, s.z);
                if (w > 1)
                {
                    // Remove cellSize usage as gridPos is integer coordinate
                    Vector3 worldOffset = temp.transform.rotation * Vector3.right * (w-1);
                    Vector2Int gridOffset = new Vector2Int(Mathf.RoundToInt(worldOffset.x), Mathf.RoundToInt(worldOffset.y));
                    gridPos += gridOffset;
                }
            }
            else {
                Destroy(temp.gameObject);
                return false; 
            }
        }

        List<Vector2Int> checkPositions = temp.GetOccupiedPositions();
        
        // Use temp's GridPosition logic simulation? 
        // ComponentBase's grid position is not set yet.
        // We need to shift "checkPositions" (which are local relative to pivot) by gridPos
        
        // Wait, GetOccupiedPositions uses GridPosition property which is 0,0 right now.
        // So we add gridPos to each.
        for(int i=0; i<checkPositions.Count; i++){
            checkPositions[i] += gridPos;
        }

        if (!activeManager.IsAreaClear(checkPositions))
        {
            if(temp is MoverComponent && activeManager.GetComponentAt(gridPos) is MoverComponent)
            {
                TryRemove();
            }
            else{
                Destroy(temp.gameObject);
                return;
            }
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

    private void TryInteract()
    {
        if (activeManager == null) return;
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return; // Block UI

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = activeManager.WorldToGridPosition(mouseWorldPos);

        if (!activeManager.IsWithinBounds(gridPos.x, gridPos.y)) return;

        ComponentBase component = activeManager.GetComponentAt(gridPos);
        if (component != null)
        {
            // Check if it is a RecursiveModule
            if (component is RecursiveModuleComponent recursiveModule)
            {
                recursiveModule.EnterModule();
            }
            else
            {
                Debug.Log($"Clicked on {component.name}");
                // Other interactions (Info panel?) could go here
            }
        }
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
            // Cannot remove Collector
            if (component is CollectorComponent) return;

            Destroy(component.gameObject);
            // Unregister handled in OnDestroy
        }
    }

    private void TryInspect()
    {
        //자세한 정보 보기. To be added...
    }

    private void TryFlip()
    {
        if (selectedComponentPrefab == null) return;
        if (selectedComponentPrefab is not CombinerComponent && selectedComponentPrefab is not DistributerComponent) return;
        _currentFlipIndex = (_currentFlipIndex + 1) % 2;
        Debug.Log($"Flipped to: {_currentFlipIndex}");
    }
}
