using TMPro;
using UnityEngine;

namespace YutArena.UI
{
    public sealed class InGameSettingsTheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Color textColor = new Color32(31, 46, 76, 255);
        public Color selectedTextColor = Color.white;
        [Tooltip("X: 작게, Y: 보통, Z: 크게")]
        public Vector3 previewTextSizes = new Vector3(27, 33, 39);
        public Texture2D windowBackground;
        public Texture2D closeIcon;
        public Texture2D selectedTab;
        public Texture2D footerButton;
        public Texture2D generalPreview;
        public Texture2D sizeIdle;
        public Texture2D sizeSelected;
        public Texture2D soundDividers;
        public Texture2D sliderEmpty;
        public Texture2D sliderFilled;
        public Texture2D sliderThumb;
        public Texture2D controlsTable;
        public Texture2D bindingIdle;
        public Texture2D bindingListening;
        public Texture2D confirmationScrim;
        public Texture2D confirmationFrame;
        public Texture2D confirmationCancel;
        public Texture2D confirmationAccept;
    }
}
