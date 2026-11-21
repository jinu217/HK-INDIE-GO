using UnityEngine;

namespace YutArena.UI
{
    [CreateAssetMenu(menuName = "UI/In Game HUD Theme")]
    public sealed class InGameHUDTheme : ScriptableObject
    {
        public Sprite skillPointFilled;
        public Sprite skillPointEmpty;
        public Color unfilledSkillPointColor = new Color(1f, 1f, 1f, .25f);
        public Sprite redTeamMarker;
        public Sprite blueTeamMarker;
        public Sprite[] unfinishedPieces;
        public Sprite[] finishedPieces;
    }
}
