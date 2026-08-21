namespace YutArena.Common
{
    public static class CharacterSelectionResult
    {
        public const int NoCharacterSelected = -1;

        private static int[] selectedCharacterIds;
        private static CharacterData[] selectedCharacterData;

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

        public static bool TryGetCharacterData(int playerIndex, out CharacterData characterData)
        {
            characterData = null;

            if (selectedCharacterData == null || playerIndex < 0 || playerIndex >= selectedCharacterData.Length)
            {
                return false;
            }

            characterData = selectedCharacterData[playerIndex];
            return characterData != null;
        }

        public static void Set(int[] characterIds, int playerCount)
        {
            selectedCharacterIds = characterIds == null ? null : (int[])characterIds.Clone();
            selectedCharacterData = null;
            PlayerCount = selectedCharacterIds == null ? 0 : playerCount;
        }

        public static void Set(int[] characterIds, CharacterData[] characterData, int playerCount)
        {
            Set(characterIds, playerCount);
            selectedCharacterData = characterData == null ? null : (CharacterData[])characterData.Clone();
        }

        public static void Clear()
        {
            selectedCharacterIds = null;
            selectedCharacterData = null;
            PlayerCount = 0;
        }
    }
}
