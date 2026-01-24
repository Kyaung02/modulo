using UnityEngine;
using System.Collections.Generic;

public class BalancerComponent : ComponentBase
{
    // 2x1 Size
    public override int GetWidth() => 2;
    public override int GetHeight() => 1;

    [SerializeField] private List<WordData> _buffer = new List<WordData>(); // Queue serialized for debug
    private int _nextOutputIndex = 0; // 0: Left Output, 1: Right Output
    
    protected override void Start()
    {
        base.Start();
        
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
            // Accept if buffer is not full (limit buffer to e.g. 2 items for visual simplicity)
            if (_buffer.Count < 2)
            {
                _buffer.Add(word);
                
                // If this is the FIRST item (Visible one), animate it in
                if (_buffer.Count == 1 && _visualizer != null)
                {
                    _visualizer.UpdateVisual(word, localDir);
                }
                else
                {
                    UpdateVisuals();
                }
                return true;
            }
        }
        
        return false;
    }

    protected override void OnTick(long tickCount)
    {
        // Splitter Logic:
        // Take first item in queue, try to send to next output port.
        
        if (_buffer.Count > 0)
        {
            WordData wordToSend = _buffer[0];
            
            // Try preferred output first
            if (TryOutput(wordToSend, _nextOutputIndex))
            {
                // Success
                _buffer.RemoveAt(0);
                _nextOutputIndex = (_nextOutputIndex + 1) % 2; // Toggle
                UpdateVisuals();
            }
            else
            {
                // Preferred blocked, try the other one? (Smart Balance)
                int otherIndex = (_nextOutputIndex + 1) % 2;
                if (TryOutput(wordToSend, otherIndex))
                {
                    // Success on other port
                    _buffer.RemoveAt(0);
                    // Don't toggle preference, keep trying the blocked one next time to maintain ratio if possible?
                    // Or just proceed. Let's simpler: Just consume.
                    UpdateVisuals();
                }
            }
        }
    }

    private bool TryOutput(WordData word, int outputIndex)
    {
        // Output 0: Top-Left (Local 0, 1)
        // Output 1: Top-Right (Local 1, 1)
        
        Vector2Int localCellPos = new Vector2Int(outputIndex, 0); // The cell we are outputting FROM (0,0 or 1,0)
        // Wait, Splitter is 2x1. Cells are (0,0) and (1,0).
        // It outputs upwards. 
        // Neighbor of (0,0) UP is (0,1).
        // Neighbor of (1,0) UP is (1,1).
        
        Vector2Int localOutputOffset = new Vector2Int(outputIndex, 1); // Target in Local Space relative to pivot (0,0)
        
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

    protected override void UpdateVisuals()
    {
        if (_visualizer != null && _buffer.Count > 0)
        {
            _visualizer.UpdateVisual(_buffer[0]);
        }
        else if (_visualizer != null)
        {
            _visualizer.UpdateVisual(null);
        }
    }
}
