using System;
using UnityEngine;

namespace YutArena.Common
{
    [Serializable]
    public class RoomSettingsData
    {
        public int gameMode;
        public int mapType;
        public int matchComposition;
        public int playerCount;
        public int maxTurnCount;
        public int gameTimeMinutes;
        public int turnTimeSeconds;
        public bool isTeamMode;
        public int[] playerTeams;

        public GameStartSettings ToGameStartSettings()
        {
            return new GameStartSettings
            {
                gameMode = (GameMode)gameMode,
                mapType = (MapType)mapType,
                matchComposition = (MatchComposition)matchComposition,
                isTeamMode = isTeamMode,
                playerTeams = playerTeams != null ? (int[])playerTeams.Clone() : null,
                playerCount = playerCount,
                pieceCountPerPlayer = GameRuleDefine.DefaultPieceCountPerPlayer,
                targetEscapeCount = GameRuleDefine.DefaultTargetEscapeCount,
                timeLimitMinutes = gameTimeMinutes,
                maxTurnCount = maxTurnCount,
                turnTimeMode = TurnTimeMode.Limited,
                throwTimeSeconds = turnTimeSeconds,
                actionTimeSeconds = turnTimeSeconds,
                useSkill = true,
                useItem = true,
                useSpecialTile = true
            };
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static RoomSettingsData FromJson(string json)
        {
            return JsonUtility.FromJson<RoomSettingsData>(json);
        }
    }
}
