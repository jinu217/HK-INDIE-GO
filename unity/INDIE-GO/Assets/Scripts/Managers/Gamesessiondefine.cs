using System;
using System.Collections.Generic;
using YutArena.Common;
using YutArena.Managers;
namespace YutArena.Managers.GameProgress
{
    // ===================================================================
    // 이 파일에 있는 3개 클래스는 원래 팀 Common 파일엔 없던 것들이라 내가 새로 추가함.
    // 이유: TurnDefine.cs의 TurnContext는 "현재 턴 하나의 스냅샷"만 담고 있어서
    //       "턴 순서 전체 목록"이나 "이번 턴에 던진 윷 결과들의 묶음" 같은 걸 담을 곳이 없었음.
    //       그래서 부족한 부분만 최소한으로 채워 넣은 것 (기존 파일은 손대지 않음)
    // ===================================================================

    // 지금 진행 중인 게임 세션(한 판) 전체를 나타내는 데이터
    // GameManager가 이 객체 하나를 들고 있으면서 관리함
    [Serializable]
    public class GameSessionData
    {
        public string sessionId;           // 이 게임 판을 구분하는 고유 id (GUID로 생성)
        public GamePhase phase;            // 지금 GamePhase의 어느 단계인지
        public GameStartSettings settings; // 대기실에서 정한 설정값 (모드/맵/인원 등) 그대로 보관
        public float elapsedSeconds;       // 게임 시작 후 지난 시간 (제한시간 모드에서 사용 예정)
    }

    // 턴이 어떤 순서로 도는지 관리하는 데이터
    // 예: Player1 -> Player2 -> Player3 -> Player4 -> 다시 Player1 ...
    [Serializable]
    public class TurnOrderData
    {
        public List<PlayerSlot> order = new List<PlayerSlot>(); // 턴 순서대로 정렬된 플레이어 목록
        public int currentIndex;                                 // order 리스트에서 지금 몇 번째 차례인지

        // 프로퍼티(=> 화살표 문법, "식 본문 프로퍼티"): 호출할 때마다 계산해서 반환하는 읽기 전용 값
        // Current 라고 쓰면 매번 이 로직이 실행되어서 "지금 순서인 플레이어"를 알려줌
        public PlayerSlot Current => (order.Count > 0 && currentIndex >= 0 && currentIndex < order.Count)
            ? order[currentIndex]   // 조건이 참이면(리스트가 있고, index가 범위 안이면) 그 자리 값 반환
            : PlayerSlot.None;      // 아니면 안전하게 None 반환 (배열 범위 밖 접근 에러 방지)
    }

    // 윷을 "한 번" 던진 기록 하나. 던질 때마다 이 객체가 하나씩 만들어짐
    // TurnManager가 이걸 리스트에 쌓아뒀다가(pendingResults), 플레이어가 원하는 순서로 골라서 씀
    [Serializable]
    public class YutThrowData
    {
        public PlayerSlot player;             // 누가 던졌는지
        public YutResult result;              // 결과가 뭐였는지 (도/개/걸/윷/모/뒷도/낙)
        public int throwIndexInTurn;          // 이번 턴에서 몇 번째로 던진 결과인지 (0, 1, 2...)
        public bool isBonusThrowFromCapture;  // 잡기로 얻은 보너스 던지기에서 나온 결과인지 여부
    }
}