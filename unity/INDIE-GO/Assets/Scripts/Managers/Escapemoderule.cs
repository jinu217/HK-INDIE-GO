using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    // 이스케이프 모드 승리 규칙: 목표 탈출 수(targetEscapeCount)를 먼저 채우면 승리
    // [수정] 인터페이스가 playerManager도 받게 바뀌었지만, Escape 모드는 여전히
    // escapeCountByTeam(숫자 카운트)만으로 충분해서 playerManager는 그냥 안 씀
    public class EscapeModeRule : MonoBehaviour, IGameModeRule
    {
        public GameMode Mode => GameMode.Escape; // "나는 Escape 모드 담당이다"라고 표시

        public bool CheckWin(TeamSlot team, Dictionary<TeamSlot, int> escapeCountByTeam,
            GameStartSettings settings, PlayerManager playerManager)
        {
            return escapeCountByTeam.TryGetValue(team, out int count) &&
                   count >= settings.targetEscapeCount;
            // TODO: 승리조건 2번(제한시간 내 더 많이 탈출)은 TestWinConditionManager.HandleTimeLimitReached()에서 별도 처리함
        }
    }
}