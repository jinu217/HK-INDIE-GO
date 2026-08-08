using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

namespace YutArena.Managers
{
    // 클래식 모드 승리 규칙: 이 팀 소속 모든 플레이어의 모든 말이 "골(Goal)" 상태여야 승리
    // [수정] 예전엔 escapeCountByTeam(완주 개수 카운터)을 pieceCountPerPlayer랑 숫자로만
    // 비교했는데, 영서 요청으로 "말들을 직접 훑어서 전부 Goal 상태인지" 확인하는 방식으로 변경함
    public class ClassicModeRule : MonoBehaviour, IGameModeRule
    {
        public GameMode Mode => GameMode.Classic; // "나는 Classic 모드 담당이다"라고 표시

        public bool CheckWin(TeamSlot team, Dictionary<TeamSlot, int> escapeCountByTeam,
            GameStartSettings settings, PlayerManager playerManager)
        {
            if (playerManager == null)
            {
                Debug.LogWarning("ClassicModeRule: playerManager가 없어서 승리 판정 불가");
                return false;
            }

            // 이 팀 소속 플레이어를 전부 찾아서
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                var playerSlot = (PlayerSlot)i;
                if (MatchCompositionRule.GetTeamSlot(settings.matchComposition, playerSlot) != team)
                    continue; // 이 팀 소속이 아니면 건너뜀

                if (!playerManager.TryGetPlayer(i, out var player))
                    continue; // 아직 세팅 안 된 플레이어면 건너뜀 (승리 아직 아님으로 취급됨)

                // 그 플레이어가 가진 말들을 전부 확인
                foreach (var piece in player.RuntimeData.Pieces)
                {
                    if (piece.State != PieceState.Goal)
                        return false; // 하나라도 아직 골 상태가 아니면 이 팀은 아직 승리 아님
                }
            }

            return true; // 이 팀 소속 모든 플레이어의 모든 말이 골 상태
        }
    }
}