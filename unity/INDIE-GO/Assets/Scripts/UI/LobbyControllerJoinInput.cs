using UnityEngine;
using UnityEngine.UI;
using YutArena.Common;

namespace YutArena.UI
{
    public class LobbyControllerJoinInput : MonoBehaviour
    {
        [Header("Player Controller Images")]
        [Tooltip("1PController ~ 4PController의 Image를 순서대로 연결")]
        [SerializeField] private Image[] playerControllerImages = new Image[4];
        [SerializeField] private Sprite keyboardSprite;
        [SerializeField] private Sprite padSprite;

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
            RefreshPlayerImages();
        }

        private void Update()
        {
            LocalSelectionInput.Poll(maxPlayers);
            for (int i = 0; i < maxPlayers; i++)
                connectedPlayers[i] = true;
            SaveJoinState();
            RefreshPlayerImages();
        }

        private void RefreshPlayerImages()
        {
            if (playerControllerImages == null)
            {
                return;
            }

            int count = Mathf.Min(playerControllerImages.Length, maxPlayers);

            for (int i = 0; i < count; i++)
            {
                Image image = playerControllerImages[i];

                if (image == null)
                {
                    continue;
                }

                bool usesPad = LocalSelectionInput.GetGamepad(i) != null ||
                    (LocalSelectionInput.SharedGamepadSelected && LocalSelectionInput.SharedInputAvailable);
                image.sprite = usesPad ? padSprite : keyboardSprite;
                image.enabled = image.sprite != null;
                image.gameObject.SetActive(connectedPlayers[i]);
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
