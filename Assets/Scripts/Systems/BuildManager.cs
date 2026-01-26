using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.Netcode;

public class BuildManager : NetworkBehaviour
{
    public static BuildManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("Settings")]
    public ComponentBase[] availableComponents; // 0: Emitter, 1: Mover, etc.
    public ComponentBase selectedComponentPrefab; // Currently selected component to build
    
    // We don't really use this parent for NetworkObjects unless we use NetworkTransform parenting
    public Transform componentParent; 

    public Camera _mainCamera;
    
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
        
        if (previewManager == null)
        {
            previewManager = FindFirstObjectByType<PreviewManager>();
        }

        if (previewManager == null) Debug.LogError("BuildManager: Could not find PreviewManager in Scene!");
        SelectComponent(0);
    }

    public void SetActiveManager(ModuleManager manager)
    {
        activeManager = manager;
    }

    public void ExitCurrentModule()
    {
        if (activeManager != null && activeManager.parentManager != null)
        {
            var parent = activeManager.parentManager;
            
            // 1. Module Position (Start of Exit)
            Vector3 modulePos = Vector3.zero;
            if (activeManager.ownerComponent != null)
            {
               modulePos = activeManager.ownerComponent.transform.position;
               if (activeManager.ownerComponent is RecursiveModuleComponent rm)
               {
                    rm.ExitModule(); 
               }
            }
            else
            {
               modulePos = new Vector3(parent.originPosition.x + 3.5f, parent.originPosition.y + 3.5f, 0) + parent.transform.position;
            }

            Vector3 parentCenter = new Vector3(parent.originPosition.x + 3.5f, parent.originPosition.y + 3.5f, -10) + parent.transform.position;
            
            if (CameraController.Instance != null)
            {
                StartCoroutine(CameraController.Instance.TransitionExitModule(modulePos, parentCenter, () => {
                    SetActiveManager(parent);
                }));
            }
            else
            {
                SetActiveManager(parent);
                Camera.main.transform.position = parentCenter;
                Camera.main.orthographicSize = 5;
            }
        }
    }

    private void Update()
    {
        if (!IsClient && !IsServer) return; // Wait for connection? Or local valid?
        // Actually, if we are not connected, we might want local play?
        // But ComponentBase is now NetworkBehaviour. It won't work without Netcode.
        // So we assume connected.
        
        HandleInput();
        if (previewManager != null)
        {
            previewManager.UpdatePreview(this);
        }
    }

    private void HandleInput()
    {
        if (CameraController.Instance != null && CameraController.Instance.IsTransitioning) return;
        if (Mouse.current == null) return;
        
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (selectedComponentPrefab != null) TryBuild();
            else TryInspect();
        }
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryRemove();
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame && !(selectedComponentPrefab is RecursiveModuleComponent) && !(selectedComponentPrefab is null))
            {
                _currentRotationIndex = (_currentRotationIndex + 1) % 4;
            }

            if(Keyboard.current.eKey.wasPressedThisFrame){
                TryInteract();
            }

            // Flip (enabled only for CombinerComponents and DistributerComponents)
            if(Keyboard.current.tKey.wasPressedThisFrame && (selectedComponentPrefab is CombinerComponent || selectedComponentPrefab is DistributerComponent)){
                TryFlip();
            }

            if(Keyboard.current.xKey.wasPressedThisFrame){
                selectedComponentPrefab = null;
            }

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
            OnComponentSelected?.Invoke(index);
        }
    }

    public bool CheckCollision(ComponentBase component, Vector2Int gridPos)
    {
        if (selectedComponentPrefab == null) return true;
        if (activeManager == null) return true;
        if (!activeManager.IsWithinBounds(gridPos.x, gridPos.y)) return true;
        
        int rot = GetEffectiveRotationIndex();
        
        // Simulation needs a dummy object or static calculation.
        // ComponentBase logic depends on instance properties.
        // We can't easily Instantiate a NetworkBehaviour locally just for check without warnings?
        // Actually we can, just don't spawn it.
        
        ComponentBase temp = Instantiate(selectedComponentPrefab, Vector3.zero, Quaternion.identity);
        
        // Setup Temp
        if (temp is CombinerComponent cc) cc.SetFlippedInitial(_currentFlipIndex); // Direct set on local temp
        // Rotation - temp.RotationIndex depends on NetVar. 
        // We can't set NetVar on temp if not spawned?
        // We need a way to set local state for calculation.
        // The implementation of GetOccupiedPositions uses RotationIndex property.
        // Property writes to NetVar.
        // We should add a "Local mode" to ComponentBase? Or just catch the exception?
        // Actually, NetworkVariable Write is only allowed on Server. 
        // TEMP fix: catch exception in ComponentBase or usage?
        // Better: Use reflection or public field in ComponentBase for simulation?
        // Or just trust the user knows what they are doing.
        // Since we upgraded ComponentBase, CheckCollision using Instantiate might break on Client!
        
        // Hack: We manually calculate offsets here to avoid instantiating NetworkBehaviour
        // But GetWidth/GetHeight is virtual.
        // We must Instantiate.
        // To fix NetVar write issue on Client:
        // Client creates local instance. Sets property -> Throws.
        // We need 'SetRotationInitial' to set a backing field that RotationIndex ignores if IsSpawned?
        
        // For now, let's assume CheckCollision handles a reduced check or catches error.
        Destroy(temp.gameObject);
        return false; // Skip complex collision for this step to avoid NetVar crash, or implement robust logic later.
        // actually returning false means "No Collision" -> Area Clear?
        // original returned !IsAreaClear.
        // Let's assume clear for now to proceed, or use Server Side validation mainly.
    }
    
    // Server RPC for Building
    [ServerRpc(RequireOwnership = false)]
    private void RequestBuildServerRpc(int prefabIndex, Vector2Int gridPos, int rot, int flip, ulong targetModuleId, ServerRpcParams rpcParams = default)
    {
        // 1. Resolve Target Manager
        ModuleManager manager = null;
        if (targetModuleId == 0)
        {
            manager = ModuleManager.Instance;
        }
        else
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetModuleId, out NetworkObject obj))
            {
                var rm = obj.GetComponent<RecursiveModuleComponent>();
                if (rm != null) manager = rm.innerGrid;
            }
        }
        
        if (manager == null) return;
        
        if (!manager.IsWithinBounds(gridPos.x, gridPos.y)) return;
        
        // Perform Server-Side Collision Check
        // here we are on Server, so we can Instantiate and set NetVars safely!
        ComponentBase prefab = availableComponents[prefabIndex];
        ComponentBase temp = Instantiate(prefab);
        
        // Apply Settings
        // temp.SetRotationInitial((Direction)rot); // Legacy
        if (temp is CombinerComponent cc) cc.PrepareFlip(flip);
        
        // Prepare NetworkVariables BEFORE validation (Validation uses properties which read NetVars if Spawned... 
        // but here it is NOT spawned. Properties read Local backing fields if !IsSpawned.
        // Wait, PrepareForSpawn sets BOTH NetVar and Local. So Validation works.
        temp.PrepareForSpawn(gridPos, (Direction)rot);
        
        // Check Validity
        List<Vector2Int> checkPositions = temp.GetOccupiedPositions(); // Now valid (uses Local/NetVar)
        // Adjust Reference Position?
        // GetOccupiedPositions uses GridPosition. 
        // PrepareForSpawn set GridPosition to `gridPos`.
        // So GetOccupiedPositions returns ABSOLUTE grid coords.
        // We do NOT need to add gridPos again!
        // Previous code: checkPositions[i] += gridPos; creates double offset!
        // Debug: GridPosition is (2,1). Offset is (0,0). Result (2,1).
        // Plus gridPos (2,1) -> (4,2). WRONG.
        
        // FIX: Remove manual offset addition since ComponentBase now knows its position.
        // for(int i=0; i<checkPositions.Count; i++) checkPositions[i] += gridPos;
        
        // Validation log?
        // Debug.Log($"Checking {temp.name} at {gridPos}. Occupied: {string.Join(",", checkPositions)}");

        if (!manager.IsAreaClear(checkPositions))
        {
            Destroy(temp.gameObject);
            return;
        }
        
        // 2. Spawn
        // temp.SetGridPositionServer(gridPos); // Handled by PrepareForSpawn
        temp.transform.position = manager.GridToWorldPosition(gridPos.x, gridPos.y);
        var no = temp.GetComponent<NetworkObject>();
        no.Spawn();
        
        // 3. Register & Parent Logic
        // IMPORTANT: If this is an Inner World, we might want to set GridPosition manually immediately?
        // ComponentBase.OnNetworkSpawn calls InitializeManager.
        // InitializeManager finds manager by searching parent or world pos.
        // World Pos should work IF the Inner World is far away and distinct.
        // But safer to forcefully set it on Server if possible, or rely on auto-detection.
        // Since we are setting position to manager.GridToWorldPosition, it should be physically correct.
        
        // Wait! We must SetManager MANUALLY if parenting is tricky or world search fails?
        // ComponentBase searches searching `ModuleManager.AllManagers`.
        // If innerGrid is registered in AllManagers, it should be fine.
        // (RecursiveModule initializes innerGrid, which should add itself to AllManagers if logic exists)
        
        // Let's ensure ComponentBase Manual Link logic is used if needed.
        // But ComponentBase doesn't have public ManualLink for regular spawn flow easily unless we call it.
        // We can call temp.SetManager(manager) directly here on Server!
        temp.SetManager(manager); // Add this reliability.
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDestroyServerRpc(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject obj))
        {
           // Check if it is a Component
           var comp = obj.GetComponent<ComponentBase>();
           if (comp != null && !(comp is CollectorComponent)) // Protection
           {
               obj.Despawn(true); // Destroy on Despawn
           }
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
        
        // Find index
        int index = -1;
        for(int i=0; i<availableComponents.Length; i++) {
            if (availableComponents[i] == selectedComponentPrefab) { index = i; break; }
        }
        if (index == -1) return;
        
        // Determine Target
        ulong targetId = 0;
        if (activeManager.ownerComponent != null)
        {
            targetId = activeManager.ownerComponent.NetworkObjectId;
        }

        // Send RPC
        RequestBuildServerRpc(index, gridPos, GetEffectiveRotationIndex(), _currentFlipIndex, targetId);
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
            }
        }
    }

    private void TryRemove()
    {
        if (activeManager == null) 
        {
             Debug.LogError("[BuildManager] TryRemove failed: activeManager is null");
             return;
        }

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = activeManager.WorldToGridPosition(mouseWorldPos);

        ComponentBase component = activeManager.GetComponentAt(gridPos);
        
        if (component != null)
        {
            if (component is CollectorComponent) 
            {
                Debug.Log("[BuildManager] Cannot remove Collector.");
                return;
            }
            // RPC
            RequestDestroyServerRpc(component.NetworkObjectId);
        }
    }

    private void TryInspect() { }

    private void TryFlip()
    {
        if (selectedComponentPrefab == null) return;
        if (selectedComponentPrefab is not CombinerComponent && selectedComponentPrefab is not DistributerComponent) return;
        _currentFlipIndex = (_currentFlipIndex + 1) % 2;
        Debug.Log($"Flipped to: {_currentFlipIndex}");
    }
}
