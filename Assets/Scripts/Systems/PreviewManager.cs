using UnityEngine;
using UnityEngine.InputSystem;

public class PreviewManager : MonoBehaviour
{
    private GameObject _previewObject;
    private ComponentBase _currentPreviewPrefab;
    private int _lastRotationIndex = -1;
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    public void UpdatePreview(ComponentBase selectedPrefab, int rotationIndex)
    {
        // 0. Safety Checks
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) { Debug.LogWarning("PreviewManager: MainCamera is missing."); return; }
        if (ModuleManager.Instance == null) { Debug.LogWarning("PreviewManager: ModuleManager is missing."); return; }

        // 1. Check if we have a selection
        if (selectedPrefab == null)
        {
            ClearPreview();
            return;
        }

        // 2. Re-create preview if selection changed
        if (_currentPreviewPrefab != selectedPrefab)
        {
            ClearPreview();
            
            _previewObject = Instantiate(selectedPrefab.gameObject, Vector3.zero, Quaternion.identity);
            _currentPreviewPrefab = selectedPrefab;
            _lastRotationIndex = -1; // Force rotation update

            // Disable logic components so it doesn't register to managers
            var components = _previewObject.GetComponentsInChildren<ComponentBase>();
            foreach (var comp in components) comp.enabled = false;
        }

        // 3. Update Position & Rotation
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = ModuleManager.Instance.WorldToGridPosition(mouseWorldPos);

        if (ModuleManager.Instance.IsWithinBounds(gridPos.x, gridPos.y))
        {
            _previewObject.SetActive(true);
            Vector3 worldPos = ModuleManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y);
            _previewObject.transform.position = worldPos;

            // Update Rotation if needed
            if (_lastRotationIndex != rotationIndex)
            {
                _previewObject.transform.rotation = Quaternion.identity;
                for (int i = 0; i < rotationIndex; i++)
                {
                    _previewObject.transform.Rotate(0, 0, -90);
                }
                _lastRotationIndex = rotationIndex;
            }

            // Update Color based on Occupancy
            // Use ModuleManager to check occupancy safely
            if (ModuleManager.Instance.GetComponentAt(gridPos) != null)
            {
                SetPreviewColor(new Color(1f, 0f, 0f, 0.5f)); // Red
            }
            else
            {
                SetPreviewColor(new Color(1f, 1f, 1f, 0.5f)); // White
            }
        }
        else
        {
            _previewObject.SetActive(false);
        }
    }

    private void ClearPreview()
    {
        if (_previewObject != null) Destroy(_previewObject);
        _previewObject = null;
        _currentPreviewPrefab = null;
    }

    private void SetPreviewColor(Color color)
    {
        if (_previewObject == null) return;
        var renderers = _previewObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers)
        {
            r.color = color;
        }
    }

    private void OnDisable()
    {
        ClearPreview();
    }
}
