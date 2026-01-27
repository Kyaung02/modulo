using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    public class PlayerListItem : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI nameText;
        public Button moveButton;
        public Image colorIcon;
        
        private ulong _targetViewId;
        
        public void Setup(string playerName, ulong currentViewId, bool isSelf, Color playerColor)
        {
            if (nameText) nameText.text = isSelf ? $"{playerName} (You)" : playerName;
            if (colorIcon) colorIcon.color = playerColor;
            
            _targetViewId = currentViewId;
            
            if (moveButton)
            {
                // Can't spectate yourself or if you are already there? 
                // Allowing self-spectate is basically "Center Camera on my work", which is useful too.
                moveButton.onClick.RemoveAllListeners();
                moveButton.onClick.AddListener(OnMoveClicked);
                
                if (GetComponentInChildren<TextMeshProUGUI>())
                {
                     // Update button text logic if needed
                }
            }
        }
        
        private void OnMoveClicked()
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.Spectate(_targetViewId);
            }
        }
    }
}
