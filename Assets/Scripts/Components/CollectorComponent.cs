using UnityEngine;

public class CollectorComponent : ComponentBase
{
    // 1x1 Size as requested
    public override int GetWidth() => 1;
    public override int GetHeight() => 1;

    protected override void OnTick(long tickCount)
    {
       // Consume held word on tick (after animation plays)
       if (HeldWord != null)
       {
           if (GoalManager.Instance != null)
           {
               GoalManager.Instance.SubmitWord(HeldWord);
           }
           ClearHeldWord();
       }
    }
}
