using System;
using UnityEngine;

using Unity.Netcode;

public class TickManager : NetworkBehaviour
{
    public static TickManager Instance { get; private set; }

    [Header("Tick Settings")]
    // Synced Interval
    private NetworkVariable<float> _netTickInterval = new NetworkVariable<float>(0.5f);
    
    public float tickInterval 
    {
        get 
        {
            // Safety: If not networked/spawned yet, return default
            // This prevents crash in WordVisualizer.Awake/Update
            try 
            {
                if (IsSpawned) return _netTickInterval.Value;
            }
            catch {} 
            return 0.5f; 
        }
    }
    
    public bool isPaused = false;

    private float _timer;
    public event Action<long> OnTick; // Tick Count
    public long CurrentTick { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Only Server drives the tick
        if (!IsServer) return;

        if (isPaused) return;

        _timer += Time.deltaTime;

        if (_timer >= tickInterval)
        {
            _timer -= tickInterval;
            CurrentTick++;
            OnTick?.Invoke(CurrentTick);
            NotifyTickClientRpc(CurrentTick);
        }
    }
    
    [ClientRpc]
    private void NotifyTickClientRpc(long tick)
    {
        if (IsServer) return; // Avoid double invoke on Host
        CurrentTick = tick;
        OnTick?.Invoke(CurrentTick);
    }

    public void SetSpeed(float newInterval)
    {
        if (IsServer)
        {
            _netTickInterval.Value = Mathf.Max(0.01f, newInterval);
        }
    }
}
