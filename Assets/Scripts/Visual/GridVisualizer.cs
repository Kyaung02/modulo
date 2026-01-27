using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    [Header("Settings")]
    public Material lineMaterial; // Assign a simple color material (Standard or Unlit/Color)
    public int orderInLayer = -10; // Draw behind everything

    private void Start()
    {
        CreateBackground(); 
        CreateGridLines();
        CreateBorder();
    }

    private void CreateBackground()
    {
        // One big quad for the background
        GameObject bgInfo = new GameObject("GridBackground");
        bgInfo.transform.SetParent(transform);
        
        // Find Local Manager or Instance
        ModuleManager manager = GetComponent<ModuleManager>();
        if (manager == null) manager = ModuleManager.Instance;
        if (manager == null) return;

        // Size: 7x7
        int w = manager.width;
        int h = manager.height;
        
        // Position relies on local calculation relative to parent
        // Since transform.position is the parent's world position, setting localPosition is safer if bgInfo is child
        bgInfo.transform.localPosition = Vector3.zero; // Reset first
        
        // Calculate Center in Local Space
        // Origin is usually (-3.5, -3.5)
        Vector2 center = manager.originPosition + new Vector2(w * 0.5f * manager.cellSize, h * 0.5f * manager.cellSize);
        bgInfo.transform.localPosition = new Vector3(center.x, center.y, 1f); // z=1 to be behind lines(0) and items
        
        bgInfo.transform.localScale = new Vector3(w * manager.cellSize, h * manager.cellSize, 1);
        
        SpriteRenderer sr = bgInfo.AddComponent<SpriteRenderer>();
        // Create a 1x1 white sprite dynamically if none provided?
        // Or assume we assign standard assets. Let's create a texture.
        sr.sprite = CreateSolidSprite(Color.white);
        sr.color = ColorPalette.BackgroundColor;
        sr.sortingOrder = orderInLayer;
    }

    private void CreateGridLines()
    {
        // Try to find local manager first (Important for Recursive Modules)
        ModuleManager manager = GetComponent<ModuleManager>();
        if (manager == null) manager = ModuleManager.Instance;
        
        if (manager == null) return;

        // Use LineRenderer for grid lines
        GameObject linesObj = new GameObject("GridLines");
        linesObj.transform.SetParent(transform);
        linesObj.transform.localPosition = Vector3.zero; // Must be 0 relative to parent
        
        int w = manager.width;
        int h = manager.height;
        float cell = manager.cellSize;
        Vector2 origin = manager.originPosition;
        
        // Vertical Lines
        for (int x = 0; x <= w; x++)
        {
            DrawLine(linesObj.transform, 
                new Vector3(origin.x + x * cell, origin.y, 0),
                new Vector3(origin.x + x * cell, origin.y + h * cell, 0));
        }
        
        // Horizontal Lines
        for (int y = 0; y <= h; y++)
        {
            DrawLine(linesObj.transform, 
                new Vector3(origin.x, origin.y + y * cell, 0),
                new Vector3(origin.x + w * cell, origin.y + y * cell, 0));
        }
    }
    
    private void CreateBorder()
    {
        // TODO: Thicker border later. For now, lines cover it.
    }

    private void DrawLine(Transform parent, Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(parent);
        lineObj.transform.localPosition = Vector3.zero;
        lineObj.transform.localRotation = Quaternion.identity;
        
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; // Must be false so lines move with the Inner World logic
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        
        lr.startWidth = ColorPalette.GridLineWidth;
        lr.endWidth = ColorPalette.GridLineWidth;
        
        if (lineMaterial != null) lr.material = lineMaterial;
        lr.startColor = ColorPalette.GridLineColor;
        lr.endColor = ColorPalette.GridLineColor;
        
        lr.sortingOrder = orderInLayer + 1; // Above background
    }

    // Utility to create a white 1x1 sprite
    private Sprite CreateSolidSprite(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1.0f);
    }
}
