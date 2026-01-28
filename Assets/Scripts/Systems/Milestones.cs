using UnityEngine;
using System;
using Unity.Netcode;
using UnityEngine.Events;

public class Milestones : MonoBehaviour
{
    public static Milestones Instance;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    //여기부터 달성 이벤트들
    public void DustUnlocked()
    {
        Debug.Log("Dust Unlocked");
        GoalManager.Instance.UnlockComponent(2);
        GoalManager.Instance.UnlockComponent(3);
    }
    public void SystemUnlocked()
    {
        Debug.Log("System Unlocked");
        
        if (GoalUI.Instance != null)
        {
             if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
             {
                 if (BuildManager.Instance != null) BuildManager.Instance.canTransformCollector = true;
                 GoalUI.Instance.ShowTutorialText("Press E on Collector");
             }
             else
             {
                 GoalUI.Instance.ShowTutorialText("Wait for Host to Upgrade Collector");
             }
        }
    }

    public void ModuleUnlocked()
    {
        Debug.Log("Module Unlocked");
        GoalManager.Instance.UnlockComponent(4);
    }

    public void CopyUnlocked()
    {
        Debug.Log("Copy Unlocked, nothing for now");
    }

    public void TunnelUnlocked()
    {
        Debug.Log("Tunnel Unlocked");
        GoalManager.Instance.UnlockComponent(5);
    }

    public void SourceUnlocked()
    {
        Debug.Log("Source Unlocked, nothing for now");
    }

    public void TeleportUnlocked()
    {
        Debug.Log("Teleport Unlocked, nothing for now");
    }

    public void GoldUnlocked()
    {
        Debug.Log("Gold Unlocked, nothing for now");
    }

    public void UpgradeUnlocked()
    {
        Debug.Log("Upgrade Unlocked, nothing for now");
    }

    public void StackUnlocked()
    {
        Debug.Log("Stack Unlocked, nothing for now");
    }

    public void EndedUnlocked()
    {
        Debug.Log("Ended Unlocked, nothing for now");
    }
}