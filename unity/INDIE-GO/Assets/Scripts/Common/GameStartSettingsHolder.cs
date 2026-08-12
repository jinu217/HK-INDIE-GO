namespace YutArena.Common
{
    public static class GameStartSettingsHolder
    {
        public static GameStartSettings Current { get; set; }
        public static RoomSettingsData CurrentRoomSettings { get; set; }
    }

    public static class LocalPlayerJoinState
    {
        private const int MaxPlayerCount = 4;

        private static readonly bool[] joinedPlayers = new bool[MaxPlayerCount];

        public static int MaxPlayers
        {
            get { return MaxPlayerCount; }
        }

        public static int JoinedPlayerCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < joinedPlayers.Length; i++)
                {
                    if (joinedPlayers[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static void SetJoined(int playerIndex, bool joined)
        {
            if (!IsValidPlayerIndex(playerIndex))
            {
                return;
            }

            joinedPlayers[playerIndex] = joined;
        }

        public static bool IsJoined(int playerIndex)
        {
            return IsValidPlayerIndex(playerIndex) && joinedPlayers[playerIndex];
        }

        public static bool[] GetJoinedPlayers()
        {
            bool[] copy = new bool[joinedPlayers.Length];

            for (int i = 0; i < joinedPlayers.Length; i++)
            {
                copy[i] = joinedPlayers[i];
            }

            return copy;
        }

        public static void Clear()
        {
            for (int i = 0; i < joinedPlayers.Length; i++)
            {
                joinedPlayers[i] = false;
            }
        }

        private static bool IsValidPlayerIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < joinedPlayers.Length;
        }
    }
}
