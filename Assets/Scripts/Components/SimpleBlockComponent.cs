using UnityEngine;

public class SimpleBlockComponent : ComponentBase
{
    private SpriteRenderer _spriteRenderer;

    protected override void Start()
    {
        base.Start();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
    }

    protected override void OnTick(long tickCount)
    {
        // Simple visual feedback on tick
        if (tickCount % 2 == 0)
        {
            _spriteRenderer.color = Color.white;
        }
        else
        {
            _spriteRenderer.color = new Color(0.9f, 0.9f, 0.9f);
        }
    }
}
