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

        public const int DefaultTargetEscapeCount = 4;
        public const int MinTargetEscapeCount = 3;
        public const int MaxTargetEscapeCount = 5;

        public const int MinMaxTurnCount = 15;
        public const int DefaultMaxTurnCount = 20;
        public const int MaxMaxTurnCount = 25;

        public const int UnlimitedTimeMinutes = 0;
        public const int DefaultTimeLimitMinutes = 15;
        public const int MinTimeLimitMinutes = 1;
        public const int MaxTimeLimitMinutes = 60;

        public const int DefaultThrowTimeSeconds = 10;
        public const int DefaultActionTimeSeconds = 30;

        public const int ExtraThrowTimeBonusSeconds = 5;
        public const int ExtraMoveTimeBonusSeconds = 10;

        public const int MaxYutMoExtraThrowCount = 3;
    }
}