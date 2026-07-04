namespace YutArena.Common
{
    // 기본 윷판의 칸 ID
    public enum BoardTileId
    {
        None,       // 칸 없음 또는 아직 위치가 정해지지 않음

        Start,      // 참먹이(시작)

        Outer01,    // 도
        Outer02,    // 개
        Outer03,    // 걸
        Outer04,    // 윷
        Corner01,   // 모

        Outer05,    // 뒷도
        Outer06,    // 뒷개
        Outer07,    // 뒷걸
        Outer08,    // 뒷윷
        Corner02,   // 뒷모

        Outer09,    // 찌도
        Outer10,    // 찌개
        Outer11,    // 찌걸
        Outer12,    // 찌윷
        Corner03,   // 찌모

        Outer13,    // 날도
        Outer14,    // 날개
        Outer15,    // 날걸
        Outer16,    // 날윷
        Corner04,   // 날모

        Center,     // 방

        Inner01,    // 모도
        Inner02,    // 모개
        Inner03,    // 모걸
        Inner04,    // 속윷

        Inner05,    // 뒷모도
        Inner06,    // 뒷모개
        Inner07,    // 뒷모걸
        Inner08,    // 방수기

        Goal        // 완주(참먹이인데, 필요할거 같아서 따로 둠)
    }
}
