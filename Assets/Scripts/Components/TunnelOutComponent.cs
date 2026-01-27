using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;

public class TunnelOutComponent : ComponentBase
{
    //1. 준게 TunnelIn인가?
    //방향 및 가까운거 확인은 TunnelIn에서 처리
    public override bool AcceptWord(WordData word, Vector2Int direction, Vector2Int targetPos)
    {
        if (!IsServer) return false;
        if (HeldWord != null)return false;
        Debug.Log("TunnelOutComponent: Target component found at " + targetPos);
        if(AssignedManager.GetComponentAt(targetPos) is not TunnelInComponent) return false;
        
        base.AcceptWord(word, direction, targetPos);
        return true;
    }
}
