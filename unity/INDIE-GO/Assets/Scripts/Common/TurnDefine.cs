using System;

namespace YutArena.Common
{
    // 턴 진행 단계
    public enum TurnPhase
    {
        None,               // 턴 단계가 아직 정해지지 않음

        TurnStart,          // 턴 시작
        ApplyTurnStartRule, // 턴 시작 시 상태, 쿨타임, 제한 시간 등을 처리

        WaitThrow,          // 윷 던지기 입력을 기다리는 단계
        Throwing,           // 윷을 던지는 연출 또는 계산이 진행 중인 단계
        SaveThrowResult,    // 윷 결과를 저장하는 단계
        CheckExtraThrow,    // 윷/모로 인한 추가 던지기 여부를 확인하는 단계

        WaitAction,         // 말 이동 또는 스킬 사용 선택을 기다리는 단계
        SelectPiece,        // 이동할 말을 선택하는 단계
        MovePiece,          // 선택한 말을 이동시키는 단계
        ResolveTile,        // 도착한 칸의 효과를 처리하는 단계
        ResolveBoardRule,   // 잡기, 업기, 완주 같은 보드 규칙을 처리하는 단계
        CheckBonusThrow,    // 잡기로 얻은 추가 던지기 여부를 확인하는 단계

        TurnEnd,            // 현재 플레이어의 턴 종료
        GameEnd             // 게임 종료
    }

    // 현재 턴에서 공통으로 참조할 상태 데이터
    [Serializable]
    public class TurnContext
    {
        public int roundNumber;                 // 모든 플레이어가 한 번씩 턴을 진행한 횟수
        public int turnNumber;                  // 게임 시작 후 진행된 전체 턴 수

        public PlayerSlot currentPlayer;        // 현재 턴을 진행 중인 플레이어
        public TeamSlot currentTeam;            // 현재 턴 플레이어가 속한 팀
        public TurnPhase currentPhase;          // 현재 턴의 진행 단계

        public YutResult lastYutResult;         // 가장 최근에 나온 윷 결과

        public int extraThrowByYutMoCount;      // 윷/모 결과로 얻은 추가 던지기 횟수
        public int extraThrowByCaptureCount;    // 잡기 성공으로 얻은 추가 던지기 횟수

        public bool isTurnCanceledByNak;        // 낙으로 인해 현재 턴이 취소되었는지 여부
        public bool isGameEnded;                // 게임이 종료되었는지 여부
    }
}
