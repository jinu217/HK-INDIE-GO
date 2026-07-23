using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    // 윷을 "던져서 무슨 결과가 나오는지"만 담당. 결과로 뭘 할지(이동, 추가던지기 등)는 TurnManager가 처리
    public class TestYutRuleManager : MonoBehaviour
    {
        // 기획서 확률표를 그대로 옮겨놓은 것
        // static readonly = 프로그램 켜져있는 동안 절대 안 바뀌는 고정값, 모든 곳에서 이 표 하나만 공유해서 씀
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

        // 캐릭터 패시브로 확률표를 통째로 바꿔야 할 때 쓰는 자리 (예: 사무라이 - 뒷도를 윷/모로 재분배)
        // 캐릭터 담장자가 함수를 만들어서 여기에 등록해두면 Throw()가 자동으로 그 확률표를 사용함
        // yutRuleManager.ProbabilityTableProvider = GetSamuraiTable; -> 예를들어 등록해야 되는 코드?
        public System.Func<PlayerSlot, (YutResult, float)[]> ProbabilityTableProvider;

        // 실제로 윷을 던지는 함수. TurnManager가 이 함수 하나만 호출해서 결과를 받아감
        public YutResult Throw(PlayerSlot player)
        {
            // 이 플레이어 전용 확률표가 등록돼 있으면 그걸 쓰고, 없으면 기본표를 씀
            var table = ProbabilityTableProvider != null
                ? ProbabilityTableProvider(player)
                : DefaultProbabilityTable;

            // 혹시 등록된 표가 비어있는 등 오류 발생시 일단 기본표로 실행하고 오류 메시지를 남김
            if (table == null || table.Length == 0)
            {
                Debug.LogError("TestYutRuleManager: 확률표가 비어있음, 기본표로 대체");
                table = DefaultProbabilityTable;
            }

            // 표에 있는 확률(weight)을 전부 더해서 총합을 구함 (보통 100)
            float totalWeight = 0f;
            foreach (var entry in table) totalWeight += entry.Item2;

            // 0~totalWeight 사이의 랜덤 숫자를 뽑고, 확률을 앞에서부터 누적해가며 그 랜덤 숫자를 처음 넘는 지점의 결과를 뽑음
            // 예: 도10.79, 개33.89일 때 roll이 5면 "도", roll이 20이면 "개", roll이 55이면 "걸"
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (var (result, weight) in table)
            {
                cumulative += weight;
                if (roll <= cumulative)
                    return result;
            }

            // 소수점 계산 오차로 아주 드물게 못 찾을 수 있을 것 같아서 마지막 결과 반환하게끔 설정
            return table[table.Length - 1].Item1;
        }
    }
}