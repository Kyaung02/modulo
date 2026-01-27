using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;

public class TunnelInComponent : ComponentBase
{
    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        Vector2Int localDir = WorldToLocalDirection(direction);
        if (localDir == Vector2Int.up)
        {
            return base.AcceptWord(word, direction, targetPos);
        }
        return false;
    }

    //1. TunnelOut인가?
    //2. 방향이 맞는가?
    protected override void OnTick(long tickCount)
    {
        if (!IsServer) return; // Redundant safety

        if (_lastReceivedTick == tickCount) return;
        
        if (HeldWord == null) return;

        Vector2Int targetPos = GridPosition + GetOutputDirection();
        while(AssignedManager.IsWithinBounds(targetPos.x, targetPos.y))
        {
            ComponentBase target=AssignedManager.GetComponentAt(targetPos);
            if(target is TunnelOutComponent && target.GetOutputDirection() == GetOutputDirection())break;
            if(target is TunnelInComponent && target.GetOutputDirection() == GetOutputDirection())return;
            targetPos += GetOutputDirection();
        }

        if(!AssignedManager.IsWithinBounds(targetPos.x,targetPos.y)) return;
        ComponentBase targetComponent = AssignedManager.GetComponentAt(targetPos);

        if (targetComponent != null)
        {
            Debug.Log("TunnelInComponent: Target component found at " + targetPos);
            if (targetComponent.AcceptWord(HeldWord, GetOutputDirection(), GridPosition))
            {
                HeldWord = null;
                _netHeldWordId.Value = "";
                UpdateVisuals();
            }
        }
    }

}
