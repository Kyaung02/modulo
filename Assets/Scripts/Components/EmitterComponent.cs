using UnityEngine;

public class EmitterComponent : ComponentBase
{
    [Header("Emitter Settings")]
    public WordData wordToEmit; // The word this emitter produces
    
    protected override void OnTick(long tickCount)
    {
        // 1. Try to push existing word first (base behavior)
        base.OnTick(tickCount);

        // 2. If empty and has emission definition, spawn new word
        if (HeldWord == null && wordToEmit != null)
        {
            SetHeldWordServer(wordToEmit);
        }
    }
}
