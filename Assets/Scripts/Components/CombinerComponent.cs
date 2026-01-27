using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class CombinerComponent : ComponentBase
{
    // Networked State
    private NetworkVariable<int> _netIsFlipped = new NetworkVariable<int>(0);
    private NetworkVariable<FixedString64Bytes> _netInputAId = new NetworkVariable<FixedString64Bytes>("");
    private NetworkVariable<FixedString64Bytes> _netInputBId = new NetworkVariable<FixedString64Bytes>("");

    private int _localIsFlipped = 0; // Local backing field

    public int IsFlipped
    {
        get => IsSpawned ? _netIsFlipped.Value : _localIsFlipped;
        set 
        { 
            if (IsSpawned && IsServer) 
            {
                _netIsFlipped.Value = value;
            }
            else if (!IsSpawned)
            {
                _localIsFlipped = value;
                UpdateFlipVisual();
            }
        }
    }
    
    public void SetFlippedInitial(int flip)
    {
        _localIsFlipped = flip;
        if(IsSpawned && IsServer) _netIsFlipped.Value = flip;
        UpdateFlipVisual();
    }
    
    public void PrepareFlip(int flip)
    {
        // Safe: Set local only. Server OnNetworkSpawn will sync to NetVar.
        _localIsFlipped = flip;
    }

    private WordData _inputA; // Local cache (Server & Client)
    private WordData _inputB; // Local cache (Server & Client)

    // 2x1 Size (Wide to accept 2 parallel inputs from bottom)
    public override int GetWidth() => 2;
    public override int GetHeight() => 1;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _netIsFlipped.OnValueChanged += OnFlipChanged;
        _netInputAId.OnValueChanged += OnInputAChanged;
        _netInputBId.OnValueChanged += OnInputBChanged;
        
        // Initial Sync
        SyncInputs();
        UpdateFlipVisual();
        
        if (IsServer)
        {
            _netIsFlipped.Value = _localIsFlipped;
        }

        if (_visualizer != null)
        {
            _visualizer.transform.localPosition = new Vector3(0.5f, 0, 0); 
        }
    }
    
    public override void OnNetworkDespawn()
    {
        _netIsFlipped.OnValueChanged -= OnFlipChanged;
        _netInputAId.OnValueChanged -= OnInputAChanged;
        _netInputBId.OnValueChanged -= OnInputBChanged;
        base.OnNetworkDespawn();
    }
    
    private void OnFlipChanged(int oldVal, int newVal) 
    { 
        UpdateFlipVisual(); 
        
        // Update Registry (Footprint changes with Flip)
        UpdateRegistration(GridPosition, RotationIndex, oldVal, GridPosition, RotationIndex, newVal);
    }
    
    private void UpdateFlipVisual()
    {
        // Visual Flip
        Vector3 s = transform.localScale;
        // If flipped (1), x is negative? Or relative to base?
        // Assuming base scale is (1,1,1).
        // Check BuildManager logic: it flipped scale.x
        float targetX = (IsFlipped == 1) ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        transform.localScale = new Vector3(targetX, s.y, s.z);
    }
    
    private void OnInputAChanged(FixedString64Bytes o, FixedString64Bytes n) { SyncInputs(); }
    private void OnInputBChanged(FixedString64Bytes o, FixedString64Bytes n) { SyncInputs(); }

    private void SyncInputs()
    {
        if (ModuleManager.Instance == null) return;
        
        string idA = _netInputAId.Value.ToString();
        _inputA = string.IsNullOrEmpty(idA) ? null : ModuleManager.Instance.FindWordById(idA);
        
        string idB = _netInputBId.Value.ToString();
        _inputB = string.IsNullOrEmpty(idB) ? null : ModuleManager.Instance.FindWordById(idB);
    }

    // SERVER ONLY
    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        if (!IsServer) return false;

        // Inputs from Bottom (Local UP direction)
        Vector2Int localDir = WorldToLocalDirection(direction);
        if (localDir == Vector2Int.up)
        {
            Vector2Int worldOffset = targetPos - GridPosition;
            //Debug.Log("TargetPos: "+targetPos);
            //Debug.Log("GridPosition: "+GridPosition);
            //Debug.Log("WorldOffset: "+worldOffset);
            Vector2Int localPos = WorldToLocalOffset(worldOffset);
            //Debug.Log("Coming from: "+localPos);

            // Cell (0,0) is Left -> Input A
            if (localPos == new Vector2Int(0,-1)) 
            {
                Debug.Log("Comingfrom A");
                if (_inputA == null)
                {
                    Debug.Log("Item received to Slot A");
                    _inputA = word;
                    _netInputAId.Value = word.id;
                    return true;
                }
            }
            // Cell (1,0) is Right -> Input B
            else if (localPos == new Vector2Int(1,-1)) 
            {
                if (_inputB == null)
                {
                    _inputB = word;
                    _netInputBId.Value = word.id;
                    return true;
                }
            }
        }
        
        return false;
    }

    protected override void OnTick(long tickCount)
    {
        if (!IsServer) return;

        // 1. Try to push existing result
        if (HeldWord != null)
        {
            // Output: Top of Left Cell (Local 0, 1)
            Vector2Int localOutputOffset = new Vector2Int(0, 1);
            Vector2Int worldOutputOffset = LocalToWorldOffset(localOutputOffset);
            Vector2Int targetPos = GridPosition + worldOutputOffset;
            // Debug.Log("targetPos: "+targetPos);
            
            ComponentBase targetComponent = _assignedManager.GetComponentAt(targetPos);

            if (targetComponent != null)
            {
                //Debug.Log("target Found");
                if (targetComponent.AcceptWord(HeldWord, GetOutputDirection(), GridPosition))
                {
                    //Debug.Log("target Accepted");
                    ClearHeldWord(); // Base component syncs this
                    UpdateVisuals();
                }
            }
        }

        // 2. Combine Logic
        //여기서 쓰는 ModuleManager.Instance는 첫번째 모듈매니저지만, 딱히 짜피 다른 매니저는 레시피 없으니까 걍 이렇게 둘게요~~
        if (HeldWord == null && _inputA != null && _inputB != null)
        {
            if (ModuleManager.Instance.recipeDatabase != null)
            {
                WordData result = ModuleManager.Instance.recipeDatabase.GetOutput(_inputA, _inputB);
                if (result != null)
                {
                    // Successful combination
                    SetHeldWordServer(result);
                    
                    _inputA = null;
                    _netInputAId.Value = "";
                    _inputB = null;
                    _netInputBId.Value = "";
                    
                    UpdateVisuals();
                }
            }
        }
    }
    
    // Legacy helper: LocalToWorldOffset was manual but now we can use WorldToLocal inverted?
    // Or just implement it. ComponentBase has WorldToLocal. Parent does NOT have LocalToWorld.
    // We need LocalToWorld for Output Direction calculation.
    
    private Vector2Int LocalToWorldOffset(Vector2Int localOffset)
    {
         int x = localOffset.x;
         int y = localOffset.y;
         
         // Invert Flip: x -> -x if flipped
         if(IsFlipped==1){
            x=-x;
         }
         
         // Invert Rotation (CCW -> CW or reversed??)
         // WorldToLocal rotates CCW (Counter Clockwise) by RotationIndex.
         // So LocalToWorld should rotate CW by RotationIndex.
         // (x,y) -> (y, -x) for CW 90
         
         for (int i=0; i<(int)RotationIndex; i++)
         {
             int temp = x;
             x = y;
             y = -temp;
         }
         //Debug.Log("before flip: " + x + ", " + y);
         //Debug.Log("after flip: " + x + ", " + y);
         return new Vector2Int(x, y);
    }
    
    private Vector2Int LocalToWorldDirection(Vector2Int localDir)
    {
        return LocalToWorldOffset(localDir); 
    }

    protected override void UpdateVisuals()
    {
        // We can visualizer inputs too if we want?
        // For now just output.
        if (_visualizer != null)
        {
            _visualizer.UpdateVisual(HeldWord, LastInputDir);
        }
    }
}
