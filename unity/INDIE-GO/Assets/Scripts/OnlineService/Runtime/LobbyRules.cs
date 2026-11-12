using System;
using System.Linq;
using YutArena.Common;

namespace YutArena.Online
{
    public static class LobbyRules
    {
        public const int MaxPlayers = 4;
        public static RoomSettingsData DefaultSettings(int playerCount = 4, bool teamMode = false) => new RoomSettingsData
        {
            gameMode = (int)GameMode.Classic, mapType = (int)MapType.Random,
            matchComposition = (int)(teamMode ? MatchComposition.TwoVsTwo :
                playerCount == 2 ? MatchComposition.OneVsOne : playerCount == 3 ? MatchComposition.OneVsOneVsOne : MatchComposition.OneVsOneVsOneVsOne),
            playerCount = playerCount, isTeamMode = teamMode, teamMode = TeamMode.TeamMode1,
            playerTeams = teamMode ? new[] { 1, 1, 2, 2 } : null,
            pieceCountPerPlayer = GameRuleDefine.DefaultPieceCountPerPlayer,
            targetEscapeCount = GameRuleDefine.DefaultTargetEscapeCount,
            maxTurnCount = GameRuleDefine.DefaultMaxTurnCount,
            turnTimeSeconds = GameRuleDefine.DefaultThrowTimeSeconds
        };

        public static RoomSettingsData CopySettings(RoomSettingsData s) => s == null ? null : new RoomSettingsData
        {
            gameMode = s.gameMode, mapType = s.mapType, matchComposition = s.matchComposition,
            playerCount = s.playerCount, pieceCountPerPlayer = s.pieceCountPerPlayer,
            targetEscapeCount = s.targetEscapeCount, maxTurnCount = s.maxTurnCount,
            turnTimeSeconds = s.turnTimeSeconds, isTeamMode = s.isTeamMode, teamMode = s.teamMode,
            playerTeams = s.playerTeams == null ? null : (int[])s.playerTeams.Clone()
        };

        public static RoomSettingsData ValidateSettings(RoomSettingsData source)
        {
            Require(source != null, "방 설정이 필요합니다.");
            var s = CopySettings(source);
            Require(s.playerCount >= 2 && s.playerCount <= MaxPlayers, "온라인 인원은 2~4명입니다.");
            Require(Enum.IsDefined(typeof(GameMode), s.gameMode), "지원하지 않는 게임 모드입니다.");
            Require(Enum.IsDefined(typeof(MapType), s.mapType), "지원하지 않는 맵입니다.");
            var composition = (MatchComposition)s.matchComposition;
            Require(composition >= MatchComposition.OneVsOne && composition <= MatchComposition.TwoVsTwo &&
                MatchCompositionRule.GetPlayerCount(composition) == s.playerCount, "대전 구성과 인원이 일치해야 합니다.");
            Require(s.isTeamMode == (composition == MatchComposition.TwoVsTwo), "팀전 여부와 대전 구성이 일치해야 합니다.");
            Require(Enum.IsDefined(typeof(TeamMode), s.teamMode), "지원하지 않는 팀전 진행 방식입니다.");
            Require(InRange(s.pieceCountPerPlayer, GameRuleDefine.MinPieceCountPerPlayer, GameRuleDefine.MaxPieceCountPerPlayer), "말 개수는 3~5개입니다.");
            Require(InRange(s.targetEscapeCount, GameRuleDefine.MinTargetEscapeCount, GameRuleDefine.MaxTargetEscapeCount), "탈출 목표는 2~10입니다.");
            Require(InRange(s.maxTurnCount, GameRuleDefine.MinMaxTurnCount, GameRuleDefine.MaxMaxTurnCount), "최대 턴은 10~25입니다.");
            Require(InRange(s.turnTimeSeconds, GameRuleDefine.MinTurnTimeSeconds, GameRuleDefine.MaxTurnTimeSeconds), "턴 시간은 10~60초입니다.");
            if (s.isTeamMode)
            {
                if (s.playerTeams == null) s.playerTeams = new[] { 1, 1, 2, 2 };
                Require(s.playerTeams.Length == 4 && s.playerTeams.All(t => t == 1 || t == 2), "팀 배정은 네 자리의 1 또는 2여야 합니다.");
                // Empty seats may temporarily have duplicated presets after a player changes teams.
                // Balance is checked against actual occupants by the authority and CanStart.
            }
            else s.playerTeams = null;
            return s;
        }

        public static string RoomName(string value) => Text(value, 40, "방 이름");
        public static string Nickname(string value) => Text(value, 20, "닉네임");
        internal static string SessionId(string value)
        {
            Require(Guid.TryParseExact(value, "N", out _), "올바른 방 식별자가 필요합니다.");
            return value;
        }
        internal static void Require(bool condition, string message)
        {
            if (!condition) throw new OnlineServiceException(OnlineErrorCode.InvalidRequest, message);
        }
        private static bool InRange(int value, int min, int max) => value >= min && value <= max;
        private static string Text(string value, int max, string label)
        {
            value = value?.Trim();
            Require(!string.IsNullOrEmpty(value) && value.Length <= max && !value.Any(char.IsControl), $"{label}은 공백·제어문자를 제외한 1~{max}자여야 합니다.");
            return value;
        }

        internal static void ValidateSnapshot(OnlineLobbySnapshot s, string sessionId)
        {
            Require(s != null && s.sessionId == sessionId && s.revision > 0, "대기실 식별자나 버전이 올바르지 않습니다.");
            RoomName(s.roomName);
            var settings = ValidateSettings(s.settings);
            Require(s.capacity == settings.playerCount && s.players != null && s.players.Length >= 1 && s.players.Length <= s.capacity, "대기실 인원이 올바르지 않습니다.");
            Require(s.players.All(p => p != null && p.connectionId > 0 && p.slot >= 1 && p.slot <= s.capacity), "참가자 식별자가 올바르지 않습니다.");
            Require(s.players.Select(p => p.connectionId).Distinct().Count() == s.players.Length &&
                s.players.Select(p => p.slot).Distinct().Count() == s.players.Length, "중복된 참가자가 있습니다.");
            Require(s.players.Count(p => p.isHost) == 1 && s.players.Single(p => p.isHost).isRegistered, "방장 정보가 올바르지 않습니다.");
            foreach (var p in s.players)
            {
                Nickname(p.nickname);
                Require(p.team == (settings.isTeamMode ? settings.playerTeams[p.slot - 1] : p.slot), "팀 배정이 일치하지 않습니다.");
            }
            Require(!settings.isTeamMode || (s.players.Count(p => p.team == 1) <= 2 && s.players.Count(p => p.team == 2) <= 2), "팀 정원을 초과했습니다.");
        }
    }
}
