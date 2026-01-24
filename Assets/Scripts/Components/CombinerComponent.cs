using UnityEngine;

public class CombinerComponent : ComponentBase
{
    [SerializeField] private WordData _inputA;
    [SerializeField] private WordData _inputB;
    
    // 2x1 Size (Wide to accept 2 parallel inputs from bottom)
    public override int GetWidth() => 2;
    public override int GetHeight() => 1;
    
    protected override void Start()
    {
        base.Start();
        
        // Reposition Visualizer to center of 2x1 block
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
            // Calculate which cell is being hit in local space
            Vector2Int worldOffset = targetPos - GridPosition;
            Vector2Int localPos = WorldToLocalOffset(worldOffset);

            // Cell (0,0) is Left -> Input A
            if (localPos == Vector2Int.zero) 
            {
                if (_inputA == null)
                {
                    _inputA = word;
                    return true;
                }
            }
            // Cell (1,0) is Right -> Input B
            else if (localPos == new Vector2Int(1, 0)) 
            {
                if (_inputB == null)
                {
                    _inputB = word;
                    return true;
                }
            }
        }
        
        return false;
    }

    protected override void OnTick(long tickCount)
    {
        // 1. Try to push existing result
        if (HeldWord != null)
        {
            // Output: Top of Left Cell (Local 0, 1)
            Vector2Int localOutputOffset = new Vector2Int(0, 1);
            Vector2Int worldOutputOffset = LocalToWorldOffset(localOutputOffset);
            Vector2Int targetPos = GridPosition + worldOutputOffset;
            
            ComponentBase targetComponent = ModuleManager.Instance.GetComponentAt(targetPos);

            if (targetComponent != null)
            {
                // Flow Direction: UP relative to us (Local 0,1)
                Vector2Int localFlowDir = Vector2Int.up; 
                Vector2Int worldFlowDir = LocalToWorldDirection(localFlowDir);

                if (targetComponent.AcceptWord(HeldWord, worldFlowDir, targetPos))
                {
                    HeldWord = null;
                    UpdateVisuals();
                }
            }
        }

        // 2. Combine Logic
        if (HeldWord == null && _inputA != null && _inputB != null)
        {
            if (ModuleManager.Instance.recipeDatabase != null)
            {
                WordData result = ModuleManager.Instance.recipeDatabase.GetOutput(_inputA, _inputB);
                if (result != null)
                {
                    // Successful combination
                    HeldWord = result;
                    _inputA = null;
                    _inputB = null;
                    UpdateVisuals();
                }
            }
        }
    }
    
    // Helper: World Offset to Local Offset (Reverse Rotation)
    private Vector2Int WorldToLocalOffset(Vector2Int worldOffset)
    {
        int r = (int)RotationIndex;
        int x = worldOffset.x;
        int y = worldOffset.y;
        
        // Undo rotation (CCW)
        for (int i=0; i<r; i++)
        {
             // Inverse of (y, -x) is (-y, x)
             int temp = x;
             x = -y;
             y = temp;
        }
        return new Vector2Int(x, y);
    }
    
    // Helper: Rotate world direction to local
    private Vector2Int WorldToLocalDirection(Vector2Int worldDir)
    {
        // Reverse rotation
        // 0: 0, 1: -90, 2: -180, 3: -270
        // We need to apply inverse of RotationIndex
        
        Vector2Int dir = worldDir;
        for (int i=0; i< (4 - (int)RotationIndex) % 4; i++)
        {
            // Rotate -90 degrees (x,y) -> (y, -x) ? No.
            // Rotate 90 CW: (x,y) -> (y, -x)
            // We want to undo rotation.
            // If Rotation is 1 (90 CW), we want to rotate -90 (CCW).
            // CCW: (x,y) -> (-y, x)
            
            // Wait, let's stick to standard rotation loop of undoing
            // Or simpler math.
        }
        
        // Simpler: Just manual switch for inverse
        // If Rotation 1 (Right): World Up (0,1) -> Local Left (-1,0)?
        // Machine facing Right. Bottom is World Left. Input from Left (Right direction) enters Bottom.
        
        // Let's use standard helpers in ComponentBase if possible, but we don't have them yet.
        // Implement locally.
        
        int r = (int)RotationIndex;
        if (r == 0) return worldDir;
        
        int x = worldDir.x;
        int y = worldDir.y;
        
        // Undo rotation (CCW)
        for (int i=0; i<r; i++)
        {
             // Inverse of (y, -x) is (-y, x)
             int temp = x;
             x = -y;
             y = temp;
        }
        return new Vector2Int(x, y);
    }

    private Vector2Int LocalToWorldOffset(Vector2Int localOffset)
    {
         int x = localOffset.x;
         int y = localOffset.y;
         
         // Apply Rotation (CW)
         for (int i=0; i<(int)RotationIndex; i++)
         {
             // CW: (x,y) -> (y, -x)
             int temp = x;
             x = y;
             y = -temp;
         }
         return new Vector2Int(x, y);
    }
    
    private Vector2Int LocalToWorldDirection(Vector2Int localDir)
    {
        return LocalToWorldOffset(localDir); // Same logic for vectors
    }

    protected override void UpdateVisuals()
    {
        if (_visualizer != null)
        {
            _visualizer.UpdateVisual(HeldWord);
        }
    }
}
