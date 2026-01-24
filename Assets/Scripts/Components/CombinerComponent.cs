using UnityEngine;

public class CombinerComponent : ComponentBase
{
    private WordData _inputA;
    private WordData _inputB;
    
    private WordVisualizer _visualizer;

    protected override void Start()
    {
        base.Start();
        
        _visualizer = GetComponentInChildren<WordVisualizer>();
        if (_visualizer == null)
        {
            GameObject vizObj = new GameObject("WordVisualizer");
            vizObj.transform.SetParent(transform);
            vizObj.transform.localPosition = Vector3.zero;
            _visualizer = vizObj.AddComponent<WordVisualizer>();
            // Ensure sorting order is high
            vizObj.GetComponent<SpriteRenderer>().sortingOrder = 10;
        }
    }

    public override bool AcceptWord(WordData word, Vector2Int direction)
    {
        // Combiner accepts inputs from any direction EXCEPT its output direction
        // (Simple logic for now: just fill empty slots)
        
        if (_inputA == null)
        {
            _inputA = word;
            return true;
        }
        else if (_inputB == null)
        {
            _inputB = word;
            return true;
        }
        
        return false;
    }

    protected override void OnTick(long tickCount)
    {
        // 1. Try to push existing result (HeldWord)
        base.OnTick(tickCount);

        // 2. If free to work, try to combine
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

    protected override void UpdateVisuals()
    {
        if (_visualizer != null)
        {
            _visualizer.UpdateVisual(HeldWord);
        }
    }
}
