using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class BlueprintUI : MonoBehaviour
{
    public GameObject blueprintItemPrefab; // Assign in Inspector
    public Transform contentRoot; // ScrollView Content
    
    // Cache
    private List<GameObject> _spawnedItems = new List<GameObject>();

    private void Start()
    {
        if (BlueprintManager.Instance != null)
        {
            BlueprintManager.Instance.OnBlueprintListChanged += RefreshUI;
            BlueprintManager.Instance.OnBlueprintSelected += OnSelectionChanged;
        }
        
        RefreshUI();
    }
    
    private void OnDestroy()
    {
        if (BlueprintManager.Instance != null)
        {
            BlueprintManager.Instance.OnBlueprintListChanged -= RefreshUI;
            BlueprintManager.Instance.OnBlueprintSelected -= OnSelectionChanged;
        }
    }

    private void RefreshUI()
    {
        if (contentRoot == null || blueprintItemPrefab == null) return;
        
        // Clear old
        foreach(var obj in _spawnedItems) Destroy(obj);
        _spawnedItems.Clear();
        
        if (BlueprintManager.Instance == null) return;
        
        var list = BlueprintManager.Instance.blueprints;
        for (int i = 0; i < list.Count; i++)
        {
            int index = i;
            var data = list[i];
            
            GameObject item = Instantiate(blueprintItemPrefab, contentRoot);
            _spawnedItems.Add(item);
            
            // Setup Text
            // Assuming prefab structure: Image (Icon), Text (Name), Button (Select)
            // Or simple setup: Root is Button. Image child.
            
            Image icon = item.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && data.previewSprite != null) icon.sprite = data.previewSprite;
            
            // Highlight if selected
            UpdateItemVisual(item, i == BlueprintManager.Instance.selectedBlueprintIndex);
            
            // Events
            Button btn = item.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => {
                    BlueprintManager.Instance.SelectBlueprint(index);
                });
            }
            
            // Right Click Logic (Needs specialized component or EventTrigger)
            BlueprintItemInteract interact = item.GetComponent<BlueprintItemInteract>();
            if (interact == null) interact = item.AddComponent<BlueprintItemInteract>();
            interact.onRightClick = () => {
                BlueprintManager.Instance.DeleteBlueprint(index);
            };
        }
    }
    
    private void OnSelectionChanged(int selectedIndex)
    {
        for(int i=0; i<_spawnedItems.Count; i++)
        {
            UpdateItemVisual(_spawnedItems[i], i == selectedIndex);
        }
    }
    
    private void UpdateItemVisual(GameObject item, bool isSelected)
    {
        Image bg = item.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = isSelected ? Color.yellow : Color.white;
        }
    }
}

// Simple helper for Right Click
public class BlueprintItemInteract : MonoBehaviour, IPointerClickHandler
{
    public System.Action onRightClick;
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            onRightClick?.Invoke();
        }
    }
}
