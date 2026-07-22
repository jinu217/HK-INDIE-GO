using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    // ===================================================================
    // 전체 흐름: 턴 시작 -> 윷 던지기(윷/모면 반복, 최대 3회) -> 결과 묶음 중 원하는 순서로 말 이동 -> 잡기 보너스 던지기(있으면) -> 턴 종료 -> 다음 플레이어
    // 아주 중요한 원칙:
    // 보드 좌표/다음칸/갈림길/잡기/업기/완주 "판정"은 절대 이 클래스에서 하지 않는다.
    // IBoardExecutor(말 이동 코드)에게 "이동해줘"라고 요청만 던지고, "이렇게 됐어"라는
    // 결과(BoardMoveResult)만 받아서 턴 흐름을 진행한다. 
    // ===================================================================
    public class TestTurnManager : MonoBehaviour
    {
        //inspector창에서 드래그 할 수 있는 칸 만들기
        [Header("Dependencies")]
        [SerializeField] private TestYutRuleManager yutRuleManager;
        [SerializeField] private TestWinConditionManager winConditionManager;
        [SerializeField] private MonoBehaviour boardExecutorSource;
        private IBoardExecutor boardExecutor;

        // 외부에서 볼 수 있지만 코드 수정은 내부에서 가능
        public TurnContext CurrentTurn { get; private set; } = new TurnContext();

        // 이번 턴에 던져서 "아직 이동에 쓰지 않은" 윷 결과들을 쌓아두는 리스트
        // 예: 윷-모-도 순서로 던졌다면 이 리스트에 [윷, 모, 도] 3개가 들어있다가 플레이어가 원하는 순서로 하나씩 골라서 꺼내 쓰게 됨
        private readonly List<YutThrowData> pendingResults = new List<YutThrowData>();

        // 잡기로 얻은 보너스 던지기 횟수 저장( 던진 윷 결과를 다 소모하고 보너스 던지기를 하니까)
        private int pendingCaptureThrows = 0;

        // 턴 순서 관리 데이터 (Common의 GameSessionDefine.cs에 새로 추가??)
        public TurnOrderData TurnOrder { get; private set; } = new TurnOrderData();

        private GameStartSettings settings;
        private int throwCountInTurn = 0; // 이번 턴에서 몇 번째 던지기인지 (YutThrowData.throwIndexInTurn)

        // ---- 외부(UI, GameManager 등)가 구독할 수 있는 이벤트들 ----
        public System.Action<TurnContext> OnTurnPhaseChanged;      // 턴 단계가 바뀔 때마다
        public System.Action<PlayerSlot> OnTurnStarted;            // 새 턴이 시작될 때
        public System.Action<PlayerSlot> OnTurnEnded;              // 턴이 끝날 때
        public System.Action<List<YutThrowData>> OnPendingResultsChanged; // 결과 묶음이 바뀔 때 (UI가 화면에 표시하려고 구독)

        // GameManager.StartGame()에서 호출됨. 게임 시작 전 준비 작업
        public void Initialize(GameStartSettings gameSettings)
        {
            settings = gameSettings;

            // boardExecutorSource가 실제로 IBoardExecutor를 구현하고 있는지 확인
            // "as" 형변환은 실패하면 예외를 던지지 않고 그냥 null을 돌려줌 (안전한 형변환)
            boardExecutor = boardExecutorSource as IBoardExecutor;
            if (boardExecutor == null)
            {
                Debug.LogError("TestTurnManager: boardExecutorSource가 IBoardExecutor를 구현하지 않음");
                return;
            }
            // 보드 말이 이동을 끝내면 HandleBoardMoveResolved가 자동으로 실행되도록 등록
            boardExecutor.OnMoveResolved += HandleBoardMoveResolved;

            // TODO: 지금은 임시로 4인 고정값. 실제로는 대기실에서 확정된 참가 플레이어 목록으로 채워야 함
            TurnOrder = new TurnOrderData
            {
                order = new List<PlayerSlot> { PlayerSlot.Player1, PlayerSlot.Player2, PlayerSlot.Player3, PlayerSlot.Player4 },
                currentIndex = 0
            };
            CurrentTurn = new TurnContext(); // 이전 정보가 남아있지 않도록 새로 초기화
        }

        //GameManager.StartGame()에서 Initialize() 다음에 호출, 진짜 첫 턴을 시작
        public void StartFirstTurn()
        {
            CurrentTurn.roundNumber = 1;
            CurrentTurn.turnNumber = 1;
            BeginTurnFor(TurnOrder.Current); // 지금 순서인 플레이어
        }

        // 특정 플레이어의 턴을 시작하는 내부 함수
        private void BeginTurnFor(PlayerSlot player)
        {
            CurrentTurn.currentPlayer = player;

            // settings.matchComposition에 "1vs1vs1vs1(개인전)"인지 "2vs2(팀전)"인지가 담겨있으므로
            // Common의 MatchCompositionRule을 그대로 써서 team을 계산함
            CurrentTurn.currentTeam = MatchCompositionRule.GetTeamSlot(settings.matchComposition, player);

            pendingResults.Clear();               // 던진 결과 묶음 비우기 (윷,모,도 같은 거 다 지움)
            pendingCaptureThrows = 0;             // 보너스 던지기 개수 0으로
            throwCountInTurn = 0;                 // 몇 번째 던지기인지 카운트 0으로
            CurrentTurn.extraThrowByYutMoCount = 0;    // 윷/모로 얻은 추가 던지기 횟수 0으로
            CurrentTurn.extraThrowByCaptureCount = 0;  // 잡기로 얻은 추가 던지기 횟수 0으로
            CurrentTurn.isTurnCanceledByNak = false;   // "낙 나와서 턴 취소됐음" 표시 해제

            // 지금 단계를 턴 시작으로 바꾸고 ui에게 알림
            SetPhase(TurnPhase.TurnStart);
            OnTurnStarted?.Invoke(player);

            //턴 시작 규칙(상태이상 적용, 스킬 쿨타임 등등 추가해야함), 코드 추가될 자리 표시함
            SetPhase(TurnPhase.ApplyTurnStartRule);

            //윷 던지기 기다림
            SetPhase(TurnPhase.WaitThrow);
        }

        // ===================================================================
        // UI의 [던지기] 버튼을 눌렀을 때 호출하는 함수.
        // 윷/모가 나오면 (최대 3회까지) 이 함수가 다시 호출되는 걸 UI가 반복해서 처리하게 되고,
        // 그 사이엔 말 이동 없이 결과만 계속 리스트인 pendingResults에 쌓인다.
        // ===================================================================
        public void RequestThrow()
        {   // 지금 윷을 던지는 단계가 아닌데 UI버튼을 누르면 경고 문자를 출력하고 넘김
            if (CurrentTurn.currentPhase != TurnPhase.WaitThrow)
            {
                Debug.LogWarning("지금은 윷을 던질 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }
            // 보너스 던지기인지 미리 확인해두고 → 던지는 중이라고 표시
            // -> 진짜로 던져서 결과 받고 → 그 결과를 저장하고 → 저장 완료 단계로 표시
            bool isCaptureBonusThrow = pendingCaptureThrows > 0;

            SetPhase(TurnPhase.Throwing);
            YutResult result = yutRuleManager.Throw(CurrentTurn.currentPlayer);
            CurrentTurn.lastYutResult = result;
            SetPhase(TurnPhase.SaveThrowResult);

            // 낙이 나오면 턴 취소 처리하고 바로 턴 종료
            if (YutResultRule.IsTurnCancelResult(result))
            {
                CurrentTurn.isTurnCanceledByNak = true;
                EndTurn();
                return; // 이 부분에서 함수 종료(아래코드 실행 x)
            }

            // 보너스 던지기를 하나 사용했으니 -1 함
            if (isCaptureBonusThrow)
                pendingCaptureThrows--;
            // 던지기 기록을 만들어서 결과 묶음에 저장
            var throwData = new YutThrowData
            {
                player = CurrentTurn.currentPlayer, // 누가 던졌는지
                result = result, // 방금 나온 결과
                throwIndexInTurn = throwCountInTurn++, // 이번 턴이 몇번째 인지 작성하고, 다음 턴 번호 준비
                isBonusThrowFromCapture = isCaptureBonusThrow // 보너스 던지기 였는지 적음
            };
            pendingResults.Add(throwData);

            // 방금 던진 결과 묶음이 바뀌었다고 알림
            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));

            SetPhase(TurnPhase.CheckExtraThrow);

            // 윷/모로 추가 던지기 횟수가 남아있으면 다시 던지기 단계로 돌아감(3회 안 채워졌으면)
            if (YutResultRule.IsExtraThrowResult(result) &&
                CurrentTurn.extraThrowByYutMoCount < GameRuleDefine.MaxYutMoExtraThrowCount)
            {
                CurrentTurn.extraThrowByYutMoCount++;
                SetPhase(TurnPhase.WaitThrow);
                return;
            }
            // 윷, 모가 아니거나 이미 3회 던지기를 다 했으면 말 이동 단계로 넘어 감
            SetPhase(TurnPhase.WaitAction);
        }

        // ===================================================================
        // UI에서 플레이어가 [결과 묶음] 중 하나를 골라(chosenResult), 어떤 말을 옮길지(pieceId) 정하면 호출됨
        // pieceId, chosenResult는 이 함수의 매개변수 - UI가 호출할 때 직접 넣어주는 값
        // 실제 이동은 여기서 안 하고, "이동해도 되는지" 3가지만 검사함
        // ===================================================================
        public void RequestMovePiece(int pieceId, YutResult chosenResult)
        {
            // 검사 1 : 지금 단계가 말 이동 단계가 아니면 경고문 출력하고 무시
            if (CurrentTurn.currentPhase != TurnPhase.WaitAction)
            {
                Debug.LogWarning("지금은 말을 이동할 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }

            //검사 2 : chosenResult가 실제로 던져서 얻은 결과 pendingResults가 맞는지 찾기
            var matched = pendingResults.Find(r => r.result == chosenResult);
            if (matched == null)
            {
                Debug.LogWarning("결과 묶음에 없는 결과를 사용하려 함: " + chosenResult);
                return;
            }

            // 검사 3: 상태이상(속박/기절 등)으로 이 말이 못 움직이는 상태는 아닌지 확인
            if (boardExecutor != null && !boardExecutor.CanMove(pieceId))
            {
                Debug.LogWarning("상태이상 등으로 이동할 수 없는 말: " + pieceId);
                return;
            }

            // 3가지 검사 다 통과 -> "말 선택함" 단계로 표시
            SetPhase(TurnPhase.SelectPiece);

            // 말 이동한테 보낼 이동 요청서 작성. 몇 칸 이동할지만 계산해서 넘김
            var request = new BoardMoveRequest
            {
                pieceId = pieceId,
                yutResult = chosenResult,
                moveCount = YutResultRule.GetMoveCount(chosenResult) // "도=1칸, 윷=4칸" 같은 계산은 Common에 있는 함수 사용
            };

            // 이 결과는 이동에 사용했으니 묶음에서 제거하고, 바뀐 묶음을 다시 방송
            pendingResults.Remove(matched);
            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));

            SetPhase(TurnPhase.MovePiece); // "말 이동 중" 단계로 표시
            boardExecutor.RequestMove(request); // 실제 이도 요청 전달
        }

        // 보드 쪽이 말 이동/잡기/업기/완주 처리를 다 끝내면 자동으로 호출되는 함수
        // 이동 결과를 보고 다음에 무슨 동작을 할지 결정
        private void HandleBoardMoveResolved(BoardMoveResult result)
        {
            SetPhase(TurnPhase.ResolveTile); // 도착 칸 처리 단계로 표시 (특수 도착 칸 효과는 아직 미구현)

            SetPhase(TurnPhase.ResolveBoardRule); // 잡기/업기/완주 결과 처리 단계로 표시

            // 완주했는지 확인은 여기서 안 하고 WinConditionManager한테 결과를 넘겨서 대신 확인시킴
            winConditionManager.OnPieceMoveResolved(CurrentTurn.currentPlayer, CurrentTurn.currentTeam, result);

            // 잡기로 보너스 던지기가 생겼는지 확인하는 단계
            SetPhase(TurnPhase.CheckBonusThrow);
            if (result.capturedPieceIds.Count > 0) // 이번 이동으로 상대 말을 잡으면 
            {
                pendingCaptureThrows++; // 나중에 쓸 보너스 던지기 개수 +1
                CurrentTurn.extraThrowByCaptureCount++;
            }

            // 아직 안 쓴 윷 결과가 남아있으면 -> 계속 말 이동시키는 단계로 돌아감
            if (pendingResults.Count > 0)
            {
                SetPhase(TurnPhase.WaitAction);
                return;
            }

            // 윷 결과를 모두 사용했는데 보너스 던지기가 있으면 -> 다시 던지기 단계로 돌아감 
            if (pendingCaptureThrows > 0)
            {
                SetPhase(TurnPhase.WaitThrow);
                return;
            }

            EndTurn(); // 결과도 보너스 던지기도 없으면 턴 종료
        }

        // 지금 턴을 마무리하는 함수. 낙이 나오거나, 쓸 결과/보너스 던지기가 다 떨어지면 호출됨
        private void EndTurn()
        {
            SetPhase(TurnPhase.TurnEnd); // 턴 종료 단계로 표시
            OnTurnEnded?.Invoke(CurrentTurn.currentPlayer); // 턴 끝났다고 알림

            // WinConditionManager가 이미 승리를 확정지었으면(NotifyGameEnded 호출됨) 다음 턴으로 안 넘김
            if (CurrentTurn.isGameEnded)
            {
                SetPhase(TurnPhase.GameEnd);
                return;
            }
            // 게임이 안 끝났으면 다음 플레이어 턴으로 넘김
            AdvanceToNextPlayer();
        }

        private void AdvanceToNextPlayer()
        {
            // 순서 끝까지 가면 다시 처음 사람으로 돌아감 (4명이면 3 다음은 다시 0)
            TurnOrder.currentIndex = (TurnOrder.currentIndex + 1) % TurnOrder.order.Count;
            if (TurnOrder.currentIndex == 0) CurrentTurn.roundNumber++; // 처음으로 돌아왔다 = 한 바퀴 다 돔 -> 라운드 +1
            CurrentTurn.turnNumber++;        // 턴 진행될 때마다 무조건 +1
            BeginTurnFor(TurnOrder.Current);  // 다음 사람 턴 시작
        }

        // WinConditionManager가 승리 조건을 확인했을 때 호출해서 부탁하는 함수
        // 여기서 바로 게임을 멈추지 않고 "끝났다"는 표시만 해둠 -> 지금 턴이 끝날 때(EndTurn) 실제로 멈춤
        public void NotifyGameEnded()
        {
            CurrentTurn.isGameEnded = true;
        }

        // 단계를 바꿀 땐 항상 이 함수만 써야 함 (직접 안 바꾸고 여기 거쳐서 바꾸기)
        // 이유: 바꾸는 것 + 알리는 것, 이 두 가지가 매번 같이 일어나게 하려고
        private void SetPhase(TurnPhase phase)
        {
            CurrentTurn.currentPhase = phase;
            OnTurnPhaseChanged?.Invoke(CurrentTurn);
        }
    }
}