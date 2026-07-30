using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    // 이스케이프 모드 승리 규칙: 목표 탈출 수(targetEscapeCount)를 먼저 채우면 승리
    public class EscapeModeRule : MonoBehaviour, IGameModeRule
    {
        public GameMode Mode => GameMode.Escape; // "나는 Escape 모드 담당이다"라고 표시

        public bool CheckWin(TeamSlot team, Dictionary<TeamSlot, int> escapeCountByTeam, GameStartSettings settings)
        {
            // 예전 WinConditionManager.CheckWinCondition() 안에 있던 Escape 분기 그대로 옮겨온 것
            return escapeCountByTeam.TryGetValue(team, out int count) &&
                   count >= settings.targetEscapeCount;
            // TODO: 승리조건 2번(제한시간 내 더 많이 탈출)은 타이머 완성 후 별도 처리 필요
        }
    }
}