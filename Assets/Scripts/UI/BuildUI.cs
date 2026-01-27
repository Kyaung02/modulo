using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BuildUI : MonoBehaviour
{
    [Header("Settings")]
    public GameObject slotPrefab; // Prefab with an Image and a Button/Background
    public Transform slotContainer; // Horizontal Layout Group parent
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color selectedColor = new Color(0.4f, 0.4f, 0.4f, 1.0f);
    public Color borderColor = new Color(0.8f, 0.8f, 0.8f);

    private List<Image> _slotImages = new List<Image>();
    private BuildManager _buildManager;

    private void Start()
    {
        _buildManager = FindFirstObjectByType<BuildManager>();
        
        if (_buildManager != null)
        {
            CreateSlots();
            _buildManager.OnComponentSelected += UpdateUI;
            // Select first one by default if not already
            UpdateUI(0); 
        }
    }

    private void CreateSlots()
    {
        // Clear existing
        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }
        _slotImages.Clear();

        if (_buildManager.availableComponents == null) return;

        for (int i = 0; i < _buildManager.availableComponents.Length; i++)
        {
            ComponentBase comp = _buildManager.availableComponents[i];
            
            if (comp == null) continue;
            if(comp.IsHidden() == 1) continue;

            GameObject slot = Instantiate(slotPrefab, slotContainer);
            
            // Setup Icon
            // Assuming component prefab has a child "Visual" with SpriteRenderer
            // or we grab SpriteRenderer from root if 1x1
            Sprite icon = null;
            SpriteRenderer sr = comp.GetComponentInChildren<SpriteRenderer>(); 
            // Note: Combiner has child Visual, SimpleBlock has root.
            
            if (sr != null) icon = sr.sprite;

            // Find Image component in slot meant for Icon (let's assume simple structure for now)
            // If prefab has Image component on root, use it as background. 
            // Child image as Icon.
            
            // Structure assumption: 
            // Slot (Image - Background)
            //  - Icon (Image)
            //  - Number (Text) - Optional
            
            // For this generated script, I will try to find a child named "Icon"
            Transform iconObj = slot.transform.Find("Icon");
            if (iconObj != null)
            {
                Image iconImg = iconObj.GetComponent<Image>();
                if (iconImg != null && icon != null)
                {
                    iconImg.sprite = icon;
                }
            }
            
            // Track the Background Image for highlighting
            _slotImages.Add(slot.GetComponent<Image>());
        }
    }

    private void Update()
    {
        // Check for Exit Input (Q)
        if (UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame)
        {
            _buildManager.ExitCurrentModule();
        }
    }

    private void UpdateUI(int selectedIndex)
    {
        for (int i = 0; i < _slotImages.Count; i++)
        {
            if (i == selectedIndex)
            {
                _slotImages[i].color = selectedColor;
                // Add border effect logic if separate component needed
            }
            else
            {
                _slotImages[i].color = normalColor;
            }
        }
    }
    
    private void OnDestroy()
    {
        if (_buildManager != null)
        {
            _buildManager.OnComponentSelected -= UpdateUI;
        }
    }
}
