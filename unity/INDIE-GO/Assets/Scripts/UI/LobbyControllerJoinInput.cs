using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YutArena.Common;

namespace YutArena.UI
{
    public class LobbyControllerJoinInput : MonoBehaviour
    {
        private const int FirstControllerPlayerIndex = 1;

        [Header("Player Texts")]
        [Tooltip("플레이어 텍스트")]
        [SerializeField] private TMP_Text[] playerTexts;

        [Header("Options")]
        [Tooltip("최대 플레이어 수")]
        [SerializeField] private int maxPlayers = 4;
        [Tooltip("키보드 플레이어")]
        [SerializeField] private bool showKeyboardPlayerOnStart = true;
        [Tooltip("Allow all local players to share the keyboard in this turn-based game.")]
        [SerializeField] private bool allowKeyboardHotseat = true;

        private readonly Gamepad[] assignedGamepads = new Gamepad[4];
        private readonly bool[] connectedPlayers = new bool[4];

        private void Awake()
        {
            maxPlayers = Mathf.Clamp(maxPlayers, 2, connectedPlayers.Length);

            for (int playerIndex = 0; playerIndex < maxPlayers; playerIndex++)
                connectedPlayers[playerIndex] = allowKeyboardHotseat ||
                                                (playerIndex == 0 && showKeyboardPlayerOnStart);
            SaveJoinState();
            RefreshPlayerTexts();
        }

        private void Update()
        {
            RemoveDisconnectedGamepads();
            CheckGamepadJoinInput();
            SaveJoinState();
            RefreshPlayerTexts();
        }

        private void CheckGamepadJoinInput()
        {
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad gamepad = Gamepad.all[i];

                if (gamepad == null || !gamepad.buttonSouth.wasPressedThisFrame || IsAssigned(gamepad))
                {
                    continue;
                }

                AssignNextPlayer(gamepad);
            }
        }

        private void AssignNextPlayer(Gamepad gamepad)
        {
            for (int playerIndex = FirstControllerPlayerIndex; playerIndex < maxPlayers; playerIndex++)
            {
                if (assignedGamepads[playerIndex] != null)
                {
                    continue;
                }

                connectedPlayers[playerIndex] = true;
                assignedGamepads[playerIndex] = gamepad;
                return;
            }
        }

        private void RemoveDisconnectedGamepads()
        {
            for (int playerIndex = FirstControllerPlayerIndex; playerIndex < maxPlayers; playerIndex++)
            {
                Gamepad gamepad = assignedGamepads[playerIndex];

                if (gamepad == null || IsConnected(gamepad))
                {
                    continue;
                }

                assignedGamepads[playerIndex] = null;
                connectedPlayers[playerIndex] = allowKeyboardHotseat;
            }
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

                playerText.text = (i + 1) + "P";
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

        private bool IsAssigned(Gamepad gamepad)
        {
            for (int i = FirstControllerPlayerIndex; i < maxPlayers; i++)
            {
                if (assignedGamepads[i] == gamepad)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsConnected(Gamepad gamepad)
        {
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                if (Gamepad.all[i] == gamepad)
                {
                    return true;
                }
            }

            return false;
        }
    }

}
