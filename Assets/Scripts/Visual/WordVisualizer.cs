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

    private Vector2Int _currentEntryDir;
    private Vector2Int _currentExitDir;

    public void UpdateVisual(WordData word, Vector2Int entryDir, Vector2Int exitDir)
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
            _renderer.color = Color.white; 
            transform.localScale = Vector3.one * 0.6f;
            
            _currentEntryDir = entryDir;
            _currentExitDir = exitDir;
            
            // Start Position logic
            // If just spawned (entryDir zero), start at center?
            // User requirement: "From prev block".
            
            if (entryDir == Vector2Int.zero && exitDir == Vector2Int.zero)
            {
                // Static
                transform.localPosition = Vector3.zero;
                _isAnimating = false;
                return;
            }

            // Sync Speed
            if (TickManager.Instance != null) _animDuration = TickManager.Instance.tickInterval;

            _animTimer = 0f;
            _isAnimating = true;
            
            // Set initial position immediately to avoid flickers
            transform.localPosition = CalculatePosition(0f);
        }
    }
    
    // Legacy overload
    public void UpdateVisual(WordData word)
    {
        UpdateVisual(word, Vector2Int.zero, Vector2Int.zero);
    }

    private void Update()
    {
        transform.rotation = Quaternion.identity;
        
        if (_isAnimating)
        {
            _animTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_animTimer / _animDuration);
            
            transform.localPosition = CalculatePosition(t);
            
            if (t >= 1f)
            {
                _isAnimating = false;
            }
        }
    }
    
    private Vector3 CalculatePosition(float t)
    {
        // Define Key Points (Local)
        // Cell size 1.0 assumed. Half extent 0.5.
        // Entry Dir is "Direction FROM neighbor". 
        // e.g. Neighbor on Left. EntryDir is (1, 0)? No. 
        // In ComponentBase, LastInputDir is WorldToLocal(entryDir).
        // If Neighbor is Left, World Dir is Right (1,0). Local Dir is Right (1,0) (if failed rot).
        // So `entryDir` points TO Center.
        // Start Point: `-entryDir * 0.5f`.
        
        Vector3 startPos = (_currentEntryDir != Vector2Int.zero) ? new Vector3(-_currentEntryDir.x, -_currentEntryDir.y, 0) * 0.5f : Vector3.zero;
        Vector3 endPos = (_currentExitDir != Vector2Int.zero) ? new Vector3(_currentExitDir.x, _currentExitDir.y, 0) * 0.5f : Vector3.zero;
        Vector3 centerPos = Vector3.zero;
        
        // Check for Straight path
        // If entry and exit are opposite: (1,0) and (1,0)?? 
        // If input is Right (1,0) [From Left], and output is Right (1,0) [To Right].
        // Then Start: (-0.5, 0). End: (0.5, 0).
        // They are parallel.
        
        bool isStraight = (_currentEntryDir == _currentExitDir) || (_currentEntryDir == Vector2Int.zero) || (_currentExitDir == Vector2Int.zero);
        
        if (isStraight)
        {
             return Vector3.Lerp(startPos, endPos, t);
        }
        else
        {
            // Curved path: Start -> Center -> End
            if (t < 0.5f)
            {
                // First half: Start to Center
                return Vector3.Lerp(startPos, centerPos, t * 2f);
            }
            else
            {
                // Second half: Center to End
                return Vector3.Lerp(centerPos, endPos, (t - 0.5f) * 2f);
            }
        }
    }
}
