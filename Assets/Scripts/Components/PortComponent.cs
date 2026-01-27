using UnityEngine;
using Unity.Netcode;

public class PortComponent : ComponentBase
{
    // Direction this port faces relative to the grid center
    // Up Port is at (3, 6), facing Up (Outwards)
    public Direction wallDirection;
    
    public RecursiveModuleComponent parentModule; // Link to the outer shell

    // Visuals
    private NetworkVariable<Color> _netBodyColor = new NetworkVariable<Color>(Color.black);
    private GameObject _bodyVisual;

    public void SetBodyColor(Color c)
    {
        if (IsServer) 
        {
            _netBodyColor.Value = c;
            UpdateBodyColor(); // Force update on Server
        }
    }

    private void CreateBodyVisual()
    {
        if (_bodyVisual != null) return;
        
        // Cleanup existing "Quad" from Prefab/AutoSetup
        var existing = transform.Find("Quad");
        if (existing) Destroy(existing.gameObject);
        
        _bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _bodyVisual.transform.SetParent(transform);
        // Reset transform to avoid weird scaling from parent
        // Offset the visual by 1.0 unit locally in the 'Facing' direction (assuming rotation handles it)
        // User requested: "0.4f 말고 1f 밀어줘" and "prefab자체를 한쪽에 치우치게"
        _bodyVisual.transform.localPosition = new Vector3(0, 0.5f, 0);
        _bodyVisual.transform.localRotation = Quaternion.identity;
        _bodyVisual.transform.localScale = new Vector3(1.0f, 0.2f, 1.0f);
        
        Destroy(_bodyVisual.GetComponent<Collider>());
        
        var ren = _bodyVisual.GetComponent<Renderer>();
        if (ren)
        {
            ren.material = new Material(Shader.Find("Sprites/Default"));
            ren.sortingOrder = 5; // Ensure it renders above background
        }
    }
    
    private void UpdateBodyColor()
    {
        if (_bodyVisual != null)
        {
            var ren = _bodyVisual.GetComponent<Renderer>();
            if (ren) ren.material.color = _netBodyColor.Value;
        }
    }

    // Helper to auto-configure based on position
    public void Configure(RecursiveModuleComponent parent, Direction dir)
    {
        parentModule = parent;
        wallDirection = dir;
    }

    private NetworkVariable<NetworkBehaviourReference> _netParentModule = new NetworkVariable<NetworkBehaviourReference>();

    public void SetParentModule(RecursiveModuleComponent parent)
    {
        if (IsServer) _netParentModule.Value = parent;
        else Debug.LogWarning("[PortComponent] Client trying to set parent module!");
        
        parentModule = parent;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Resolve Reference on Client
        if (!IsServer)
        {
            if (_netParentModule.Value.TryGet(out RecursiveModuleComponent parent))
            {
                parentModule = parent;
            }
        }
        
        // Link to InnerGrid if found
        if (parentModule != null && parentModule.innerGrid != null)
        {
            SetManager(parentModule.innerGrid);
        }
        else
        {
            // If parentModule logic fails, fallback to hierarchy or world search (Base already does world search)
            if (_assignedManager == null) 
                 _assignedManager = GetComponentInParent<ModuleManager>();
        }

        // Manually register to Tick system
        if (TickManager.Instance != null && IsServer)
        {
            TickManager.Instance.OnTick += OnTick;
        }
        
        // Listen for late updates
        _netParentModule.OnValueChanged += (pid, nid) => {
            if (nid.TryGet(out RecursiveModuleComponent parent)) {
                 parentModule = parent;
                 if (parent.innerGrid != null) SetManager(parent.innerGrid);
            }
        };
        
        // Initialize Body Visual
        CreateBodyVisual();
        _netBodyColor.OnValueChanged += (oldC, newC) => UpdateBodyColor();
        UpdateBodyColor();
    }

    public override void OnNetworkDespawn()
    {
        if (TickManager.Instance != null && IsServer)
        {
            TickManager.Instance.OnTick -= OnTick;
        }
        base.OnNetworkDespawn();
    }

    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        // When item arrives at Port from INSIDE the module:
        // It wants to leave the module.
        // We pass it to the parentModule to handle external dispensing.
        
        if (parentModule != null)
        {
            return parentModule.ExportItem(word, wallDirection);
        }
        return false;
    }

    // Called by Parent Module to spawn item coming from Outside
    public bool ImportItem(WordData word)
    {
        // Item entering from outside.
        // We need to push it into the inner grid (Opposite of wallDirection).
        // e.g. Top Port (Up) -> Push Down (Inner).
        
        // Just hold it? Or push immediately?
        // ComponentBase default logic pushes to "OutputDirection".
        // We should set our RotationIndex such that OutputDirection is Inwards.
        
        // Top Port (Up Wall): Needs to push Down. Rotation should be Down (2).
        // Right Port: Push Left. Rotation Left (3).
        
        // We can just hold it, and OnTick will push it.
        // We can just hold it, and OnTick will push it.
        if (HeldWord == null)
        {
            Debug.Log($"[PortComponent] Imported Item: {word.id}");
            SetHeldWordServer(word);
            return true;
        }
        else
        {
            Debug.Log("[PortComponent] Import failed: Already holding word.");
        }
        return false;
    }

    public WordData infiniteSourceWord; // If set, this port generates items infinitely (for Root World)

    protected override void OnTick(long tickCount)
    {
        // Infinite Source Logic (Root World)
        if (HeldWord == null && infiniteSourceWord != null && parentModule == null)
        {
            SetHeldWordServer(infiniteSourceWord);
        }

        // Port is out of bounds, so we need special logic to push into inner grid
        if (HeldWord != null && _assignedManager != null)
        {
            // Get output direction (should point inward into the grid)
            Vector2Int outputDir = GetOutputDirection();
            
            // Port is out of bounds; compute grid pos from world position each tick
            Vector2Int portGridPos = _assignedManager.WorldToGridPosition(transform.position);
            Vector2Int targetPos = portGridPos + outputDir;
            
            // Check if target is within bounds
            if (_assignedManager.IsWithinBounds(targetPos.x, targetPos.y))
            {
                ComponentBase targetComponent = _assignedManager.GetComponentAt(targetPos);
                
                if (targetComponent != null)
                {
                    // Try to give the word to the target
                    if (targetComponent.AcceptWord(HeldWord, outputDir, GridPosition))
                    {
                        // Successfully passed the word
                        // Since we are Server, we use base SetHeldWordServer(null) or ClearHeldWord()?
                        // Base has ClearHeldWord() but checks IsServer internally.
                        ClearHeldWord(); 
                    }
                }
            }
        }
    }

    protected override void UpdateVisuals()
    {
        // 포트에서는 아이템(Word)을 시각적으로 표시하지 않음
    }
}
