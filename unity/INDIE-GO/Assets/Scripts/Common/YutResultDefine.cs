namespace YutArena.Common
{
    // 윷 던지기 결과
    public enum YutResult
    {
        None = 999,  // 아직 윷 결과가 정해지지 않은 상태

        Nak = 0,        // 낙: 남은 행동/이동/추가 던지기를 모두 취소하고 즉시 턴 종료
        BackDo = -1,    // 백도: 1칸 후진

        Do = 1,         // 도: 1칸 전진
        Gae = 2,        // 개: 2칸 전진
        Geol = 3,       // 걸: 3칸 전진
        Yut = 4,        // 윷: 4칸 전진, 추가 던지기 획득
        Mo = 5          // 모: 5칸 전진, 추가 던지기 획득
    }

    // 윷 결과에 대한 공통 판정 함수 모음 (맵 종류마다/아이템 사용 시/스킬 사용 시 결과 값이 달라질 수 있으므로 윷 결과와 이동 칸 수는 따로 분리함)
    public static class YutResultRule
    {
        // 윷 결과가 몇 칸 이동을 의미하는지 반환
        public static int GetMoveCount(YutResult result)
        {
            return result switch
            {
                YutResult.BackDo => -1,
                YutResult.Nak => 0,
                YutResult.Do => 1,
                YutResult.Gae => 2,
                YutResult.Geol => 3,
                YutResult.Yut => 4,
                YutResult.Mo => 5,
                _ => 0
            };
        }

        // 윷 또는 모처럼 추가 던지기를 주는 결과인지 확인
        public static bool IsExtraThrowResult(YutResult result)
        {
            return result == YutResult.Yut || result == YutResult.Mo;
        }

        // 낙처럼 턴을 즉시 취소해야 하는 결과인지 확인
        public static bool IsTurnCancelResult(YutResult result)
        {
            return result == YutResult.Nak;
        }
    }
}
