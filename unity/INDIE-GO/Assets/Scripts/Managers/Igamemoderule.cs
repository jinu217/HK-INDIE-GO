using System.Collections.Generic;
using YutArena.Common;

namespace YutArena.Managers
{
    // 게임 모드마다 "승리 조건을 어떻게 판단하는지"가 다른데, 그 판단 로직을
    // WinConditionManager가 직접 다 갖고 있지 않고, 모드별로 이 규격을 따르는
    // 별도 스크립트(ClassicModeRule, EscapeModeRule 등)에 위임하기 위한 인터페이스.
    // 나중에 킬더킹 모드 추가할 때도 이 인터페이스만 구현하는 새 스크립트만 만들면 됨
    // (WinConditionManager나 다른 모드 룰 파일은 안 건드려도 됨)
    public interface IGameModeRule
    {
        // 이 룰이 어느 모드를 담당하는지
        GameMode Mode { get; }

        // Classic 모드는 "말들의 실제 상태(Goal인지)를 직접 확인"하는 방식
        bool CheckWin(TeamSlot team, Dictionary<TeamSlot, int> escapeCountByTeam,
            GameStartSettings settings, PlayerManager playerManager);
    }
}