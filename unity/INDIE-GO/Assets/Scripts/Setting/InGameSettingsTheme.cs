using TMPro;
using UnityEngine;

namespace YutArena.UI
{
    public sealed class InGameSettingsTheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Texture2D[] layers = new Texture2D[18];
    }
}
