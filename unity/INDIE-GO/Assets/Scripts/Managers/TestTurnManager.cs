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
    // 결과(BoardMoveResult)만 받아서 턴 흐름을 진행한다. 이 클래스는 좌표를 아예 모른다.
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

            boardExecutor.OnMoveResolved += HandleBoardMoveResolved;

            // TODO: 지금은 임시로 4인 고정값. 실제로는 대기실에서 확정된 참가 플레이어 목록으로 채워야 함
            TurnOrder = new TurnOrderData
            {
                order = new List<PlayerSlot> { PlayerSlot.Player1, PlayerSlot.Player2, PlayerSlot.Player3, PlayerSlot.Player4 },
                currentIndex = 0
            };
            CurrentTurn = new TurnContext();
        }

        public void StartFirstTurn()
        {
            CurrentTurn.roundNumber = 1;
            CurrentTurn.turnNumber = 1;
            BeginTurnFor(TurnOrder.Current);
        }

        private void BeginTurnFor(PlayerSlot player)
        {
            CurrentTurn.currentPlayer = player;

            // settings.matchComposition에 "1vs1vs1vs1(개인전)"인지 "2vs2(팀전)"인지가 담겨있으므로
            // Common의 MatchCompositionRule을 그대로 써서 team을 계산함
            CurrentTurn.currentTeam = MatchCompositionRule.GetTeamSlot(settings.matchComposition, player);

            pendingResults.Clear();
            pendingCaptureThrows = 0;
            throwCountInTurn = 0;
            CurrentTurn.extraThrowByYutMoCount = 0;
            CurrentTurn.extraThrowByCaptureCount = 0;
            CurrentTurn.isTurnCanceledByNak = false;

            SetPhase(TurnPhase.TurnStart);
            OnTurnStarted?.Invoke(player);

            SetPhase(TurnPhase.ApplyTurnStartRule);

            SetPhase(TurnPhase.WaitThrow);
        }

        public void RequestThrow()
        {
            if (CurrentTurn.currentPhase != TurnPhase.WaitThrow)
            {
                Debug.LogWarning("지금은 윷을 던질 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }

            bool isCaptureBonusThrow = pendingCaptureThrows > 0;

            SetPhase(TurnPhase.Throwing);
            YutResult result = yutRuleManager.Throw(CurrentTurn.currentPlayer);
            CurrentTurn.lastYutResult = result;
            SetPhase(TurnPhase.SaveThrowResult);

            if (YutResultRule.IsTurnCancelResult(result))
            {
                CurrentTurn.isTurnCanceledByNak = true;
                EndTurn();
                return;
            }

            if (isCaptureBonusThrow)
                pendingCaptureThrows--;

            var throwData = new YutThrowData
            {
                player = CurrentTurn.currentPlayer,
                result = result,
                throwIndexInTurn = throwCountInTurn++,
                isBonusThrowFromCapture = isCaptureBonusThrow
            };
            pendingResults.Add(throwData);

            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));

            SetPhase(TurnPhase.CheckExtraThrow);

            if (YutResultRule.IsExtraThrowResult(result) &&
                CurrentTurn.extraThrowByYutMoCount < GameRuleDefine.MaxYutMoExtraThrowCount)
            {
                CurrentTurn.extraThrowByYutMoCount++;
                SetPhase(TurnPhase.WaitThrow);
                return;
            }

            SetPhase(TurnPhase.WaitAction);
        }

        public void RequestMovePiece(int pieceId, YutResult chosenResult)
        {
            if (CurrentTurn.currentPhase != TurnPhase.WaitAction)
            {
                Debug.LogWarning("지금은 말을 이동할 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }

            var matched = pendingResults.Find(r => r.result == chosenResult);
            if (matched == null)
            {
                Debug.LogWarning("결과 묶음에 없는 결과를 사용하려 함: " + chosenResult);
                return;
            }
            if (boardExecutor != null && !boardExecutor.CanMove(pieceId))
            {
                Debug.LogWarning("상태이상 등으로 이동할 수 없는 말: " + pieceId);
                return;
            }

            SetPhase(TurnPhase.SelectPiece);

            var request = new BoardMoveRequest
            {
                pieceId = pieceId,
                yutResult = chosenResult,
                moveCount = YutResultRule.GetMoveCount(chosenResult)
            };

            pendingResults.Remove(matched);
            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));

            SetPhase(TurnPhase.MovePiece);
            boardExecutor.RequestMove(request);
        }

        private void HandleBoardMoveResolved(BoardMoveResult result)
        {
            SetPhase(TurnPhase.ResolveTile);

            SetPhase(TurnPhase.ResolveBoardRule);

            winConditionManager.OnPieceMoveResolved(CurrentTurn.currentPlayer, CurrentTurn.currentTeam, result);

            SetPhase(TurnPhase.CheckBonusThrow);
            if (result.capturedPieceIds.Count > 0)
            {
                pendingCaptureThrows++;
                CurrentTurn.extraThrowByCaptureCount++;
            }

            if (pendingResults.Count > 0)
            {
                SetPhase(TurnPhase.WaitAction);
                return;
            }

            if (pendingCaptureThrows > 0)
            {
                SetPhase(TurnPhase.WaitThrow);
                return;
            }

            EndTurn();
        }

        private void EndTurn()
        {
            SetPhase(TurnPhase.TurnEnd);
            OnTurnEnded?.Invoke(CurrentTurn.currentPlayer);

            if (CurrentTurn.isGameEnded)
            {
                SetPhase(TurnPhase.GameEnd);
                return;
            }

            AdvanceToNextPlayer();
        }

        private void AdvanceToNextPlayer()
        {
            TurnOrder.currentIndex = (TurnOrder.currentIndex + 1) % TurnOrder.order.Count;
            if (TurnOrder.currentIndex == 0) CurrentTurn.roundNumber++;
            CurrentTurn.turnNumber++;
            BeginTurnFor(TurnOrder.Current);
        }

        public void NotifyGameEnded()
        {
            CurrentTurn.isGameEnded = true;
        }

        private void SetPhase(TurnPhase phase)
        {
            CurrentTurn.currentPhase = phase;
            OnTurnPhaseChanged?.Invoke(CurrentTurn);
        }
    }
}