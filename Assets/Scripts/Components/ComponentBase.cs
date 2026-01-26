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
        get => IsSpawned ? _netGridPosition.Value : _localGridPosition;
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
        // If spawned (Server), set netvar
        if (IsSpawned && IsServer) _netRotationIndex.Value = dir;
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
        List<Vector2Int> positions = new List<Vector2Int>();
        int w = GetWidth();
        int h = GetHeight();

        // 0: Up (Normal), 1: Right (90 deg CW), 2: Down, 3: Left
        // Need to rotate the local footprint (0,0) to (w-1, h-1) based on RotationIndex
        
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int newx=x,newy=y;
                if(this is CombinerComponent newcombiner && newcombiner.IsFlipped == 1){
                    newx=-newx;
                }
                // Simple rotation logic
                Vector2Int offset = Vector2Int.zero;
                switch (RotationIndex)
                {
                    case Direction.Up: offset = new Vector2Int(newx, newy); break;
                    case Direction.Right: offset = new Vector2Int(newy, -newx); break; // x becomes y, y becomes -x
                    case Direction.Down: offset = new Vector2Int(-newx, -newy); break;
                    case Direction.Left: offset = new Vector2Int(-newy, newx); break;
                }
                positions.Add(GridPosition + offset);
            }
        }
        return positions;
    }

    protected ModuleManager _assignedManager;

    // Replace Start with OnNetworkSpawn for networked init
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Listeners
        _netRotationIndex.OnValueChanged += OnRotationChanged;
        _netHeldWordId.OnValueChanged += OnHeldWordIdChanged;
        _netLastInputDir.OnValueChanged += OnInputDirChanged; // Fix: Listen for anim dir changes
        _netGridPosition.OnValueChanged += OnGridPositionChanged;
        
        InitializeManager();
        SnapToGrid(); // Uses current GridPosition value
        UpdateRotationVisual();
        
        // Sync initial HeldWord state for late joiners
        SyncHeldWordFromId();
        UpdateVisuals();

        // Register to TickManager - SERVER ONLY logic
        if (IsServer)
        {
            // Apply initial rotation from build phase to NetworkVariable
            _netRotationIndex.Value = _localRotation;
            
            if (TickManager.Instance != null)
                TickManager.Instance.OnTick += OnTick;
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

    public override void OnNetworkDespawn()
    {
        _netRotationIndex.OnValueChanged -= OnRotationChanged;
        _netHeldWordId.OnValueChanged -= OnHeldWordIdChanged;
        _netLastInputDir.OnValueChanged -= OnInputDirChanged; // Fix: Unsubscribe

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
    }

    private void OnHeldWordIdChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        SyncHeldWordFromId();
        UpdateVisuals();
    }
    
    // Fix: Trigger visual update when input direction arrives (might be after word id)
    private void OnInputDirChanged(Vector2Int oldVal, Vector2Int newVal)
    {
        UpdateVisuals();
    }
    
    private void OnGridPositionChanged(Vector2Int oldVal, Vector2Int newVal)
    {
        // Re-register at new position
        if (_assignedManager != null)
        {
            // Note: We can't easily 'unregister' from old pos if we don't know it (oldVal provided!)
            // Unregister manually? Manager.Unregister(this, oldVal)? 
            // Current Manager.Unregister uses GetOccupiedPositions which uses CURRENT pos.
            // So we need to handle this carefully. Use internal update or brute force.
            // Simplest: Just Register again (Overwrite).
            _assignedManager.RegisterComponent(this);
            transform.position = _assignedManager.GridToWorldPosition(newVal.x, newVal.y);
            // Debug.Log($"[ComponentBase] {name} moved to {newVal} on Client");
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
