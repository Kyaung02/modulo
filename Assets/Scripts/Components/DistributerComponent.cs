using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class DistributerComponent : ComponentBase
{
    public int _nextOutputIndex = 0; // 0: Left Output, 1: Right Output

    private NetworkVariable<int> _netIsFlipped = new NetworkVariable<int>(0);
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

    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        // Inputs from Bottom (Local UP direction)
        Vector2Int localDir = WorldToLocalDirection(direction);

        if (localDir != Vector2Int.up)return false;
        if (HeldWord == null)
        {
            HeldWord = word;
            if (TickManager.Instance != null)_lastReceivedTick = TickManager.Instance.CurrentTick;
            if (_visualizer != null) _visualizer.UpdateVisual(HeldWord);
            return true;
        }
        return false;
    }

    protected override void OnTick(long tickCount)
    {

        if (_lastReceivedTick == tickCount) return;
        if(HeldWord == null) return;
        if(_nextOutputIndex==1){
            if(TryOutput(HeldWord,Vector2Int.up)){
                HeldWord = null;
                UpdateVisuals();
                _nextOutputIndex=0;
                return;
            }
            else if(TryOutput(HeldWord,Vector2Int.right)){
                HeldWord = null;
                UpdateVisuals();
                _nextOutputIndex=1;
                return;
            }
        }
        else{   
            if(TryOutput(HeldWord,Vector2Int.right)){
                HeldWord = null;
                UpdateVisuals();
                _nextOutputIndex=1;
                return;
            }
            else if(TryOutput(HeldWord,Vector2Int.up)){
                HeldWord = null;
                UpdateVisuals();
                _nextOutputIndex=0;
                return;
            }
        }
    }

    private bool TryOutput(WordData word, Vector2Int direction)
    {
        
        Vector2Int worldOutputOffset = LocalToWorldOffset(direction);
        Vector2Int targetPos = GridPosition + worldOutputOffset;

        ComponentBase targetComponent = ModuleManager.Instance.GetComponentAt(targetPos);
        
        if (targetComponent != null)
        {
             Vector2Int worldFlowDir = LocalToWorldDirection(direction);
             return targetComponent.AcceptWord(word, worldFlowDir, targetPos);
        }
        return false;
    }

    public void PrepareFlip(int flip)
    {
        // Safe: Set local only. Server OnNetworkSpawn will sync to NetVar.
        _localIsFlipped = flip;
    }

    private void UpdateFlipVisual()
    {
        // Visual Flip
        Vector3 s = transform.localScale;
        // If flipped (1), x is negative? Or relative to base?
        // Assuming base scale is (1,1,1).
        // Check BuildManager logic: it flipped scale.x
        float targetX = (_netIsFlipped.Value == 1) ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        transform.localScale = new Vector3(targetX, s.y, s.z);
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _netIsFlipped.OnValueChanged += OnFlipChanged;
        
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
        base.OnNetworkDespawn();
    }

    private void OnFlipChanged(int oldVal, int newVal) 
    { 
        UpdateFlipVisual(); 
        
        // Update Registry (Footprint changes with Flip)
        UpdateRegistration(GridPosition, RotationIndex, oldVal, GridPosition, RotationIndex, newVal);
    }
    
    private Vector2Int LocalToWorldOffset(Vector2Int localOffset)
    {
         int x = localOffset.x;
         int y = localOffset.y;
         //flip: x->-x
         if(_netIsFlipped.Value==1){
            x=-x;
         }
         // Apply Rotation (CW)
         for (int i=0; i<(int)RotationIndex; i++)
         {
             // CW: (x,y) -> (y, -x)
             int temp = x;
             x = y;
             y = -temp;
         }
         //Debug.Log("before flip: " + x + ", " + y);
         //Debug.Log("after flip: " + x + ", " + y);
         return new Vector2Int(x, y);
    }
    
    // WorldToLocalDirection removed (Inherited)

    private Vector2Int LocalToWorldDirection(Vector2Int localDir)
    {
        return LocalToWorldOffset(localDir);
    }

}
