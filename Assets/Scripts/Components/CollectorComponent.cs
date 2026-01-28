using UnityEngine;

public class CollectorComponent : ComponentBase
{
    // 1x1 Size as requested
    public override int GetWidth() => 1;
    public override int GetHeight() => 1;

    // SERVER ONLY
    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        if (!IsServer) return false;

        // Unlimited Capacity: Process immediately
        if (GoalManager.Instance != null && word != null)
        {
            GoalManager.Instance.SubmitWord(word);
        }
        
        // Visual Feedback (show brief flash of item)
        // We overwrite any existing held word.
        // Note: Using SetHeldWordServer triggers visuals.
        SetHeldWordServer(word);
        
        // Calculate and set input direction for visualizer (same as base)
        Vector2Int localDir = WorldToLocalDirection(direction);
        _netLastInputDir.Value = localDir;
        UpdateVisuals();

        return true;
    }

    protected override void OnTick(long tickCount)
    {
       // Cleanup Visuals only. Do NOT push items out.
       if (HeldWord != null)
       {
           ClearHeldWord();
       }
    }
}
