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
                timeLimitMinutes = GameRuleDefine.UnlimitedTimeMinutes,
                maxTurnCount = maxTurnCount,
                turnTimeMode = TurnTimeMode.Unlimited,
                throwTimeSeconds = GameRuleDefine.DefaultThrowTimeSeconds,
                actionTimeSeconds = GameRuleDefine.DefaultActionTimeSeconds,
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
