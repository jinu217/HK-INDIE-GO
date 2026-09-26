using UnityEngine;

namespace YutArena.InGame
{
    public sealed class FixedFootholdCatalog : ScriptableObject
    {
        public const string ResourcePath = "FixedFootholdCatalog";

        [SerializeField, HideInInspector] private GameObject[] playerFootholds;

        public GameObject GetForPlayer(int playerId)
        {
            int index = playerId - 1;
            return playerFootholds != null && index >= 0 && index < playerFootholds.Length
                ? playerFootholds[index]
                : null;
        }
    }
}
