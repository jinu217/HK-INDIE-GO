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
            if (modeNameText != null) modeNameText.text = entry != null ? entry.modeName : string.Empty;
            if (modeDescriptionText != null) modeDescriptionText.text = entry != null ? entry.description : string.Empty;
        }
    }
}
