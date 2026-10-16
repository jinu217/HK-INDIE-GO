namespace YutArena.Common
{
    public static class GameRuleDefine
    {
        public const int DemoMaxPlayerCount = 4;
        public const int FutureMaxPlayerCount = 8;

        public const int MinDemoPlayerCount = 2;
        public const int MaxDemoPlayerCount = 4;

        public const int DefaultPlayerCount = 4;
        public const int DefaultPieceCountPerPlayer = 4;
        public const int MinPieceCountPerPlayer = 3;
        public const int MaxPieceCountPerPlayer = 5;

        // 이스케이프 필요 점수 (기획 10/17: 기본 5점, 최소 2 최대 10)
        public const int DefaultTargetEscapeCount = 5;
        public const int MinTargetEscapeCount = 2;
        public const int MaxTargetEscapeCount = 10;

        // 턴 수 = 전원 1바퀴(라운드) 기준 (기획 10/17: 기본 15턴, 최소 10 최대 25)
        public const int MinMaxTurnCount = 10;
        public const int DefaultMaxTurnCount = 15;
        public const int MaxMaxTurnCount = 25;

        public const int UnlimitedTimeMinutes = 0;
        public const int DefaultTimeLimitMinutes = 15;
        public const int MinTimeLimitMinutes = 1;
        public const int MaxTimeLimitMinutes = 60;

        // 턴 시간 (기획 10/17: 기본 30초, 최소 10 최대 60)
        public const int DefaultThrowTimeSeconds = 30;
        public const int MinTurnTimeSeconds = 10;
        public const int MaxTurnTimeSeconds = 60;
        public const int DefaultActionTimeSeconds = 30; // 사용 x

        public const int ExtraThrowTimeBonusSeconds = 15;
        public const int ExtraMoveTimeBonusSeconds = 10; // 사용 x

        public const int MaxYutMoExtraThrowCount = 3;
    }
}