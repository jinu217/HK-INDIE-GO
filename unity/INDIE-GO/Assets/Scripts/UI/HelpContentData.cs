using UnityEngine;

namespace YutArena.UI
{
    [CreateAssetMenu(menuName = "UI/Help Content")]
    public sealed class HelpContentData : ScriptableObject
    {
        public Sprite yutRules;
        public Sprite characters;
        public Sprite maps;
        public Sprite modes;
    }
}
