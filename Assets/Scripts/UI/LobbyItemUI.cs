using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Multiplayer;

public class LobbyItemUI : MonoBehaviour
{
    public TextMeshProUGUI lobbyNameText;
    public TextMeshProUGUI playerCountText;
    public Button joinButton;

    private ISessionInfo session;
    private System.Action<ISessionInfo> onJoinAction;

    public void Initialize(ISessionInfo session, System.Action<ISessionInfo> onJoin)
    {
        this.session = session;
        this.onJoinAction = onJoin;

        lobbyNameText.text = session.Name;
        playerCountText.text = $"{session.MaxPlayers - session.AvailableSlots}/{session.MaxPlayers}";

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked()
    {
        onJoinAction?.Invoke(session);
    }
}
