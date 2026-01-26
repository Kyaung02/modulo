using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WordVisualizer : MonoBehaviour
{
    private SpriteRenderer _renderer;
    


    private Vector3 _targetLocalPos = Vector3.zero;
    private Vector3 _startLocalPos = Vector3.zero;
    private float _animTimer = 0f;
    private float _animDuration = 0.5f; // Sync with TickManager if possible, or fixed speed
    private bool _isAnimating = false;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.enabled = false; 
        _renderer.sortingOrder = 10;
        
        // Auto-detect tick speed
        if (TickManager.Instance != null) _animDuration = TickManager.Instance.tickInterval;
    }

    public void UpdateVisual(WordData word, Vector2Int entryDir)
    {
        if (word == null)
        {
            _renderer.enabled = false;
            _isAnimating = false;
        }
        else
        {
            _renderer.enabled = true;
            _renderer.sprite = word.wordIcon;
            _renderer.color = Color.white; // No tint for emojis
            transform.localScale = Vector3.one * 0.6f;
            
            // Main component: "targetComponent.AcceptWord(HeldWord, GetOutputDirection(), ...)"
            // GetOutputDirection() is effectively "Move Dir".
            // So if Move Dir is Right (1,0), items moves Right.
            // Start Pos relative to center (0,0) should be Left (-1, 0).
            
            if (entryDir != Vector2Int.zero)
            {
                // Start exactly at the edge of the cell (-dir)
                // Assuming standard grid size 1.0. 
                // Adjust if pivot issues.
                _startLocalPos = new Vector3(-entryDir.x, -entryDir.y, 0f); // * cellSize? Visualizer is child of Component which is at 0,0 locally.
                _targetLocalPos = Vector3.zero; // Center of current cell
                
                transform.localPosition = _startLocalPos;
                _animTimer = 0f;
                _isAnimating = true;
                
                // Sync Speed
                if (TickManager.Instance != null) _animDuration = TickManager.Instance.tickInterval;
            }
            else
            {
                // Direct spawn (no anim)
                transform.localPosition = Vector3.zero;
                _isAnimating = false;
            }
        }
    }
    
    // Overload for simple update without animation reset
    public void UpdateVisual(WordData word)
    {
        UpdateVisual(word, Vector2Int.zero);
    }

    private void Update()
    {
        // Keep word upright regardless of parent rotation (world space)
        transform.rotation = Quaternion.identity;
        
        if (_isAnimating)
        {
            _animTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_animTimer / _animDuration);
            
            // Smooth step or Linear? Linear feels mechanical/belt-like.
            transform.localPosition = Vector3.Lerp(_startLocalPos, _targetLocalPos, t);
            
            if (t >= 1f)
            {
                _isAnimating = false;
            }
        }
    }
}
