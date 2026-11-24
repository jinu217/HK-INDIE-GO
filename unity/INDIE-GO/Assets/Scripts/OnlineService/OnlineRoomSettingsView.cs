using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.Online
{
    public sealed class OnlineRoomSettingsView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text startButtonLabel;
        [SerializeField] private GameObject[] localOnlyObjects;
        [SerializeField] private Behaviour[] localOnlyBehaviours;
        private OnlineSessionController session;

        private void Start()
        {
            session = OnlineSessionController.Instance;
            if (session == null || !session.IsConfiguringRoom) { gameObject.SetActive(false); return; }
            foreach (var item in localOnlyObjects) if (item != null) item.SetActive(false);
            foreach (var item in localOnlyBehaviours) if (item != null) item.enabled = false;
            if (startButtonLabel != null)
            {
                startButtonLabel.font = roomNameInput.textComponent.font;
                startButtonLabel.fontSharedMaterial = roomNameInput.textComponent.font.material;
                startButtonLabel.text = "설정 완료";
                startButtonLabel.ForceMeshUpdate();
                var startButton = startButtonLabel.GetComponentInParent<UnityEngine.UI.Button>();
                if (startButton != null)
                {
                    // Keep the baked-in "게임 시작" on the scene background covered while disabled.
                    var colors = startButton.colors;
                    var disabledColor = colors.disabledColor;
                    disabledColor.a = 1f;
                    colors.disabledColor = disabledColor;
                    startButton.colors = colors;
                }
            }
            roomNameInput.SetTextWithoutNotify(session.RoomName);
            nicknameInput.SetTextWithoutNotify(session.Nickname);
            roomNameInput.onValueChanged.AddListener(SetRoomName);
            nicknameInput.onValueChanged.AddListener(SetNickname);
            session.Changed += Refresh;
            Refresh();
        }

        private void SetRoomName(string value) => session.RoomName = value;
        private void SetNickname(string value) => session.Nickname = value;
        private void Refresh()
        {
            statusText.text = session.Status;
            roomNameInput.interactable = nicknameInput.interactable = !session.Busy;
        }
        private void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
        }
    }
}
