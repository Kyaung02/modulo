using UnityEngine;

public class CollectorComponent : ComponentBase
{
    // 1x1 Size as requested
    public override int GetWidth() => 1;
    public override int GetHeight() => 1;

    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        // Collector accepts ANY word from ANY direction
        if (GoalManager.Instance != null)
        {
            GoalManager.Instance.SubmitWord(word);
            
            // Visual feedback (maybe floating text later)
            // Debug.Log($"Collected: {word.wordName}");
            
            return true; // Consumed immediately
        }
        return false;
    }

    protected override void OnTick(long tickCount)
    {
       // Collector does not hold items, it consumes them instantly in AcceptWord.
       // So OnTick is empty or handles animation.
    }
}
