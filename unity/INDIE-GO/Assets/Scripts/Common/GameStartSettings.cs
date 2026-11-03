using System;

namespace YutArena.Common
{
    public enum TeamMode
    {
        TeamMode1 = 0, // 2인1팀
        TeamMode2 = 1  // 2대2
    }

    public enum GameMode
    {
        Classic,
        Escape,
        KillTheKing
    }

    public enum MapType
    {
        Random = -1,
        Basic = 0,
        Grassland = 1,
        Korean = 2
    }

    public enum TurnTimeMode
    {
        Limited,
        Unlimited
    }

    [Serializable]
    public class GameStartSettings
    {
        public GameMode gameMode;
        public MapType mapType;
        public MatchComposition matchComposition;
        public bool isTeamMode;
        // isTeamMode가 true일 때 적용하는 팀전 세부 모드
        public TeamMode teamMode = TeamMode.TeamMode1;
        public int[] playerTeams;

        public int playerCount;
        public int pieceCountPerPlayer;

        public int targetEscapeCount;
        public int timeLimitMinutes;
        public int maxTurnCount;

        public TurnTimeMode turnTimeMode;

        public int throwTimeSeconds;
        public int actionTimeSeconds;

        public bool useSkill;
        public bool useItem;
        public bool useSpecialTile;
    }
}
