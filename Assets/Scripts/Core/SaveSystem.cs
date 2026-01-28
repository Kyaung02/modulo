using UnityEngine;
using Unity.Netcode;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// 게임 저장/로드 시스템
/// 저장: 클라이언트도 ServerRpc를 통해 요청 가능
/// 로드: 서버 전용, 타이틀 화면에서만
/// </summary>
public class SaveSystem : NetworkBehaviour
{
    public static SaveSystem Instance { get; private set; }
    
    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
    private static string DefaultSaveFileName => "savegame.json";
    private static string SaveFilePath => Path.Combine(SaveDirectory, DefaultSaveFileName);
    
    // 로드할 게임 데이터 (타이틀에서 설정)
    public static GameSaveData PendingLoadData { get; set; }
    
    // 저장 완료 이벤트
    public static event System.Action<bool, string> OnSaveComplete;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    
    #region Save
    
    /// <summary>
    /// 게임 저장 (클라이언트도 호출 가능)
    /// </summary>
    public void RequestSave()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            Debug.LogWarning("[SaveSystem] Not connected to network!");
            return;
        }
        
        if (IsServer)
        {
            // 서버는 직접 저장
            SaveGame();
        }
        else
        {
            // 클라이언트는 서버에 요청
            RequestSaveServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestSaveServerRpc()
    {
        SaveGame();
    }
    
    /// <summary>
    /// 실제 저장 로직 (서버 전용)
    /// </summary>
    private void SaveGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[SaveSystem] SaveGame can only be called on Server!");
            return;
        }
        
        try
        {
            GameSaveData saveData = new GameSaveData();
            saveData.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            if (GoalManager.Instance != null)
            {
                List<int> completedList = new List<int>();
                if (GoalManager.Instance.levels != null)
                {
                    for (int i = 0; i < GoalManager.Instance.levels.Length; i++)
                    {
                        if (GoalManager.Instance.IsGoalCompleted(i))
                        {
                            completedList.Add(i);
                        }
                    }
                }
                
                List<int> progressList = new List<int>();
                if (GoalManager.Instance.levels != null)
                {
                     for(int i=0; i< GoalManager.Instance.levels.Length; i++)
                     {
                         progressList.Add(GoalManager.Instance.GetProgress(i));
                     }
                }

                saveData.goalData = new GoalSaveData
                {
                    progressCounts = progressList,
                    completedGoals = completedList
                };
            }
            
            // ModuleManager 저장
            if (ModuleManager.Instance != null)
            {
                saveData.moduleData = new ModuleSaveData
                {
                    gridWidth = ModuleManager.Instance.width,
                    gridHeight = ModuleManager.Instance.height,
                    cellSize = ModuleManager.Instance.cellSize,
                    originPosition = ModuleManager.Instance.originPosition
                };
            }
            
            // 모든 컴포넌트 저장 (루트 + 모든 RecursiveModule 내부)
            SaveAllComponents(saveData);
            
            // 블루프린트 저장
            SaveBlueprints(saveData);
            
            // JSON으로 직렬화
            string json = JsonUtility.ToJson(saveData, true);
            
            // 디렉토리 생성
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
            
            // 파일 저장
            File.WriteAllText(SaveFilePath, json);
            
            Debug.Log($"[SaveSystem] Game saved successfully! Path: {SaveFilePath}");
            NotifySaveCompleteClientRpc(true, "저장 완료!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
            NotifySaveCompleteClientRpc(false, "저장 실패!");
        }
    }
    
    /// <summary>
    /// 모든 ModuleManager(루트 + RecursiveModule 내부)의 컴포넌트 저장
    /// </summary>
    private void SaveAllComponents(GameSaveData saveData)
    {
        // 모든 ModuleManager 가져오기 (루트 + innerGrid들)
        foreach (var manager in ModuleManager.AllManagers)
        {
            if (manager == null) continue;
            
            // 이 매니저의 모든 컴포넌트 가져오기
            ComponentBase[] components = FindObjectsByType<ComponentBase>(FindObjectsSortMode.None);
            
            foreach (var component in components)
            {
                // 이 컴포넌트가 현재 매니저에 속하는지 확인
                if (component.AssignedManager != manager) continue;
                
                // 모든 포트는 제외 (Root Port와 RecursiveModule 내부 Port 모두 자동 생성됨)
                if (component is PortComponent)
                {
                    Debug.Log($"[SaveSystem] Skipping Auto-generated Port at {component.GridPosition}");
                    continue;
                }
                
                ComponentSaveData compData = CreateComponentSaveData(component);
                if (compData != null)
                {
                    saveData.components.Add(compData);
                }
            }
        }
        
        Debug.Log($"[SaveSystem] Saved {saveData.components.Count} components total.");
    }
    
    /// <summary>
    /// 컴포넌트를 ComponentSaveData로 변환
    /// </summary>
    private ComponentSaveData CreateComponentSaveData(ComponentBase component)
    {
        ComponentSaveData compData = new ComponentSaveData
        {
            componentType = component.GetType().Name,
            gridPosition = component.GridPosition,
            rotation = component.RotationIndex,
            heldWordId = component.HeldWord != null ? component.HeldWord.id : null
        };
        
        // 네트워크 ID 저장 (로드 시 매핑용)
        NetworkObject netObj = component.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            compData.savedNetworkId = netObj.NetworkObjectId;
        }
        
        // 부모 모듈 ID 설정 (RecursiveModule 내부인 경우)
        if (component.AssignedManager != null && component.AssignedManager.ownerComponent != null)
        {
            var ownerNetObj = component.AssignedManager.ownerComponent.GetComponent<NetworkObject>();
            if (ownerNetObj != null && ownerNetObj.IsSpawned)
            {
                compData.parentModuleNetworkId = ownerNetObj.NetworkObjectId;
                Debug.Log($"[SaveSystem] Component {component.GetType().Name} belongs to module {compData.parentModuleNetworkId}");
            }
        }
        
        // 타입별 특수 데이터
        if (component is CombinerComponent combiner)
        {
            compData.isFlipped = combiner.IsFlipped;
            compData.prefabPath = "Prefabs/Combiner";
        }
        else if (component is PortComponent portComp)
        {
            compData.wallDirection = portComp.wallDirection;
            compData.infiniteSourceWordId = portComp.infiniteSourceWord != null ? portComp.infiniteSourceWord.id : null;
            compData.prefabPath = "NetworkPrefabs/PortComponent";
        }
        else if (component is BalancerComponent)
        {
            compData.prefabPath = "Prefabs/Balancer";
        }
        else if (component is RecursiveModuleComponent)
        {
            compData.isRecursiveModule = true;
            compData.prefabPath = "Prefabs/RecursiveModule";
        }
        else if (component is DistributerComponent)
        {
            compData.prefabPath = "Prefabs/Distributer";
        }
        else if (component is CollectorComponent)
        {
            compData.prefabPath = "Prefabs/Collector";
        }
        else if (component is MoverComponent)
        {
            compData.prefabPath = "Prefabs/Mover";
        }
        else
        {
            Debug.LogWarning($"[SaveSystem] Unknown component type, skipping: {component.GetType().Name}");
            return null;
        }
        
        return compData;
    }
    
    [ClientRpc]
    private void NotifySaveCompleteClientRpc(bool success, string message)
    {
        Debug.Log($"[SaveSystem] {message}");
        OnSaveComplete?.Invoke(success, message);
    }
    
    #endregion
    
    #region Load
    
    /// <summary>
    /// 타이틀 화면에서 저장 파일을 읽어서 PendingLoadData에 저장
    /// </summary>
    public static bool LoadSaveFile()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.LogWarning($"[SaveSystem] Save file not found: {SaveFilePath}");
                return false;
            }
            
            string json = File.ReadAllText(SaveFilePath);
            PendingLoadData = JsonUtility.FromJson<GameSaveData>(json);
            
            Debug.Log($"[SaveSystem] Save file loaded. Timestamp: {PendingLoadData.saveTimestamp}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Load file failed: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 게임 씬이 로드된 후 PendingLoadData를 적용 (서버 전용)
    /// </summary>
    public void ApplyLoadedGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[SaveSystem] ApplyLoadedGame can only be called on Server!");
            return;
        }
        
        if (PendingLoadData == null)
        {
            Debug.Log("[SaveSystem] No pending load data. Starting new game.");
            return;
        }
        
        // 코루틴으로 약간의 딜레이를 준 후 로드
        StartCoroutine(ApplyLoadedGameCoroutine());
    }
    
    private System.Collections.IEnumerator ApplyLoadedGameCoroutine()
    {
        // 1프레임 대기 (모든 시스템이 초기화될 시간 확보)
        yield return null;
        
        Debug.Log("[SaveSystem] Applying loaded game data...");
        
        // 기존 컴포넌트 모두 제거 (Root Port 제외)
        ComponentBase[] existingComponents = FindObjectsByType<ComponentBase>(FindObjectsSortMode.None);
        foreach (var comp in existingComponents)
        {
            // Root 포트는 유지
            if (comp is PortComponent port && port.parentModule == null)
                continue;
                
            NetworkObject netObj = comp.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn();
            }
        }
        
        // 1프레임 대기 (Despawn이 완료될 시간 확보)
        yield return null;
        
        // GoalManager 복원
        if (GoalManager.Instance != null && PendingLoadData.goalData != null)
        {
            GoalManager.Instance.RestoreState(
                PendingLoadData.goalData.progressCounts,
                PendingLoadData.goalData.completedGoals
            );
        }
        
        // 세션 간 ID 매핑 (저장된 ID -> 새로 생성된 RecursiveModule)
        Dictionary<ulong, RecursiveModuleComponent> idToModuleMap = new Dictionary<ulong, RecursiveModuleComponent>();
        
        // 컴포넌트 복원 (2단계)
        // 1단계: RecursiveModule 먼저 복원 (innerGrid 생성)
        var recursiveModules = PendingLoadData.components.Where(c => c.isRecursiveModule).ToList();
        var otherComponents = PendingLoadData.components.Where(c => !c.isRecursiveModule).ToList();
        
        Debug.Log($"[SaveSystem] Restoring {recursiveModules.Count} RecursiveModules first...");
        foreach (var compData in recursiveModules)
        {
            ComponentBase restored = RestoreComponent(compData, idToModuleMap);
            if (restored is RecursiveModuleComponent rm)
            {
                idToModuleMap[compData.savedNetworkId] = rm;
                Debug.Log($"[SaveSystem] Mapped saved ID {compData.savedNetworkId} to new RecursiveModule {rm.NetworkObjectId}");
            }
        }
        
        // RecursiveModule들이 innerGrid를 초기화할 시간을 줌
        yield return null;
        
        Debug.Log($"[SaveSystem] Restoring {otherComponents.Count} other components...");
        // 2단계: 나머지 컴포넌트 복원 (루트 + innerGrid 내부)
        foreach (var compData in otherComponents)
        {
            RestoreComponent(compData, idToModuleMap);
        }
        
        // 블루프린트 복원
        RestoreBlueprints();
        
        Debug.Log($"[SaveSystem] Game loaded successfully! Restored {PendingLoadData.components.Count} components.");
        PendingLoadData = null; // 사용 완료
    }
    
    private ComponentBase RestoreComponent(ComponentSaveData data, Dictionary<ulong, RecursiveModuleComponent> idMap)
    {
        try
        {
            Debug.Log($"[SaveSystem] Restoring {data.componentType} at {data.gridPosition}, rotation: {data.rotation}");
            
            if (string.IsNullOrEmpty(data.prefabPath))
            {
                Debug.LogWarning($"[SaveSystem] No prefab path for component type: {data.componentType}");
                return null;
            }
            
            // 프리팹 로드
            GameObject prefab = Resources.Load<GameObject>(data.prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SaveSystem] Failed to load prefab: {data.prefabPath}");
                return null;
            }
            
            Debug.Log($"[SaveSystem] Prefab loaded successfully: {data.prefabPath}");
            
            // 인스턴스 생성
            GameObject instance = Instantiate(prefab);
            ComponentBase component = instance.GetComponent<ComponentBase>();
            
            if (component == null)
            {
                Debug.LogError($"[SaveSystem] Prefab has no ComponentBase: {data.prefabPath}");
                Destroy(instance);
                return null;
            }
            
            // 위치 및 회전 설정 (Spawn 전)
            component.PrepareForSpawn(data.gridPosition, data.rotation);
            
            // 적절한 ModuleManager 찾기 (parentModuleNetworkId 기반)
            ModuleManager targetManager = null;
            
            if (data.parentModuleNetworkId == 0)
            {
                // 루트 ModuleManager 사용
                targetManager = ModuleManager.Instance;
                Debug.Log($"[SaveSystem] Component will be placed in root ModuleManager");
            }
            else
            {
                // idMap에서 부모 모듈 찾기
                if (idMap.TryGetValue(data.parentModuleNetworkId, out RecursiveModuleComponent parentModule))
                {
                    if (parentModule != null && parentModule.innerGrid != null)
                    {
                        targetManager = parentModule.innerGrid;
                        Debug.Log($"[SaveSystem] Component will be placed in RecursiveModule {data.parentModuleNetworkId}'s innerGrid (Mapped)");
                    }
                    else
                    {
                        Debug.LogError($"[SaveSystem] Found parent module in map but innerGrid is null!");
                    }
                }
                
                // Fallback: 기존 방식으로 검색 (네트워크 스폰 타이밍에 따라 필요할 수 있음)
                if (targetManager == null)
                {
                    NetworkObject[] allNetObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
                    foreach (var no in allNetObjects)
                    {
                        if (no.NetworkObjectId == data.parentModuleNetworkId)
                        {
                            RecursiveModuleComponent recursiveModule = no.GetComponent<RecursiveModuleComponent>();
                            if (recursiveModule != null && recursiveModule.innerGrid != null)
                            {
                                targetManager = recursiveModule.innerGrid;
                                Debug.Log($"[SaveSystem] Component will be placed in RecursiveModule {data.parentModuleNetworkId}'s innerGrid (Fallback)");
                                break;
                            }
                        }
                    }
                }
                
                if (targetManager == null)
                {
                    Debug.LogError($"[SaveSystem] Could not find parent ModuleManager for ID {data.parentModuleNetworkId}");
                    Destroy(instance);
                    return null;
                }
            }
            
            // 월드 위치 설정
            if (targetManager != null)
            {
                instance.transform.position = targetManager.GridToWorldPosition(data.gridPosition.x, data.gridPosition.y);
                Debug.Log($"[SaveSystem] Position set to: {instance.transform.position}");
                
                // 매니저 등록 (이게 있어야 그리드에 배치됨)
                component.SetManager(targetManager);
            }
            
            // 타입별 특수 설정
            if (component is CombinerComponent combiner)
            {
                combiner.SetFlippedInitial(data.isFlipped);
                Debug.Log($"[SaveSystem] Combiner flip set to: {data.isFlipped}");
            }
            else if (component is PortComponent port)
            {
                port.wallDirection = data.wallDirection;
                if (!string.IsNullOrEmpty(data.infiniteSourceWordId) && targetManager != null)
                {
                    port.infiniteSourceWord = targetManager.FindWordById(data.infiniteSourceWordId);
                    Debug.Log($"[SaveSystem] Port infiniteSource set to: {data.infiniteSourceWordId}");
                }
            }
            
            // NetworkObject Spawn
            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                Debug.Log($"[SaveSystem] NetworkObject spawned: {data.componentType}");
                
                // Spawn 후 보유 단어 복원
                if (!string.IsNullOrEmpty(data.heldWordId) && targetManager != null)
                {
                    WordData word = targetManager.FindWordById(data.heldWordId);
                    if (word != null)
                    {
                        component.AcceptWord(word, Vector2Int.zero, data.gridPosition);
                        Debug.Log($"[SaveSystem] Held word restored: {data.heldWordId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[SaveSystem] Failed to find word: {data.heldWordId}");
                    }
                }
            }
            else
            {
                Debug.LogError($"[SaveSystem] No NetworkObject on prefab: {data.prefabPath}");
            }
            
            return component;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to restore component {data.componentType}: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }
    
    
    /// <summary>
    /// 저장 파일 존재 여부 확인
    /// </summary>
    public static bool SaveFileExists()
    {
        return File.Exists(SaveFilePath);
    }
    
    /// <summary>
    /// 저장된 블루프린트 복원
    /// </summary>
    private void RestoreBlueprints()
    {
        Debug.Log("[SaveSystem] RestoreBlueprints() called.");
        if (PendingLoadData.blueprints == null)
        {
             Debug.LogWarning("[SaveSystem] PendingLoadData.blueprints is NULL");
             return;
        }
        Debug.Log($"[SaveSystem] Blueprint count in save data: {PendingLoadData.blueprints.Count}");
        
        if (PendingLoadData.blueprints.Count == 0) return;
        
        // BlueprintManager가 없으면 생성 (UI 시스템이지만 데이터 복원을 위해 필요)
        if (BlueprintManager.Instance == null)
        {
            GameObject managerObj = new GameObject("BlueprintManager");
            managerObj.AddComponent<BlueprintManager>();
            Debug.Log("[SaveSystem] Created BlueprintManager for restoration");
        }
        
        BlueprintManager.Instance.ClearBlueprints();
        
        foreach (var bpData in PendingLoadData.blueprints)
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(bpData.previewImageBase64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(bpData.previewImageBase64);
                    Texture2D tex = new Texture2D(2, 2); // 크기는 LoadImage가 알아서 조정
                    if (tex.LoadImage(bytes))
                    {
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Failed to load blueprint image: {e.Message}");
                }
            }
            
            BlueprintManager.Instance.AddBlueprint(bpData.name, bpData.snapshotJson, bpData.prefabIndex, sprite);
        }
        
        Debug.Log($"[SaveSystem] Restored {PendingLoadData.blueprints.Count} blueprints.");
        
        // Force UI Refresh (Ensure UI sees the data immediately)
        // Since event might have been missed if UI started late
        BlueprintUI ui = FindFirstObjectByType<BlueprintUI>();
        
        // FAIL-SAFE: If UI is missing, force creation via Setup
        if (ui == null)
        {
             Debug.LogWarning("[SaveSystem] BlueprintUI missing. Triggering Setup...");
             BlueprintUISetup setup = FindFirstObjectByType<BlueprintUISetup>();
             if (setup == null)
             {
                 GameObject setupObj = new GameObject("BlueprintUISetup_Auto");
                 setup = setupObj.AddComponent<BlueprintUISetup>();
             }
             
             // Run setup immediately
             setup.SetupBlueprintSystem();
             
             // Try find again
             ui = FindFirstObjectByType<BlueprintUI>();
        }
        
        if (ui != null)
        {
            ui.RefreshUI();
            Debug.Log("[SaveSystem] Forced BlueprintUI refresh.");
        }
        else
        {
            Debug.LogError("[SaveSystem] BlueprintUI could NOT be created during restore.");
        }
    }

    /// <summary>
    /// 블루프린트 저장
    /// </summary>
    private void SaveBlueprints(GameSaveData saveData)
    {
        if (BlueprintManager.Instance == null)
        {
             Debug.LogWarning("[SaveSystem] BlueprintManager.Instance is NULL during save!");
             return;
        }
        
        Debug.Log($"[SaveSystem] Finding Blueprints to save... Current Count: {BlueprintManager.Instance.blueprints.Count}");
        
        foreach (var bp in BlueprintManager.Instance.blueprints)
        {
            string base64 = null;
            if (bp.previewSprite != null && bp.previewSprite.texture != null)
            {
                try
                {
                    // Texture가 Readable이어야 함
                    if (bp.previewSprite.texture.isReadable)
                    {
                        byte[] bytes = bp.previewSprite.texture.EncodeToPNG();
                        base64 = Convert.ToBase64String(bytes);
                    }
                    else
                    {
                         // Readable하지 않으면 안타깝지만 이미지는 저장 불가 (또는 렌더텍스처로 복사해야 함)
                         // BlueprintManager 생성 방식상 Readable이어야 맞음.
                         Debug.LogWarning($"[SaveSystem] Blueprint texture not readable: {bp.name}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Failed to encode blueprint image: {e.Message}");
                }
            }
            
            BlueprintSaveData bpData = new BlueprintSaveData
            {
                name = bp.name,
                snapshotJson = bp.snapshotJson,
                prefabIndex = bp.prefabIndex,
                previewImageBase64 = base64
            };
            
            saveData.blueprints.Add(bpData);
        }
        
        Debug.Log($"[SaveSystem] Saved {saveData.blueprints.Count} blueprints.");
    }
    
    #endregion
}
