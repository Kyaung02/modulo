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
    public ComponentBase collectorPrefabReference; // Explicit reference for transformation
    public ComponentBase selectedComponentPrefab; // Currently selected component to build
    
    // We don't really use this parent for NetworkObjects unless we use NetworkTransform parenting
    public Transform componentParent; 

    public Camera _mainCamera;
    
    public PreviewManager previewManager;

    public int _currentRotationIndex = 0;
    public int _currentFlipIndex = 0;
    public ModuleManager activeManager;
    
    // Milestone Unlocks
    public bool canTransformCollector = false;

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
        RecursiveModuleComponent.RefreshAllModules();
    }

    public void ExitCurrentModule()
    {
        if (CameraController.Instance != null && CameraController.Instance.IsTransitioning) return;
        
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
            
            if (GoalUI.Instance != null) GoalUI.Instance.HideTutorialText();
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

            if(Keyboard.current.qKey.wasPressedThisFrame){
                ExitCurrentModule();
            }

            if(Keyboard.current.eKey.wasPressedThisFrame){
                TryInteract();
            }

            // Flip (enabled only for CombinerComponents and DistributerComponents)
            if(Keyboard.current.tKey.wasPressedThisFrame ){
                TryFlip();
            }

            if(Keyboard.current.xKey.wasPressedThisFrame){
                selectedComponentPrefab = null;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame&&GoalManager.Instance.CheckUnlock(0)==1) SelectComponent(0);//레일
            if (Keyboard.current.digit2Key.wasPressedThisFrame&&GoalManager.Instance.CheckUnlock(1)==1) SelectComponent(1);//합성기
            if (Keyboard.current.digit3Key.wasPressedThisFrame&&GoalManager.Instance.CheckUnlock(2)==1) SelectComponent(2);//밸런서
            if (Keyboard.current.digit4Key.wasPressedThisFrame&&GoalManager.Instance.CheckUnlock(3)==1) SelectComponent(3);//모듈
            if (Keyboard.current.digit5Key.wasPressedThisFrame&&GoalManager.Instance.CheckUnlock(4)==1) SelectComponent(4);//분배기
            if (Keyboard.current.digit6Key.wasPressedThisFrame&&GoalManager.Instance.CheckUnlock(5)==1) SelectComponent(5);//터널
            //if (Keyboard.current.digit7Key.wasPressedThisFrame) SelectComponent(6);
        }
    }

    public event System.Action<int> OnComponentSelected;

    private void SelectComponent(int index)
    {
        //_currentRotationIndex=0;
        
        if (availableComponents != null && index >= 0 && index < availableComponents.Length && selectedComponentPrefab != availableComponents[index])
        {
            _currentFlipIndex=0;
            selectedComponentPrefab = availableComponents[index];
            if(selectedComponentPrefab.IsHidden() == 0)OnComponentSelected?.Invoke(index);
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
        
        if (temp is CombinerComponent cc) cc.PrepareFlip(flip);
        if (temp is DistributerComponent dc) dc.PrepareFlip(flip);
        
        // Prepare NetworkVariables BEFORE validation (Validation uses properties which read NetVars if Spawned... 
        // but here it is NOT spawned. Properties read Local backing fields if !IsSpawned.
        // Wait, PrepareForSpawn sets BOTH NetVar and Local. So Validation works.
        temp.PrepareForSpawn(gridPos, (Direction)rot);
        
        // Check Validity
        List<Vector2Int> checkPositions = temp.GetOccupiedPositions(); // Now valid (uses Local/NetVar)
        
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
        no.TrySetParent(manager.transform);
        // temp.SetManager(manager); // Add this reliability.
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

        // Apply Pivot Offset for Flipped Components (Re-implemented logic)
        Vector2Int buildPos = gridPos;
        if (_currentFlipIndex == 1 && (selectedComponentPrefab is CombinerComponent || selectedComponentPrefab is DistributerComponent))
        {
            int w = selectedComponentPrefab.GetWidth();
            if (w > 1)
            {
                // Logic: Previously we rotated a dummy object. Now we calculate manually.
                // Rotate vector (1,0) by -90 * rot
                int rot = GetEffectiveRotationIndex();
                Quaternion rotQ = Quaternion.Euler(0, 0, -90 * rot);
                Vector3 worldOffset = rotQ * Vector3.right * (w - 1);
                Vector2Int gridOffset = new Vector2Int(Mathf.RoundToInt(worldOffset.x), Mathf.RoundToInt(worldOffset.y));
                buildPos += gridOffset;
            }
        }

        // Send RPC
        RequestBuildServerRpc(index, buildPos, GetEffectiveRotationIndex(), _currentFlipIndex, targetId);
        if(selectedComponentPrefab is TunnelInComponent){
            SelectComponent(6);
        }
        else if(selectedComponentPrefab is TunnelOutComponent){
            SelectComponent(5);
        }
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
            else if (component is CollectorComponent && canTransformCollector)
            {
                // Transform!
                ulong managerId = 0;
                if (activeManager.ownerComponent != null) managerId = activeManager.ownerComponent.NetworkObjectId;
                
                RequestCollectorTransformServerRpc(gridPos, managerId);
                
                // Hide tutorial text locally
                if (GoalUI.Instance != null) GoalUI.Instance.HideTutorialText();
                // Disable flag locally to prevent double trigger
                canTransformCollector = false; 
            }
            else
            {
                Debug.Log($"Clicked on {component.name}");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollectorTransformServerRpc(Vector2Int gridPos, ulong managerId, ServerRpcParams rpcParams = default)
    {
        // 1. Resolve Manager
        ModuleManager manager = ModuleManager.Instance;
        if (managerId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(managerId, out NetworkObject obj))
        {
            var rm = obj.GetComponent<RecursiveModuleComponent>();
            if (rm != null) manager = rm.innerGrid;
        }
        if (manager == null) return;

        // 2. Validate Collector
        ComponentBase comp = manager.GetComponentAt(gridPos);
        if (comp == null || !(comp is CollectorComponent)) return;

        // 3. Destroy Collector
        manager.UnregisterComponent(comp); // Force unregister immediately so new module can take the spot
        comp.GetComponent<NetworkObject>().Despawn(true);

        // 4. Spawn Module
        ComponentBase modulePrefab = null;
        foreach(var c in availableComponents)
        {
             if (c is RecursiveModuleComponent) { modulePrefab = c; break; }
        }

        if (modulePrefab != null)
        {
            ComponentBase module = Instantiate(modulePrefab);
            module.PrepareForSpawn(gridPos, Direction.Up);
            module.transform.position = manager.GridToWorldPosition(gridPos.x, gridPos.y);
            
            var no = module.GetComponent<NetworkObject>();
            no.Spawn();
            no.TrySetParent(manager.transform);
            module.SetManager(manager); // <-- Critical: Register with manager
            
            // 5. Spawn Collector INSIDE
            RecursiveModuleComponent recursiveMod = module.GetComponent<RecursiveModuleComponent>();
            
            // Wait / Ensure Init
            if (recursiveMod != null && recursiveMod.innerGrid == null)
            {
                 Debug.LogWarning($"[BuildManager] recursiveMod.innerGrid is null after spawn! Attempting manual init check.");
                 // This might be redundant if OnNetworkSpawn works, but let's see.
            }
            
            if (recursiveMod != null && recursiveMod.innerGrid != null)
            {
                 Debug.Log($"[BuildManager] Spawning Collector inside new module at {recursiveMod.innerGrid.transform.position}");
                 
                 ComponentBase collectorPrefab = collectorPrefabReference;
                 if (collectorPrefab == null)
                 {
                    foreach(var c in availableComponents) { if (c is CollectorComponent) { collectorPrefab = c; break; } }
                 }

                 if (collectorPrefab != null)
                 {
                     Vector2Int center = new Vector2Int(3, 3);
                     ComponentBase innerCollector = Instantiate(collectorPrefab);
                     innerCollector.PrepareForSpawn(center, Direction.Up);
                     innerCollector.transform.position = recursiveMod.innerGrid.GridToWorldPosition(center.x, center.y);
                     
                     // Critical: Set Manager BEFORE Spawn so OnNetworkSpawn uses the correct manager
                     innerCollector.SetManager(recursiveMod.innerGrid); 
                     
                     var innerNo = innerCollector.GetComponent<NetworkObject>();
                     innerNo.Spawn();
                     innerCollector.transform.SetParent(recursiveMod.innerGrid.transform);
                     
                     Debug.Log($"[BuildManager] Collector Spawned: NetID {innerNo.NetworkObjectId} at {innerCollector.transform.position}");
                 }
                 else
                 {
                     Debug.LogError("[BuildManager] Could not find CollectorComponent prefab!");
                 }
            }
            else
            {
                Debug.LogError($"[BuildManager] Failed to access innerGrid on new module! (RM: {recursiveMod != null})");
            }

            // 6. Force Enter for EVERYONE
            ForceEnterModuleClientRpc(no.NetworkObjectId);
        }
    }

    [ClientRpc]
    private void ForceEnterModuleClientRpc(ulong moduleNetId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(moduleNetId, out NetworkObject obj))
        {
            var rm = obj.GetComponent<RecursiveModuleComponent>();
            if (rm != null)
            {
                // Stop building/inspecting
                selectedComponentPrefab = null;
                // Force Enter
                // Hide current tutorial text (e.g. "Press E") BEFORE starting transition
                if (GoalUI.Instance != null) GoalUI.Instance.HideTutorialText();

                rm.EnterModule(() => {
                    // Show new tutorial text AFTER transition completes
                    if (GoalUI.Instance != null)
                    {
                        GoalUI.Instance.ShowTutorialText("Press Q on Module Center to Exit");
                    }
                });
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
        if( selectedComponentPrefab is TunnelInComponent || selectedComponentPrefab is TunnelOutComponent){
            if(selectedComponentPrefab is TunnelInComponent){
                SelectComponent(6);
            }
            else if(selectedComponentPrefab is TunnelOutComponent){
                SelectComponent(5);
            }
            return;
        }
        if (selectedComponentPrefab is not CombinerComponent && selectedComponentPrefab is not DistributerComponent) return;
        _currentFlipIndex = (_currentFlipIndex + 1) % 2;
        Debug.Log($"Flipped to: {_currentFlipIndex}");
    }
}
