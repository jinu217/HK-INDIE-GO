using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    /// <summary>
    /// InGameScene에서 Lobby가 전달한 시작 설정을 Console과 Inspector로 확인하는 임시 디버그 컴포넌트입니다.
    /// </summary>
    public class LobbySettingsDebugViewer : MonoBehaviour
    {
        [Header("Lobby Settings Debug")]
        [SerializeField, TextArea(3, 12)] private string currentSettingsJson;

        private void Start()
        {
            RefreshSettingsView();
        }

        [ContextMenu("Refresh Lobby Settings")]
        public void RefreshSettingsView()
        {
            RoomSettingsData roomSettings = GameStartSettingsHolder.CurrentRoomSettings;
            GameStartSettings gameSettings = GameStartSettingsHolder.Current;

            if (gameSettings == null)
            {
                currentSettingsJson = "GameStartSettingsHolder.Current is null";
                Debug.LogError("[LobbySettingsDebugViewer] Lobby 설정을 받지 못했습니다.", this);
                return;
            }

            currentSettingsJson = JsonUtility.ToJson(gameSettings, true);
            string roomSettingsJson = roomSettings != null
                ? roomSettings.ToJson()
                : "CurrentRoomSettings is null";

            Debug.Log(
                "[LobbySettingsDebugViewer] Lobby -> InGame settings received\n" +
                "RoomSettingsData: " + roomSettingsJson + "\n" +
                "GameStartSettings: " + currentSettingsJson,
                this);
        }
    }
}
