using System;

namespace YutArena.Common
{
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