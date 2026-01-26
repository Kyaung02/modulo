using UnityEngine;
using System.Collections.Generic;

public class DistributerComponent : ComponentBase
{
    public int isFlipped = 0;

    public int _nextOutputIndex = 0; // 0: Left Output, 1: Right Output
    
    protected override void Start()
    {
        base.Start();

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
            if(TryOutput(HeldWord,GetOutputDirection())){
                HeldWord = null;
                UpdateVisuals();
                _nextOutputIndex=0;
                return;
            }
            else if(TryOutput(HeldWord,DirectionUtility.CW(GetOutputDirection()))){
                HeldWord = null;
                UpdateVisuals();
                _nextOutputIndex=1;
                return;
            }
        }
        else{   
            if(TryOutput(HeldWord,DirectionUtility.CW(GetOutputDirection()))){
                HeldWord = null;
                UpdateVisuals();
                _nextOutputIndex=1;
                return;
            }
            else if(TryOutput(HeldWord,GetOutputDirection())){
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

    // Helper: Need these repeated from Combiner? 
    // Ideally should be in ComponentBase, but for now duplicate to avoid base refactor risk.
    // Actually, let's just create protected helpers in ComponentBase in next pass.
    // For now, local duplicate.
    
    private Vector2Int LocalToWorldOffset(Vector2Int localOffset)
    {
         int x = localOffset.x;
         int y = localOffset.y;
         for (int i=0; i<(int)RotationIndex; i++) { int temp = x; x = y; y = -temp; }
         return new Vector2Int(x, y);
    }
    
    // WorldToLocalDirection removed (Inherited)

    private Vector2Int LocalToWorldDirection(Vector2Int localDir)
    {
        return LocalToWorldOffset(localDir);
    }

}
