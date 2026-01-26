using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;

public class ComponentBase : NetworkBehaviour
{
    // Networked Position Logic
    private NetworkVariable<Vector2Int> _netGridPosition = new NetworkVariable<Vector2Int>();
    private Vector2Int _localGridPosition; // For ghost objects

    public Vector2Int GridPosition 
    { 
        get 
        {
            if (IsSpawned)
            {
                // 클라이언트 동기화 지연 보호: 네트워크값이 (0,0)이고 로컬값이 (0,0)이 아니면 로컬값 사용
                if (!IsServer && _netGridPosition.Value == Vector2Int.zero && _localGridPosition != Vector2Int.zero)
                {
                    // Debug.Log($"[ComponentBase] Using local grid position fallback: {_localGridPosition}");
                    return _localGridPosition;
                }
                return _netGridPosition.Value;
            }
            return _localGridPosition;
        }
        private set 
        {
            if (IsSpawned && IsServer) _netGridPosition.Value = value;
            else if (!IsSpawned) _localGridPosition = value;
        }
    }
    
    // Server-side setter
    public void SetGridPositionServer(Vector2Int pos)
    {
        if (IsServer) _netGridPosition.Value = pos;
        else _localGridPosition = pos; // Fallback
    }
    private NetworkVariable<Direction> _netRotationIndex = new NetworkVariable<Direction>(Direction.Up);

    // Local fallback for unspawned/ghost objects
    private Direction _localRotation = Direction.Up;

    public Direction RotationIndex 
    { 
        get 
        {
             return IsSpawned ? _netRotationIndex.Value : _localRotation;
        }
        set 
        { 
             if (IsSpawned && IsServer) 
             {
                 _netRotationIndex.Value = value;
             }
             else if (!IsSpawned) 
             {
                 _localRotation = value;
                 UpdateRotationVisual(); // Immediate visual update for local/dummy objects
             }
             else 
             {
                 Debug.LogWarning($"[ComponentBase] Client tried to set RotationIndex on spawned object {name}!");
             }
        }
    }
    
    // Helper for instantiation before spawn
    public void SetRotationInitial(Direction dir)
    {
        // For unspawned objects, set local
        _localRotation = dir;
    }
    
    // IMPORTANT: Call this on the new instance BEFORE Spawn()
    public void PrepareForSpawn(Vector2Int pos, Direction rot)
    {
        // Set local backups only. Server OnNetworkSpawn will sync these to NetVars.
        // This avoids "Written to but doesn't know Behaviour" warnings.
        _localGridPosition = pos;
        _localRotation = rot;
    }

    private NetworkVariable<FixedString64Bytes> _netHeldWordId = new NetworkVariable<FixedString64Bytes>("");
    private NetworkVariable<Vector2Int> _netLastInputDir = new NetworkVariable<Vector2Int>(Vector2Int.zero); // For Animation


    [Header("Debug")]
    [SerializeField] private WordData _debugHeldWord;

    public WordData HeldWord 
    { 
        get => _debugHeldWord; 
        protected set => _debugHeldWord = value; 
    }

    protected Vector2Int LastInputDir => IsSpawned ? _netLastInputDir.Value : Vector2Int.zero;

    // Public method to clear held word (for external components)
    public void ClearHeldWord()
    {
        if (!IsServer) return;
        HeldWord = null;
        _netHeldWordId.Value = "";
        _netLastInputDir.Value = Vector2Int.zero;
        UpdateVisuals();
    }

    protected void SetHeldWordServer(WordData word)
    {
        if (!IsServer) return;
        HeldWord = word;
        _netHeldWordId.Value = word != null ? new FixedString64Bytes(word.id) : new FixedString64Bytes("");
        _netLastInputDir.Value = Vector2Int.zero; // New spawn = No animation (or pop in)
        UpdateVisuals();
    }

    public virtual int GetWidth() => 1;
    public virtual int GetHeight() => 1;

    public List<Vector2Int> GetOccupiedPositions()
    {
         // Use current state
         int isFlipped = 0;
         if(this is CombinerComponent cc) isFlipped = cc.IsFlipped;
         return GetOccupiedPositions(GridPosition, RotationIndex, isFlipped);
    }
    
    public List<Vector2Int> GetOccupiedPositions(Vector2Int gridPos, Direction rot, int isFlipped)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        int w = GetWidth();
        int h = GetHeight();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int newx=x,newy=y;
                if(isFlipped == 1){
                    newx=-newx;
                }
                
                Vector2Int offset = Vector2Int.zero;
                switch (rot)
                {
                    case Direction.Up: offset = new Vector2Int(newx, newy); break;
                    case Direction.Right: offset = new Vector2Int(newy, -newx); break; 
                    case Direction.Down: offset = new Vector2Int(-newx, -newy); break;
                    case Direction.Left: offset = new Vector2Int(-newy, newx); break;
                }
                positions.Add(gridPos + offset);
            }
        }
        return positions;
    }

    protected ModuleManager _assignedManager;
    
    // Public accessor for SaveSystem
    public ModuleManager AssignedManager => _assignedManager;
    
    // Helper to modify registry cleanly
    protected void UpdateRegistration(Vector2Int oldPos, Direction oldRot, int oldFlip, Vector2Int newPos, Direction newRot, int newFlip)
    {
        if (_assignedManager == null) return;
        
        // Unregister Old
        var oldFootprint = GetOccupiedPositions(oldPos, oldRot, oldFlip);
        _assignedManager.UnregisterAtPositions(oldFootprint, this);
        
        // Register New (Calling RegisterComponent works because it uses CURRENT state properties, 
        // ASSUMING properties are already updated by NetVar by the time this is called?)
        // Yes, OnValueChanged fires AFTER value update.
        _assignedManager.RegisterComponent(this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Listeners
        _netRotationIndex.OnValueChanged += OnRotationChanged;
        _netHeldWordId.OnValueChanged += OnHeldWordIdChanged;
        _netLastInputDir.OnValueChanged += OnInputDirChanged;
        _netGridPosition.OnValueChanged += OnGridPositionChanged;
        
        if (IsServer)
        {
            // Sync Initial Local -> NetVar (Do this FIRST so InitializeManager sees correct value on Server)
            _netRotationIndex.Value = _localRotation;
            _netGridPosition.Value = _localGridPosition;
            
            if (TickManager.Instance != null)
                TickManager.Instance.OnTick += OnTick;

            InitializeManager();
            SnapToGrid();
            UpdateRotationVisual();
            SyncHeldWordFromId();
            UpdateVisuals();
        }
        else
        {
            // 클라이언트: 네트워크 변수가 동기화될 시간을 확보하기 위해 1프레임 지연 초기화
            StartCoroutine(InitializeOnClientDelayed());
        }
    }

    private System.Collections.IEnumerator InitializeOnClientDelayed()
    {
        // 1프레임 대기 (네트워크 페이로드 동기화가 확실히 이루어지도록 함)
        yield return null;

        InitializeManager();
        SnapToGrid();
        UpdateRotationVisual();
        SyncHeldWordFromId();
        UpdateVisuals();
        
        // Debug.Log($"[ComponentBase] Client initialized {name} at {GridPosition}");
    }
    public override void OnNetworkDespawn()
    {
        _netRotationIndex.OnValueChanged -= OnRotationChanged;
        _netHeldWordId.OnValueChanged -= OnHeldWordIdChanged;
        _netLastInputDir.OnValueChanged -= OnInputDirChanged;
        _netGridPosition.OnValueChanged -= OnGridPositionChanged;

        if (_assignedManager != null)
        {
            _assignedManager.UnregisterComponent(this);
        }

        if (IsServer && TickManager.Instance != null)
        {
            TickManager.Instance.OnTick -= OnTick;
        }
        base.OnNetworkDespawn();
    }
    
    private void OnRotationChanged(Direction oldVal, Direction newVal)
    {
         UpdateRotationVisual();
         
         // Update Registry
         int flip = 0;
         if(this is CombinerComponent cc) flip = cc.IsFlipped;
         // Note: GridPosition hasn't changed.
         UpdateRegistration(GridPosition, oldVal, flip, GridPosition, newVal, flip);
    }

    private void OnHeldWordIdChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        SyncHeldWordFromId();
        UpdateVisuals();
    }
    
    private void OnInputDirChanged(Vector2Int oldVal, Vector2Int newVal)
    {
        UpdateVisuals();
    }
    
    private void OnGridPositionChanged(Vector2Int oldVal, Vector2Int newVal)
    {
        // Re-register at new position
        if (_assignedManager != null)
        {
             int flip = 0;
             if(this is CombinerComponent cc) flip = cc.IsFlipped;
             
             UpdateRegistration(oldVal, RotationIndex, flip, newVal, RotationIndex, flip);
             
             transform.position = _assignedManager.GridToWorldPosition(newVal.x, newVal.y);
        }
    }
    
    private void InitializeManager()
    {
        if (_assignedManager == null)
        {
            // Try explicit parent first (classic)
            _assignedManager = GetComponentInParent<ModuleManager>();
            
            // If not found, search by world position (robust for inner worlds)
            if (_assignedManager == null && ModuleManager.AllManagers.Count > 0)
            {
                foreach (var mgr in ModuleManager.AllManagers)
                {
                    // Check if we are inside this manager's bounds (roughly)
                    // Just check if WorldToGrid returns valid bounds
                    Vector2Int gPos = mgr.WorldToGridPosition(transform.position);
                    if (mgr.IsWithinBounds(gPos.x, gPos.y))
                    {
                        _assignedManager = mgr;
                        break;
                    }
                }
            }
            
            // Fallback to singleton
            if (_assignedManager == null) _assignedManager = ModuleManager.Instance;
        }
        
        if (_assignedManager != null)
        {
            _assignedManager.RegisterComponent(this);
            // Debug.Log($"[ComponentBase] {name} registered to Manager {_assignedManager.name} at {GridPosition}");
        }
        else
        {
             Debug.LogWarning($"[ComponentBase] {name} failed to find a ModuleManager!");
        }
        
        // Visualizer Init
        _visualizer = GetComponentInChildren<WordVisualizer>();
        if (_visualizer == null)
        {
            GameObject vizObj = new GameObject("WordVisualizer");
            vizObj.transform.SetParent(transform);
            vizObj.transform.localPosition = Vector3.zero;
            _visualizer = vizObj.AddComponent<WordVisualizer>();
        }
    }
    
    // Manual helper for Ports or special components
    public void SetManager(ModuleManager manager)
    {
        _assignedManager = manager;
        if (_assignedManager != null && IsServer)
        {
            _assignedManager.RegisterComponent(this);
        }
    }

    private void SyncHeldWordFromId()
    {
        string id = _netHeldWordId.Value.ToString();
        if (string.IsNullOrEmpty(id))
        {
            HeldWord = null;
        }
        else
        {
            // Lookup via Manager
            if (ModuleManager.Instance != null)
            {
                var word = ModuleManager.Instance.FindWordById(id);
                if (word == null && !string.IsNullOrEmpty(id)) 
                    Debug.LogWarning($"[ComponentBase] Could not find word for ID: {id}");
                HeldWord = word;
            }
            else
            {
                 Debug.LogError("[ComponentBase] ModuleManager.Instance is null during SyncHeldWordFromId!");
            }
        }
        // Debug.Log($"[Debug] SyncHeldWord. ID: {id}, Found: {HeldWord?.wordName}");
    }

    protected WordVisualizer _visualizer;

    [ContextMenu("Snap to Grid")]
    public void SnapToGrid()
    {
        if (_assignedManager == null) InitializeManager();

        if (_assignedManager != null)
        {
            // If Server, valid to set. If Client, we TRUST the NetworkVariable.
            if (IsServer || !IsSpawned)
            {
                Vector2Int calcPos = _assignedManager.WorldToGridPosition(transform.position);
                SetGridPositionServer(calcPos);
            }
            
            // Snap visual to the authoritative GridPosition
            transform.position = _assignedManager.GridToWorldPosition(GridPosition.x, GridPosition.y); 
        }
    }

    // Double Buffering
    protected long _lastReceivedTick = -1;

    // Returns true if the component successfully accepted the word
    // SERVER ONLY
    public virtual bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        if (!IsServer) return false;

        if (HeldWord == null)
        {
            HeldWord = word;
            _netHeldWordId.Value = word != null ? new FixedString64Bytes(word.id) : new FixedString64Bytes("");
            
            // Fix: Convert World Direction to Local Direction for Visualizer
            // Visualizer is a child, so it inherits rotation.
            // If item moves RIGHT (1,0) and Component is ROTATED 90 (Facing Right),
            // World Right (1,0) should be seen as Local Up (0,1) relative to component? No.
            // WorldToLocalDirection converts World Vector to Local Vector.
            // Component Rotation 90 means local X is World Y... wait. 
            // Let's rely on the math method already in ComponentBase.
            Vector2Int localDir = WorldToLocalDirection(direction);
            
            _netLastInputDir.Value = localDir; // Store LOCAL dir for visualizer
            
            if (TickManager.Instance != null)
                _lastReceivedTick = TickManager.Instance.CurrentTick;
            
            // Immediate visual update on Server (Client gets it via OnValueChanged)
            UpdateVisuals(); 
            return true;
        }
        return false;
    }

    protected virtual void OnTick(long tickCount)
    {
        if (!IsServer) return; // Redundant safety

        if (_lastReceivedTick == tickCount) return;
        
        if (HeldWord != null)
        {
            Vector2Int targetPos = GridPosition + GetOutputDirection();
            ComponentBase targetComponent = _assignedManager != null ? _assignedManager.GetComponentAt(targetPos) : null;

            if (targetComponent != null)
            {
                if (targetComponent.AcceptWord(HeldWord, GetOutputDirection(), targetPos))
                {
                    HeldWord = null;
                    _netHeldWordId.Value = "";
                    UpdateVisuals();
                }
            }
        }
    }

    protected virtual void UpdateVisuals()
    {
        if (_visualizer != null)
        {
            Vector2Int animDir = IsSpawned ? _netLastInputDir.Value : Vector2Int.zero;
            _visualizer.UpdateVisual(HeldWord, animDir);
        }
    }

    // Handled by OnNetworkDespawn
    public override void OnDestroy() {}

    public virtual void Rotate()
    {
        // Support both Server(Spawned) and Local(Unspawned) rotation
        if (IsServer || !IsSpawned)
        {
            RotationIndex = (Direction)(((int)RotationIndex + 1) % 4);
        }
    }
    
    // For Editor time
    protected virtual void OnValidate()
    {
        UpdateRotationVisual();
    }

    private void UpdateRotationVisual()
    {
        // Use property to get value (works for both Prefab and Network Instance)
        // But property reads NetworkVariable.Value which throws if not spawned...
        // We need a fallback for editor/prefab mode
        int rot = 0;
        try { rot = (int)RotationIndex; } catch { rot = 0; } // Hacky but safe for prefab view
        
        transform.rotation = Quaternion.Euler(0, 0, -90 * rot);
    }
    
    public Vector2Int GetOutputDirection()
    {
        switch (RotationIndex)
        {
            case Direction.Up: return Vector2Int.up;
            case Direction.Right: return Vector2Int.right;
            case Direction.Down: return Vector2Int.down;
            case Direction.Left: return Vector2Int.left;
            default: return Vector2Int.up;
        }
    }
    
    public Vector2Int WorldToLocalDirection(Vector2Int worldDir)
    {
        int r = (int)RotationIndex;
        int x = worldDir.x;
        int y = worldDir.y;
        
        for (int i=0; i<r; i++) 
        { 
            int temp = x; 
            x = -y; 
            y = temp; 
        }
        if(this is CombinerComponent newcombiner && newcombiner.IsFlipped==1){
            x=-x;
        }
        return new Vector2Int(x, y);
    }
    
    public Vector2Int WorldToLocalOffset(Vector2Int worldOffset)
    {
        return WorldToLocalDirection(worldOffset);
    }
}
