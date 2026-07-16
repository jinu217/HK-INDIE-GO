using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    public class TestYutRuleManager : MonoBehaviour
    {
        private static readonly (YutResult result, float weight)[] DefaultProbabilityTable =
        {
            (YutResult.Do,     10.79f),
            (YutResult.Gae,    33.89f),
            (YutResult.Geol,   35.49f),
            (YutResult.Yut,    13.94f),
            (YutResult.Mo,      2.29f),
            (YutResult.BackDo,  3.59f),
            (YutResult.Nak,     0.01f),
        };

        public System.Func<PlayerSlot, (YutResult, float)[]> ProbabilityTableProvider;

        public YutResult Throw(PlayerSlot player)
        {
            var table = ProbabilityTableProvider != null
                ? ProbabilityTableProvider(player)
                : DefaultProbabilityTable;

            if (table == null || table.Length == 0)
            {
                Debug.LogError("TestYutRuleManager: 확률표가 비어있음, 기본표로 대체");
                table = DefaultProbabilityTable;
            }

            float totalWeight = 0f;
            foreach (var entry in table) totalWeight += entry.Item2;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (var (result, weight) in table)
            {
                cumulative += weight;
                if (roll <= cumulative)
                    return result;
            }

            return table[table.Length - 1].Item1;
        }
    }
}