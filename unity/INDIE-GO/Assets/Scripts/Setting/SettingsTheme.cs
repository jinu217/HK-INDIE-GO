using TMPro;
using UnityEngine;

namespace YutArena.UI
{
    public sealed class SettingsTheme : ScriptableObject
    {
        public TMP_FontAsset font;
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
