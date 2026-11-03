using UnityEngine;
using YutArena.Common;

namespace YutArena.InGame
{
    /// <summary>
    /// 필요할 때만 로비 설정을 Inspector에서 확인하는 수동 도구입니다. 상시 상태는 QA 모니터에서 제공합니다.
    /// </summary>
    public class LobbySettingsDebugViewer : MonoBehaviour
    {
        [Header("Lobby Settings Debug")]
        [SerializeField, TextArea(3, 12)] private string currentSettingsJson;


        [ContextMenu("Refresh Lobby Settings")]
        public void RefreshSettingsView()
        {
            GameStartSettings gameSettings = GameStartSettingsHolder.Current;

            if (gameSettings == null)
            {
                currentSettingsJson = "GameStartSettingsHolder.Current is null";
                Debug.LogError("[LobbySettingsDebugViewer] Lobby 설정을 받지 못했습니다.", this);
                return;
            }

            currentSettingsJson = JsonUtility.ToJson(gameSettings, true);

        }
    }
}
