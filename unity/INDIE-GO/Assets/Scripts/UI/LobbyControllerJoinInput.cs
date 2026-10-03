using TMPro;
using UnityEngine;
using YutArena.Common;

namespace YutArena.UI
{
    public class LobbyControllerJoinInput : MonoBehaviour
    {
        [Header("Player Texts")]
        [Tooltip("플레이어 텍스트")]
        [SerializeField] private TMP_Text[] playerTexts;

        [Header("Options")]
        [Tooltip("최대 플레이어 수")]
        [SerializeField] private int maxPlayers = 4;
        [Tooltip("키보드 플레이어")]
        [SerializeField] private bool showKeyboardPlayerOnStart = true;
        private readonly bool[] connectedPlayers = new bool[4];

        private void Awake()
        {
            maxPlayers = Mathf.Clamp(maxPlayers, 2, connectedPlayers.Length);

            connectedPlayers[0] = showKeyboardPlayerOnStart;
            SaveJoinState();
            RefreshPlayerTexts();
        }

        private void Update()
        {
            LocalSelectionInput.Poll(maxPlayers);
            for (int i = 0; i < maxPlayers; i++)
                connectedPlayers[i] = true;
            SaveJoinState();
            RefreshPlayerTexts();
        }

        private void RefreshPlayerTexts()
        {
            if (playerTexts == null)
            {
                return;
            }

            int count = Mathf.Min(playerTexts.Length, maxPlayers);

            for (int i = 0; i < count; i++)
            {
                TMP_Text playerText = playerTexts[i];

                if (playerText == null)
                {
                    continue;
                }

                playerText.text = (i + 1) + "P " +
                    (LocalSelectionInput.GetGamepad(i) != null ? "[Gamepad]" : "[Keyboard]");
                playerText.gameObject.SetActive(connectedPlayers[i]);
            }
        }

        private void SaveJoinState()
        {
            LocalPlayerJoinState.Clear();

            for (int i = 0; i < maxPlayers; i++)
            {
                LocalPlayerJoinState.SetJoined(i, connectedPlayers[i]);
            }
        }

    }

}
