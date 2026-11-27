using TMPro;
using UnityEngine;
using YutArena.Common;

namespace YutArena.UI
{
    public sealed class GameModeCardView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image modeImage;
        [SerializeField] private TMP_Text modeNameText;
        [SerializeField] private TMP_Text modeDescriptionText;

        public void Refresh(GameModeEntry entry)
        {
            if (modeImage != null)
            {
                modeImage.sprite = entry != null ? entry.image : null;
                modeImage.enabled = modeImage.sprite != null;
            }
            if (modeNameText != null)
            {
                modeNameText.text = entry == null ? string.Empty
                    : entry.gameMode == GameMode.Classic ? "클래식"
                    : entry.gameMode == GameMode.Escape ? "이스케이프"
                    : entry.gameMode == GameMode.KillTheKing ? "킬 더 킹"
                    : entry.modeName;
            }
            if (modeDescriptionText != null) modeDescriptionText.text = entry != null ? entry.description : string.Empty;
        }
    }
}
