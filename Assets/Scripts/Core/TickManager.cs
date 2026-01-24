using System;
using UnityEngine;

public class TickManager : MonoBehaviour
{
    public static TickManager Instance { get; private set; }

    [Header("Tick Settings")]
    public float tickInterval = 0.5f; // Seconds per tick
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
        if (isPaused) return;

        _timer += Time.deltaTime;

        if (_timer >= tickInterval)
        {
            _timer -= tickInterval;
            CurrentTick++;
            OnTick?.Invoke(CurrentTick);
        }
    }

    public void SetSpeed(float newInterval)
    {
        tickInterval = Mathf.Max(0.01f, newInterval);
    }
}
