using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.Online
{
    public sealed class OnlineRoomRowView : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text compositionText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Image background;
        public void Bind(OnlineRoomInfo room, bool selected, Action select)
        {
            nameText.text = room.RoomName;
            modeText.text = OnlineLobbyLabels.Mode(room.GameMode);
            compositionText.text = room.IsTeamMode == true ? "팀전" : room.IsTeamMode == false ? "개인전" : "—";
            countText.text = $"{room.PlayerCount}/{room.Capacity}" + (room.IsOpen ? "" : " · 입장 불가");
            background.color = selected ? new Color32(220, 233, 244, 255) : new Color32(255, 252, 245, 255);
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => select());
        }
    }
}
