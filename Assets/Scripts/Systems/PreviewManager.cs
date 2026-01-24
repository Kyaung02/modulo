using UnityEngine;
using UnityEngine.InputSystem;

public class PreviewManager : MonoBehaviour
{
    private GameObject _previewObject;
    private ComponentBase _currentPreviewPrefab;
    private int _lastRotationIndex = -1;
    public void UpdatePreview(BuildManager buildManager)
    {
        if(buildManager==null)return;
        if (buildManager._mainCamera == null) return;
        
        // 1. Check if we have a selection
        if (buildManager.selectedComponentPrefab == null)
        {
            ClearPreview();
            return;
        }

        // 2. Re-create preview if selection changed
        if (_currentPreviewPrefab != buildManager.selectedComponentPrefab)
        {
            ClearPreview();
            
            _previewObject = Instantiate(buildManager.selectedComponentPrefab.gameObject, Vector3.zero, Quaternion.identity);
            _currentPreviewPrefab = buildManager.selectedComponentPrefab;
            _lastRotationIndex = -1; // Force rotation update

            // Disable logic components so it doesn't register to managers
            var components = _previewObject.GetComponentsInChildren<ComponentBase>();
            foreach (var comp in components) comp.enabled = false;
        }

        // 3. Update Position & Rotation
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = buildManager._mainCamera.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridPos = buildManager.activeManager.WorldToGridPosition(mouseWorldPos);

        if (buildManager.activeManager.IsWithinBounds(gridPos.x, gridPos.y))
        {
            _previewObject.SetActive(true);
            Vector3 worldPos = buildManager.activeManager.GridToWorldPosition(gridPos.x, gridPos.y);
            _previewObject.transform.position = worldPos;

            // Update Rotation if needed
            if (_lastRotationIndex != buildManager._currentRotationIndex)
            {
                _previewObject.transform.rotation = Quaternion.identity;
                for (int i = 0; i < buildManager._currentRotationIndex; i++)
                {
                    _previewObject.transform.Rotate(0, 0, -90);
                }
                _lastRotationIndex = buildManager._currentRotationIndex;
            }

            // Update Color based on Occupancy
            if (buildManager.CheckCollision(_currentPreviewPrefab, gridPos))
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
