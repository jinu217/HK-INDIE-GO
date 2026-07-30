using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    // 클래식 모드 승리 규칙: 그 팀이 가진 말 전체(pieceCountPerPlayer)가 다 완주하면 승리
    // IGameModeRule을 구현하는 것만 빼면 WinConditionManager 코드는 이 파일을 전혀 몰라도 됨
    public class ClassicModeRule : MonoBehaviour, IGameModeRule
    {
        public GameMode Mode => GameMode.Classic; // "나는 Classic 모드 담당이다"라고 표시

        public bool CheckWin(TeamSlot team, Dictionary<TeamSlot, int> escapeCountByTeam, GameStartSettings settings)
        {
            // 예전 WinConditionManager.CheckWinCondition() 안에 있던 Classic 분기 그대로 옮겨온 것
            return escapeCountByTeam.TryGetValue(team, out int count) &&
                   count >= settings.pieceCountPerPlayer;
        }
    }
}