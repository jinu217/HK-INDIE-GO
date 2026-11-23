using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.Online
{
    public sealed class OnlineTitleMenuView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button createButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button reconnectButton;
        [SerializeField] private Button backButton;
        private OnlineSessionController session;

        private void OnEnable()
        {
            session = OnlineSessionController.Instance;
            if (session == null) return;
            session.Changed += Refresh;
            reconnectButton.onClick.AddListener(session.EnterOnline);
            backButton.onClick.AddListener(Close);
            Refresh();
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.Changed -= Refresh;
            reconnectButton.onClick.RemoveListener(session.EnterOnline);
            backButton.onClick.RemoveListener(Close);
        }

        private void Close() => gameObject.SetActive(false);
        private void Refresh()
        {
            statusText.text = session.Status;
            bool available = !session.Busy;
            createButton.interactable = available;
            joinButton.interactable = available;
            reconnectButton.interactable = available;
            reconnectButton.gameObject.SetActive(session.Service.State == OnlineState.Offline);
        }
    }
}
