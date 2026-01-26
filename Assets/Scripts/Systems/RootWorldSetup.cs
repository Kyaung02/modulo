using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class RootWorldSetup : MonoBehaviour
{
    [Header("Elemental Sources")]
    public WordData fireWord;
    public WordData waterWord;
    public WordData earthWord;
    public WordData windWord;

    // Use NetworkManager to wait for Server start
    public void Start()
    {
        // 1. Common Setup (Word Registration) - Run on EVERYONE
        // We can run this immediately or wait for manager.
        // ModuleManager is a singleton, likely in scene.
        RegisterAllWords();
        
        // 2. Server Setup (Ports)
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        if (NetworkManager.Singleton.IsServer) OnServerStarted();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
    }
    
    // Server Only
    private void OnServerStarted()
    {
        ModuleManager rootManager = ModuleManager.Instance;
        if (rootManager == null || rootManager.ownerComponent != null) return;
        
        // 저장된 게임이 있으면 로드
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.ApplyLoadedGame();
        }
        
        // Root Port가 없으면 생성 (새 게임이거나 로드 오류 시)
        // 모든 PortComponent를 찾아서 parentModule이 null인 것이 있는지 확인
        PortComponent[] allPorts = FindObjectsOfType<PortComponent>();
        bool hasRootPort = false;
        foreach (var port in allPorts)
        {
            if (port.parentModule == null)
            {
                hasRootPort = true;
                break;
            }
        }
        
        if (!hasRootPort)
        {
            Debug.Log("[RootWorldSetup] No root ports found, creating default ports...");
            SetupRootPorts(rootManager);
        }
    }
    
    private void RegisterAllWords()
    {
        ModuleManager rootManager = ModuleManager.Instance;
        if (rootManager == null) return;

        // 1. Auto-Find Base Words
        if (fireWord == null) fireWord = rootManager.FindWordById("Fire");
        if (waterWord == null) waterWord = rootManager.FindWordById("Water");
        if (earthWord == null) earthWord = rootManager.FindWordById("Earth");
        if (windWord == null) windWord = rootManager.FindWordById("Wind"); 
        
        // 2. Register Base Words
        if (fireWord != null) rootManager.RegisterWord(fireWord);
        if (waterWord != null) rootManager.RegisterWord(waterWord);
        if (earthWord != null) rootManager.RegisterWord(earthWord);
        if (windWord != null) rootManager.RegisterWord(windWord);
        
        // 3. Register Recipe Output Words (Combiner Results)
        if (rootManager.recipeDatabase != null)
        {
            foreach(var recipe in rootManager.recipeDatabase.recipes)
            {
                if (recipe != null && recipe.output != null)
                {
                    rootManager.RegisterWord(recipe.output);
                }
            }
        }
        
        Debug.Log("[RootWorldSetup] Registered all base and recipe words.");
    }

    private void SetupRootPorts(ModuleManager grid)
    {
        SpawnSourcePort(grid, new Vector2Int(3, 7), Direction.Up, Direction.Down, fireWord, Color.red);
        SpawnSourcePort(grid, new Vector2Int(7, 3), Direction.Right, Direction.Left, waterWord, Color.blue);
        SpawnSourcePort(grid, new Vector2Int(3, -1), Direction.Down, Direction.Up, earthWord, new Color(0.6f, 0.4f, 0.2f));
        SpawnSourcePort(grid, new Vector2Int(-1, 3), Direction.Left, Direction.Right, windWord, Color.cyan);
    }

    private void SpawnSourcePort(ModuleManager grid, Vector2Int gridPos, Direction wallDir, Direction facingDir, WordData word, Color col)
    {
        // Load Registered Prefab
        GameObject prefab = Resources.Load<GameObject>("NetworkPrefabs/PortComponent");
        if (prefab == null) { Debug.LogError("RootWorldSetup: Missing Prefab!"); return; }

        GameObject portObj = Instantiate(prefab);
        portObj.name = $"RootSource_{wallDir}_{word?.name ?? "None"}";
        
        // Setup Logic BEFORE Spawn
        PortComponent port = portObj.GetComponent<PortComponent>();
        port.parentModule = null; // Root
        port.wallDirection = wallDir;
        port.infiniteSourceWord = word;
        
        // Setup Visuals Customization (Color) - requires NetworkVariable or RPC usually?
        // Basic Port is black. We want colored for Source.
        // Quick visual hack: local modification will only spawn on Server? 
        // No, we modify the instantiated object.
        // BUT visual changes are NOT synced unless we use NetworkVariable for Color or separate Prefabs.
        // For now, let's just make it work logic-wise. Color can be ignored or handled later.
        // Or we spawn a visual quad separately? 
        // PortComponent has its own visual. We can add a "SourceVisual" on top?
        // Let's rely on Port default visual for now to ensure functionality.
        
        // Position
        portObj.transform.position = grid.GridToWorldPosition(gridPos.x, gridPos.y);

        // Rotation via NetworkVariable (fixed in ComponentBase)
        port.SetRotationInitial(facingDir);
        
        var no = portObj.GetComponent<NetworkObject>();
        no.Spawn();
        
        // Manual Link (Important)
        port.SetManager(grid);
        
        // Set Color
        port.SetBodyColor(col);
    }
}
