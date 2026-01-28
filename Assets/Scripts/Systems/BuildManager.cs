using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.Netcode;

public class BuildManager : NetworkBehaviour
{
    public static BuildManager Instance { get; private set; }
    
    // Add public method for BlueprintManager to force selection
    public void ForceSelectComponent(int index)
    {
        SelectComponent(index);
    }

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
            if (selectedComponentPrefab != null) TryBuild(false);
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
            if(Keyboard.current.tKey.wasPressedThisFrame ){
                TryFlip();
            }

            if(Keyboard.current.cKey.wasPressedThisFrame)
            {
                TryCopy();
            }
            
            if(Keyboard.current.vKey.wasPressedThisFrame)
            {
                TryPaste();
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
    private void RequestBuildServerRpc(int prefabIndex, Vector2Int gridPos, int rot, int flip, ulong targetModuleId, string snapshotJson, ServerRpcParams rpcParams = default)
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
        no.Spawn();
        no.TrySetParent(manager.transform);
        // temp.SetManager(manager); // Add this reliability.

        // 3. Apply Snapshot (Recursive Restoration)
        if (!string.IsNullOrEmpty(snapshotJson))
        {
            if (temp is RecursiveModuleComponent rm)
            {
                 // Ensure inner world is ready (Server side instant init)
                 if(rm.innerGrid == null) rm.OnNetworkSpawn(); // Force Init if not yet? 
                 // Actually OnNetworkSpawn is called by Netcode automatically on client, but on Server?
                 // When Spawn() is called on Server, OnNetworkSpawn runs immediately? Yes usually.
                 // But let's be safe.
                 
                 // Apply Data
                 ApplySnapshot(rm.innerGrid, snapshotJson);
            }
        }
    }

    private void ApplySnapshot(ModuleManager targetManager, string json)
    {
        if (targetManager == null || string.IsNullOrEmpty(json)) return;
        
        ModuleSnapshot snapshot = JsonUtility.FromJson<ModuleSnapshot>(json);
        if (snapshot == null || snapshot.components == null) return;
        
        foreach (var data in snapshot.components)
        {
             if (data.prefabIndex < 0 || data.prefabIndex >= availableComponents.Length) continue;
             
             // Recursively request build (Directly instantiate since we are on Server)
             // We can reuse RequestBuildServerRpc logic or extract it?
             // Since we are ALREADY on Server, we can just call the logic directly.
             // But RequestBuildServerRpc is an RPC, we shouldn't call it directly from code usually? 
             // Actually InternalBuild method is better.
             
             // For now, let's duplicate the instantiation logic for recursion to avoid RPC overhead/complexity in loops.
             
             ComponentBase prefab = availableComponents[data.prefabIndex];
             ComponentBase temp = Instantiate(prefab);
             
             if (temp is CombinerComponent cc) cc.PrepareFlip(data.flipIndex);
             if (temp is DistributerComponent dc) dc.PrepareFlip(data.flipIndex);
             
             temp.PrepareForSpawn(data.gridPos, (Direction)data.rotationIndex);
             
             List<Vector2Int> checkPositions = temp.GetOccupiedPositions();
             // Assume collision check passed or force it? 
             // Ideally we should check, but if it's a valid snapshot it should fit.
             
             if (!targetManager.IsAreaClear(checkPositions))
             {
                 Destroy(temp.gameObject);
                 continue; 
             }
             
             temp.transform.position = targetManager.GridToWorldPosition(data.gridPos.x, data.gridPos.y);
             var no = temp.GetComponent<NetworkObject>();
             no.Spawn();
             no.TrySetParent(targetManager.transform);
             
             // Recurse
             if (temp is RecursiveModuleComponent childRm && !string.IsNullOrEmpty(data.innerWorldJson))
             {
                 // Ensure inner grid exists
                 if (childRm.innerGrid == null) childRm.OnNetworkSpawn(); 
                 ApplySnapshot(childRm.innerGrid, data.innerWorldJson);
             }
        }
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

    private void TryBuild(bool useClipboard = false)
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

        if (useClipboard) // Called from TryPaste
        {
            var bp = BlueprintManager.Instance.GetSelectedBlueprint();
            if (bp != null && bp.prefabIndex == index)
            {
                snapshotData = bp.snapshotJson;
            }
        }
        else // Check if we manually selected a blueprint via UI (BuildManager.Instance.ForceSelectComponent was called, but rotation reset logic might apply)
             // Wait, if user clicked Blueprint in UI, we want standard Build to use that blueprint data?
             // Actually requirement says: "V key pastes selected". Left click on list just selects it.
             // If I Left Click on main screen after selecting from list... does it build clean or blueprint?
             // Requirement: "Click list -> Left click to select... V key to paste selected".
             // It implies "V" does the specific paste.
             // But if I select a blueprint, and then just Click to build... usually expectation is it builds the blueprint?
             // "V key to paste selected" -> Explicit paste.
             // Let's stick to: "Default Build (Click)" builds CLEAN prefab. "V Build" builds BLUEPRINT.
        {
             // Do nothing (clean build)
        }

        // Send RPC
        RequestBuildServerRpc(index, buildPos, GetEffectiveRotationIndex(), _currentFlipIndex, targetId, snapshotData);
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
    private void RequestCollectorTransformServerRpc(Vector2Int gridPos, ulong managerId)
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
        comp.GetComponent<NetworkObject>().Despawn(true);

        // 4. Spawn Module
        // Find RecursiveModule in availableComponents
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
            
            // 5. Spawn Collector INSIDE the new module
            // RecursiveModule OnNetworkSpawn (Server) inits innerGrid immediately.
            RecursiveModuleComponent recursiveMod = module.GetComponent<RecursiveModuleComponent>();
            if (recursiveMod != null && recursiveMod.innerGrid != null)
            {
                 // Find Collector Prefab
                 ComponentBase collectorPrefab = null;
                 foreach(var c in availableComponents)
                 {
                     if (c is CollectorComponent) { collectorPrefab = c; break; }
                 }

                 if (collectorPrefab != null)
                 {
                     Vector2Int center = new Vector2Int(3, 3);
                     ComponentBase innerCollector = Instantiate(collectorPrefab);
                     innerCollector.PrepareForSpawn(center, Direction.Up);
                     innerCollector.transform.position = recursiveMod.innerGrid.GridToWorldPosition(center.x, center.y);
                     
                     var innerNo = innerCollector.GetComponent<NetworkObject>();
                     innerNo.Spawn();
                     // Note: innerGrid is not a NetworkObject, so we can't TrySetParent with netcode easily?
                     // RecursiveModuleComponent.InitializeInnerWorld sets innerGrid on a generic Transform.
                     // The inner components MUST be parented to innerGrid transform for organization, 
                     // but NetworkObject parenting requires Parent to be NetworkObject if we use it.
                     // Our system usually puts them at root or under a NetworkObject.
                     // RecursiveModule.innerWorldRoot is NOT a NetworkObject.
                     // So we just set transform parent.
                     innerCollector.transform.SetParent(recursiveMod.innerGrid.transform);
                     innerCollector.SetManager(recursiveMod.innerGrid);
                 }
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

    public void TryCopy()
    {
        if (activeManager == null) return;
        
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = activeManager.WorldToGridPosition(mouseWorldPos);

        ComponentBase target = activeManager.GetComponentAt(gridPos);
        if (target != null)
        {
            // Identify Prefab Index
            int index = -1;
            // Use name matching or type matching? Prefab reference check won't work on instance.
            // We need a way to ID the prefab from the instance. 
            // Current simplistic approach: Check Type?
            // Multiple prefabs might share Type.
            // Improve: ComponentBase should hold 'PrefabID' or we infer from list.
            
            // Heuristic: Check if name starts with prefab name?
            string targetName = target.name.Replace("(Clone)", "").Trim();
            for(int i=0; i<availableComponents.Length; i++)
            {
                if (availableComponents[i].name == targetName || availableComponents[i].GetType() == target.GetType()) // Fallback to type
                {
                    // Strict naming required for multiple same-type components.
                    // For now assuming 1-to-1 Type to Prefab for complex modules.
                    if (target.GetType() == availableComponents[i].GetType())
                    {
                        index = i;
                        break;
                    }
                }
            }
            
            if (index != -1)
            {
                // Delegate to BlueprintManager
                string json = RecursiveSerialize(target);
                BlueprintManager.Instance.CaptureBlueprint(target, json, index);
                
                // Copy orientation (Visual feedback for next build)
                if (!(target is RecursiveModuleComponent))
                {
                    _currentRotationIndex = (int)target.RotationIndex;
                    if(target is CombinerComponent cc) _currentFlipIndex = cc.IsFlipped;
                    if(target is DistributerComponent dc) _currentFlipIndex = dc.IsFlipped;
                }
            }
        }
    }
    
    private void TryPaste()
    {
        if (BlueprintManager.Instance == null) return;
        
        var bp = BlueprintManager.Instance.GetSelectedBlueprint();
        if (bp == null) 
        {
            Debug.Log("[BuildManager] No Blueprint selected.");
            return;
        }
        
        // Ensure the correct component is selected for the clipboard item
        if (selectedComponentPrefab != availableComponents[bp.prefabIndex])
        {
            SelectComponent(bp.prefabIndex);
        }
        
        // TryBuild with clipboard flag
        TryBuild(true);
        // Feedback
        Debug.Log($"[BuildManager] Pasting blueprint: {bp.name}");
    }
    
    private string RecursiveSerialize(ComponentBase target)
    {
        // Recursively serialize only if it is a RecursiveModule
        if (target is RecursiveModuleComponent rm)
        {
            if (rm.innerGrid == null) return "";
            
            ModuleSnapshot snapshot = new ModuleSnapshot();
            
            // Iterate all 7x7 scan? Or internal list?
            // ModuleManager has _gridComponents array.
            // But we don't have list of all components. Scan 7x7.
            
            HashSet<ComponentBase> visited = new HashSet<ComponentBase>();
            
            for(int x=0; x<rm.innerGrid.width; x++)
            {
                for(int y=0; y<rm.innerGrid.height; y++)
                {
                    ComponentBase child = rm.innerGrid.GetComponentAt(new Vector2Int(x, y));
                    if (child != null && !visited.Contains(child))
                    {
                        visited.Add(child);
                        
                        // Find child prefab index
                        int childIndex = -1;
                        for(int i=0; i<availableComponents.Length; i++)
                        {
                            if (availableComponents[i].GetType() == child.GetType())
                            {
                                childIndex = i;
                                break;
                            }
                        }
                        
                        if (childIndex != -1)
                        {
                            ComponentSnapshot cs = new ComponentSnapshot();
                            cs.prefabIndex = childIndex;
                            cs.gridPos = child.GridPosition;
                            cs.rotationIndex = (int)child.RotationIndex;
                            
                            if (child is CombinerComponent cc) cs.flipIndex = cc.IsFlipped;
                            else if (child is DistributerComponent dc) cs.flipIndex = dc.IsFlipped;
                            
                            // Recurse
                            cs.innerWorldJson = RecursiveSerialize(child);
                            
                            snapshot.components.Add(cs);
                        }
                    }
                }
            }
            return JsonUtility.ToJson(snapshot);
        }
        return "";
    }
}
