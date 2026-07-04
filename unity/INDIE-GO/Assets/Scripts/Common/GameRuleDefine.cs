namespace YutArena.Common
{
    // 여러 시스템이 함께 참조하는 기본 규칙 상수
    public static class GameRuleDefine
    {
        public const int DemoMaxPlayerCount = 4;            // 데모 버전에서 허용하는 최대 플레이어 수
        public const int FutureMaxPlayerCount = 8;          // 추후 확장 목표 최대 플레이어 수

        public const int MinDemoPlayerCount = 2;            // 데모 버전에서 허용하는 최소 플레이어 수
        public const int MaxDemoPlayerCount = 4;            // 데모 버전에서 허용하는 최대 플레이어 수

        public const int DefaultPlayerCount = 4;            // 기본 플레이어 수
        public const int DefaultPieceCountPerPlayer = 4;    // 기본 말 개수
        public const int MinPieceCountPerPlayer = 3;        // 플레이어당 최소 말 개수
        public const int MaxPieceCountPerPlayer = 5;        // 플레이어당 최대 말 개수

        public const int DefaultTargetEscapeCount = 4;      // Escape 모드 기본 목표 탈출 수
        public const int MinTargetEscapeCount = 3;          // Escape 모드 최소 목표 탈출 수
        public const int MaxTargetEscapeCount = 5;          // Escape 모드 최대 목표 탈출 수

        public const int UnlimitedTimeMinutes = 0;          // 제한 시간 없음 표시값
        public const int DefaultTimeLimitMinutes = 20;      // 기본 제한 시간
        public const int MinTimeLimitMinutes = 1;           // 최소 제한 시간
        public const int MaxTimeLimitMinutes = 60;          // 최대 제한 시간

        public const int DefaultThrowTimeSeconds = 10;      // 윷 던지기 기본 제한 시간
        public const int DefaultActionTimeSeconds = 30;     // 말 이동/스킬 사용 기본 제한 시간

        public const int ExtraThrowTimeBonusSeconds = 5;    // 추가 던지기 1회당 윷 던지기 시간 증가량
        public const int ExtraMoveTimeBonusSeconds = 10;    // 추가 이동 결과 1개당 행동 시간 증가량

        public const int MaxYutMoExtraThrowCount = 3;       // 윷/모로 얻을 수 있는 추가 던지기 최대 횟수
    }
}
