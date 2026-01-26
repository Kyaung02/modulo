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
        //Debug.Log("Item: "+word.wordName);
        Vector2Int localDir = WorldToLocalDirection(direction);
        if (localDir == Vector2Int.up)
        {
            // Calculate which cell is being hit in local space
            Vector2Int worldOffset = targetPos - GridPosition;
            //Debug.Log("worldOffset: " + worldOffset);
            Vector2Int localPos = WorldToLocalOffset(worldOffset);
            //Debug.Log("localPos: " + localPos);

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
           // Debug.Log("targetPos: "+targetPos);
            
            ComponentBase targetComponent = _assignedManager.GetComponentAt(targetPos);

            if (targetComponent != null)
            {
                //Debug.Log("target Found");
                if (targetComponent.AcceptWord(HeldWord, GetOutputDirection(), targetPos))
                {
                    //Debug.Log("target Accepted");
                    HeldWord = null;
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
         //flip: x->-x
         if(isFlipped==1){
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
