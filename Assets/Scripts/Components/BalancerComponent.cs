using UnityEngine;
using System.Collections.Generic;

public class BalancerComponent : ComponentBase
{
    // 2x1 Size
    public override int GetWidth() => 2;
    public override int GetHeight() => 1;

    [SerializeField] private List<WordData> _buffer = new List<WordData>(); // Queue serialized for debug
    private int _nextOutputIndex = 0; // 0: Left Output, 1: Right Output
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Reposition Visualizer to center of 2x1 block
        // Base creates it at (0,0), we want it at (0.5, 0)
        if (_visualizer != null)
        {
            _visualizer.transform.localPosition = new Vector3(0.5f, 0, 0); 
        }
    }

    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        // Inputs from Bottom (Local UP direction)
        Vector2Int localDir = WorldToLocalDirection(direction);

        if (localDir == Vector2Int.up)
        {
            // Sync Fix: Balancer uses HeldWord as its "Active" item buffer (Limit 1)
            // This ensures built-in sync works.
            if (HeldWord == null)
            {
                // SetHeldWordServer handles _netHeldWordId and _netLastInputDir automatically!
                // We just need to ensure we pass the correct params if wrapping logic.
                // Actually, componentBase logic handles this if we call base.AcceptWord?
                // No, Balancer has custom logic. We must manually set it.
                
                // IMPORTANT: Use the Base method logic here
                base.AcceptWord(word, direction, targetPos);
                return true;
            }
        }
        
        return false;
    }

    protected override void OnTick(long tickCount)
    {
        // Splitter Logic:
        // Take HeldWord, try to send to next output port.
        
        if (HeldWord != null)
        {
            WordData wordToSend = HeldWord;
            
            // Try preferred output first
            if (TryOutput(wordToSend, _nextOutputIndex))
            {
                // Success
                ClearHeldWord();
                _nextOutputIndex = (_nextOutputIndex + 1) % 2; // Toggle
            }
            else
            {
                // Preferred blocked, try the other one?
                int otherIndex = (_nextOutputIndex + 1) % 2;
                if (TryOutput(wordToSend, otherIndex))
                {
                    ClearHeldWord();
                    // Don't toggle
                }
            }
        }
    }
    
    // ... Output logic remains same ...

    private bool TryOutput(WordData word, int outputIndex)
    {
        // Output 0: Top-Left (Local 0, 1)
        // Output 1: Top-Right (Local 1, 1)
        
        // Target in Local Space relative to pivot (0,0)
        Vector2Int localOutputOffset = new Vector2Int(outputIndex, 1);
        
        Vector2Int worldOutputOffset = LocalToWorldOffset(localOutputOffset);
        Vector2Int targetPos = GridPosition + worldOutputOffset;

        ComponentBase targetComponent = ModuleManager.Instance.GetComponentAt(targetPos);
        
        if (targetComponent != null)
        {
             // Flow logic: We are pushing UP relative to our local space.
             Vector2Int localFlowDir = Vector2Int.up;
             Vector2Int worldFlowDir = LocalToWorldDirection(localFlowDir);
             
             return targetComponent.AcceptWord(word, worldFlowDir, targetPos);
        }
        
        return false;
    }

    private Vector2Int LocalToWorldOffset(Vector2Int localOffset)
    {
         int x = localOffset.x;
         int y = localOffset.y;
         for (int i=0; i<(int)RotationIndex; i++) { int temp = x; x = y; y = -temp; }
         return new Vector2Int(x, y);
    }

    private Vector2Int LocalToWorldDirection(Vector2Int localDir)
    {
        return LocalToWorldOffset(localDir);
    }

    protected override void UpdateVisuals()
    {
        if (_visualizer != null)
        {
            _visualizer.UpdateVisual(HeldWord, LastInputDir);
        }
    }
}
