using UnityEngine;

public class CombinerComponent : ComponentBase
{
    [SerializeField] private WordData _inputA;
    [SerializeField] private WordData _inputB;
    
    // 2x1 Size (Wide to accept 2 parallel inputs from bottom)
    public override int GetWidth() => 2;
    public override int GetHeight() => 1;
    public int isFlipped = 0;

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
                //if(result != null)Debug.Log("CombineResult: " + result.wordName);
                //else Debug.Log("CombineResult: null");
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
    
    // Helper methods WorldToLocalOffset and WorldToLocalDirection removed as they are now inherited from ComponentBase

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
