using UnityEngine;

public class MoverComponent : ComponentBase
{
    private WordVisualizer _visualizer;

    protected override void Start()
    {
        base.Start();
        
        // Setup visualizer
        _visualizer = GetComponentInChildren<WordVisualizer>();
        if (_visualizer == null)
        {
            // Optional: Create one if not exists, or expect it to be in prefab
            GameObject vizObj = new GameObject("WordVisualizer");
            vizObj.transform.SetParent(transform);
            vizObj.transform.localPosition = Vector3.zero;
            _visualizer = vizObj.AddComponent<WordVisualizer>();
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
