using TMPro;
using UnityEngine;

namespace YutArena.UI
{
    public sealed class SettingsTheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Color textColor = new Color32(31, 50, 79, 255);
        public Color selectedTextColor = Color.white;
        public Color selectedBackgroundColor = new Color32(35, 78, 137, 255);
        public Color dropdownBackgroundColor = new Color32(250, 247, 239, 255);
        public Color disabledButtonTint = new Color32(231, 225, 211, 255);
        public Color disabledTextColor = new Color32(143, 138, 124, 255);
        [Tooltip("X: 작게, Y: 보통, Z: 크게")]
        public Vector3 previewTextSizes = new Vector3(22, 27, 32);
        public Texture2D titleWindow;
        public Texture2D gameWindow;
        public Texture2D confirmation;
        public Sprite sliderHandle;
        public Sprite toggleOn;
        public Sprite toggleOff;
        public Sprite dropdownArrow;
        public Sprite activeButton;
        public Sprite idleButton;
        public Sprite checkmark;
    }
}
