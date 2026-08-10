namespace YutArena.Common
{
    public static class CharacterSelectionResult
    {
        public const int NoCharacterSelected = -1;

        private static int[] selectedCharacterIds;

        public static int PlayerCount { get; private set; }

        public static int[] GetSelectedCharacterIds()
        {
            return selectedCharacterIds == null ? null : (int[])selectedCharacterIds.Clone();
        }

        public static bool TryGetCharacterId(int playerIndex, out int characterId)
        {
            characterId = NoCharacterSelected;

            if (selectedCharacterIds == null || playerIndex < 0 || playerIndex >= selectedCharacterIds.Length)
            {
                return false;
            }

            characterId = selectedCharacterIds[playerIndex];
            return characterId != NoCharacterSelected;
        }

        public static void Set(int[] characterIds, int playerCount)
        {
            selectedCharacterIds = characterIds == null ? null : (int[])characterIds.Clone();
            PlayerCount = selectedCharacterIds == null ? 0 : playerCount;
        }

        public static void Clear()
        {
            selectedCharacterIds = null;
            PlayerCount = 0;
        }
    }
}
