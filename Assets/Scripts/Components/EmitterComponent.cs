using UnityEngine;

public class EmitterComponent : ComponentBase
{
    [Header("Emitter Settings")]
    public WordData wordToEmit; // The word this emitter produces
    
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
        }
    }

    protected override void OnTick(long tickCount)
    {
        // 1. Try to push existing word first (base behavior)
        base.OnTick(tickCount);

        // 2. If empty and has emission definition, spawn new word
        if (HeldWord == null && wordToEmit != null)
        {
            HeldWord = wordToEmit;
            UpdateVisuals();
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
