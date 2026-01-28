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
        Debug.Log($"[BlueprintUI] Start. Manager: {BlueprintManager.Instance != null}");
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
        if (contentRoot == null) { Debug.LogError("[BlueprintUI] ContentRoot is null!"); return; }
        if (blueprintItemPrefab == null) { Debug.LogError("[BlueprintUI] BlueprintItemPrefab is null!"); return; }
        
        // Clear old
        foreach(var obj in _spawnedItems) Destroy(obj);
        _spawnedItems.Clear();
        
        if (BlueprintManager.Instance == null) return;
        
        var list = BlueprintManager.Instance.blueprints;
        Debug.Log($"[BlueprintUI] Refreshing UI. Count: {list.Count}");
        
        for (int i = 0; i < list.Count; i++)
        {
            int index = i;
            var data = list[i];
            
            GameObject item = Instantiate(blueprintItemPrefab, contentRoot);
            item.SetActive(true); // Prefab is disabled, so we must enable usage
            _spawnedItems.Add(item);
            
            // Setup Text
            // Assuming prefab structure: Image (Icon), Text (Name), Button (Select)
            // Or simple setup: Root is Button. Image child.
            
            // NEW HIERARCHY:
            // Root -> SelectButton(Image) -> Icon
            // Root -> NameInput
            
            Transform selectBtnTr = item.transform.Find("SelectButton");
            Button btn = selectBtnTr?.GetComponent<Button>();
            Image bgImage = selectBtnTr?.GetComponent<Image>();
            
            // Icon is now inside SelectButton
            Image icon = selectBtnTr?.Find("Icon")?.GetComponent<Image>();
            if (icon != null && data.previewSprite != null) icon.sprite = data.previewSprite;
            
            // Setup Name Input
            InputField input = item.transform.Find("NameInput")?.GetComponent<InputField>();
            if (input != null)
            {
                input.text = data.name;
                input.onEndEdit.RemoveAllListeners(); // Safety
                input.onEndEdit.AddListener((val) => {
                    data.name = val;
                    // Debug.Log($"Renamed Blueprint {index} to {val}");
                });
            }
            
            // Highlight if selected
            // We pass the BACKGROUND IMAGE directly to helper, not the item logic
            if (bgImage != null)
                bgImage.color = (i == BlueprintManager.Instance.selectedBlueprintIndex) ? Color.yellow : Color.white;
            
            // Events
            if (btn != null)
            if (btn != null)
            {
                btn.onClick.AddListener(() => {
                    BlueprintManager.Instance.SelectBlueprint(index);
                });
            }
            
            // Right Click Logic (Needs specialized component or EventTrigger)
            // Attach to the SELECT BUTTON since it blocks raycasts
            if(selectBtnTr != null)
            {
                BlueprintItemInteract interact = selectBtnTr.GetComponent<BlueprintItemInteract>();
                if (interact == null) interact = selectBtnTr.gameObject.AddComponent<BlueprintItemInteract>();
                interact.onRightClick = () => {
                    BlueprintManager.Instance.DeleteBlueprint(index);
                };
            }
        }
    }
    
    private void OnSelectionChanged(int selectedIndex)
    {
        for(int i=0; i<_spawnedItems.Count; i++)
        {
            // Re-find bg image
            Transform btnTr = _spawnedItems[i].transform.Find("SelectButton");
            Image bg = btnTr?.GetComponent<Image>();
            if(bg != null) bg.color = (i == selectedIndex) ? Color.yellow : Color.white;
        }
    }
    
    // Legacy helper removed or repurposed
    // private void UpdateItemVisual...
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
