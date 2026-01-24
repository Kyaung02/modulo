using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Settings")]
    public float moveSmoothTime = 0.3f;
    public float zoomSmoothTime = 0.3f;
    
    private Camera _cam;
    private Vector3 _targetPosition;
    private float _targetSize;
    
    // Velocity references for SmoothDamp
    private Vector3 _moveVelocity;
    private float _zoomVelocity;
    
    public bool IsTransitioning { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        _cam = GetComponent<Camera>();
        
        // Init target to current
        if (_cam != null)
        {
            _targetPosition = transform.position;
            _targetSize = _cam.orthographicSize;
        }
    }

    private void Update()
    {
        if (_cam == null) return;
        
        // If transitioning manually (via Coroutine), SmoothDamp will follow the target set by coroutine
        // The coroutine sets _targetPosition and _targetSize, so SmoothDamp will smoothly animate to them
        
        // Smooth Move
        float dist = Vector3.Distance(transform.position, _targetPosition);
        if (dist > 100f) // If distance is huge (teleport), snap immediately
        {
            transform.position = _targetPosition;
            _moveVelocity = Vector3.zero;
        }
        else
        {
            // Use faster smoothing during transitions for more responsive feel
            float smoothTime = IsTransitioning ? moveSmoothTime * 0.5f : moveSmoothTime;
            transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _moveVelocity, smoothTime);
        }
        
        // Smooth Zoom
        float zoomSmooth = IsTransitioning ? zoomSmoothTime * 0.5f : zoomSmoothTime;
        _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, _targetSize, ref _zoomVelocity, zoomSmooth);
    }

    // Immediate Teleport
    public void SetPositionByType(Vector3 pos, float size)
    {
        _targetPosition = pos;
        _targetSize = size;
        transform.position = pos;
        _cam.orthographicSize = size;
    }

    // Smooth Transition Task
    public void FocusOn(Vector3 pos, float size)
    {
        _targetPosition = pos;
        _targetSize = size;
    }

    [Header("Transition Settings")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float transitionDuration = 1.0f;

    // The fancy enter sequence
    public IEnumerator TransitionEnterModule(Vector3 moduleWorldPos, Vector3 innerWorldPos, System.Action onComplete)
    {
        IsTransitioning = true;
        
        Vector3 startPos = transform.position;
        float startSize = _cam.orthographicSize;
        
        // Target: Zoom in on the module in the OUTER world
        // Match Ratio: 
        // 1. Inner World is 7x7 units wide.
        // 2. Outer World Module Window is 0.95x0.95 units wide.
        // To make them look identical, we zoom such that 0.95 units in Outer World
        // fills the same screen percentage as 7 units in Inner World.
        // Scale Factor = 0.95 / 7.0
        
        float finalZoom = 5.0f;
        float ratio = 0.95f / 7f; 
        float targetPreZoom = finalZoom * ratio; 
        
        Vector3 targetPos = new Vector3(moduleWorldPos.x, moduleWorldPos.y, transform.position.z);
        
        float timer = 0f;
        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;
            float curveValue = transitionCurve.Evaluate(t);
            
            transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
            _cam.orthographicSize = Mathf.Lerp(startSize, targetPreZoom, curveValue);
            
            // Sync internal targets to prevent SmoothDamp interference in Update
            _targetPosition = transform.position;
            _targetSize = _cam.orthographicSize;
            
            yield return null;
        }

        // Final Snap to ensure we hit exact values
        transform.position = targetPos;
        _cam.orthographicSize = targetPreZoom;
        
        // 2. Teleport to Inner World (Context Switch)
        // Since we are now visually matching the inner world scale, this switch is seamless.
        transform.position = new Vector3(innerWorldPos.x, innerWorldPos.y, transform.position.z);
        _cam.orthographicSize = finalZoom;

        // Reset targets for Update loop
        _targetPosition = transform.position;
        _targetSize = _cam.orthographicSize;

        yield return null; 
        
        IsTransitioning = false;
        onComplete?.Invoke();
    }

    // The fancy exit sequence
    public IEnumerator TransitionExitModule(Vector3 moduleWorldPos, Vector3 parentCenterPos, System.Action onComplete)
    {
        IsTransitioning = true;

        // Reverse logic of Enter:
        // 1. Current State: Viewing Inner World (Zoom 5.0)
        // 2. Teleport to Outer World (Module Position) immediately with "ratio matched" size
        
        float finalZoom = 5.0f; // Standard view zoom
        float ratio = 0.95f / 7f; 
        float startSmallZoom = finalZoom * ratio; 
        
        // Teleport to the Module first (Visual continuity form inner world)
        transform.position = new Vector3(moduleWorldPos.x, moduleWorldPos.y, transform.position.z);
        _cam.orthographicSize = startSmallZoom;
        
        // 3. Animate OUT to standard view AND Pan to Parent Center
        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(parentCenterPos.x, parentCenterPos.y, transform.position.z);
        
        float timer = 0f;
        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;
            float curveValue = transitionCurve.Evaluate(t);
            
            transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
            _cam.orthographicSize = Mathf.Lerp(startSmallZoom, finalZoom, curveValue);
            
            // Sync internal targets
            _targetPosition = transform.position;
            _targetSize = _cam.orthographicSize;
            
            yield return null;
        }
        
        // Final Snap
        transform.position = targetPos;
        _cam.orthographicSize = finalZoom;
        
        _targetPosition = transform.position;
        _targetSize = _cam.orthographicSize;
        
        IsTransitioning = false;
        onComplete?.Invoke();
    }
}
