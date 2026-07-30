using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers.GameProgress;

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

            // 대기실 인원수(settings.playerCount)만큼 참가자 목록을 만듦
            // 예: playerCount=4 -> [Player1, Player2, Player3, Player4]
            // 이 시점엔 아직 "누가 몇 번째로 도는지"(진짜 순서)는 안 정해진 상태 -> StartFirstTurn()에서 정함
            var initialPlayers = BuildPlayerList(settings.playerCount);
            TurnOrder = new TurnOrderData
            {
                order = initialPlayers,
                currentIndex = 0
            };
            CurrentTurn = new TurnContext();
        }

        // playerCount(대기실에서 정한 인원수)만큼 PlayerSlot 목록을 만들어주는 함수
        // PlayerSlot enum 값이 Player1=1, Player2=2 ... 순서라서 숫자로 바로 변환 가능
        private List<PlayerSlot> BuildPlayerList(int count)
        {
            var list = new List<PlayerSlot>();
            for (int i = 1; i <= count && i <= 8; i++)
                list.Add((PlayerSlot)i);
            return list;
        }

        // GameManager.StartGame()에서 Initialize() 다음에 호출, 진짜 첫 턴을 시작
        public void StartFirstTurn()
        {
            // ===================================================================
            // 순서 정하기: 기획서 "윷 던지기로 정하기, 도개걸윷모뒷도 모두 같은 확률"
            // 참가자 전원이 균등확률로 한 번씩 던져서, 높은 결과가 나온 사람이 먼저 시작
            // (동점이면 동점자끼리만 다시 던져서 순위를 가림)
            // ===================================================================
            var determinedOrder = DetermineOrder(TurnOrder.order);
            TurnOrder = new TurnOrderData
            {
                order = determinedOrder,
                currentIndex = 0
            };
            Debug.Log("[실제 게임] 확정된 순서: " + string.Join(" -> ", determinedOrder)); // 테스트용, 진짜 게임이 쓰는 순서를 딱 한 번 찍음

            CurrentTurn.roundNumber = 1;
            CurrentTurn.turnNumber = 1;
            BeginTurnFor(TurnOrder.Current);
        }

        // 참가자 목록을 받아서, 던진 결과가 높은 순서대로 정렬된 새 목록을 돌려줌
        // YutResult enum 값 자체가 BackDo=-1, Do=1, Gae=2, Geol=3, Yut=4, Mo=5 순이라
        // (int)로 바로 비교하면 "높은 결과 = 먼저 시작" 순서가 됨
        private List<PlayerSlot> DetermineOrder(List<PlayerSlot> players)
        {
            if (players.Count <= 1) return new List<PlayerSlot>(players); // 1명 이하면 정할 것도 없음

            // 이번 라운드에서 각 플레이어가 던진 결과를 기록
            var resultByPlayer = new Dictionary<PlayerSlot, YutResult>();
            foreach (var p in players)
                resultByPlayer[p] = yutRuleManager.ThrowForOrder();

            // 결과값이 높은 그룹부터 순서대로 정렬 (그룹 안에 여러 명이면 동점)
            var groupsHighToLow = players
                .GroupBy(p => resultByPlayer[p])
                .OrderByDescending(g => (int)g.Key);

            var finalOrder = new List<PlayerSlot>();
            foreach (var group in groupsHighToLow)
            {
                var tiedPlayers = group.ToList();
                if (tiedPlayers.Count == 1)
                {
                    finalOrder.Add(tiedPlayers[0]); // 동점자 없으면 그대로 순서에 추가
                }
                else
                {
                    // 동점자끼리만 다시 던져서 그 안에서의 순서를 가림 
                    finalOrder.AddRange(DetermineOrder(tiedPlayers));
                }
            }
            return finalOrder;
        }

        // 테스트용: 인스펙터 우클릭으로 순서정하기 전체 과정을 확인
        [ContextMenu("테스트: 순서 다시 정하기")]
        public void TestDetermineOrder()
        {
            var result = DetermineOrder(TurnOrder.order);
            Debug.Log("정해진 순서: " + string.Join(" -> ", result));
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

        // ===================================================================
        // 아래 3개는 UI 없이 인스펙터 우클릭으로 테스트하기 위한 임시 함수들.
        // 실제 게임 로직에는 영향 없음 (UI 완성되면 이 3개는 지워도 됨)
        // ===================================================================
        [ContextMenu("테스트: 던지기")]
        public void TestRequestThrow()
        {
            RequestThrow();
        }

        [ContextMenu("테스트: 이동 (첫 결과로)")]
        public void TestRequestMovePiece()
        {
            if (pendingResults.Count == 0)
            {
                Debug.LogWarning("테스트 실패: pendingResults가 비어있음. 먼저 던지기부터 하세요.");
                return;
            }
            var first = pendingResults[0];
            Debug.Log("이동 요청 보냄: " + first.result);
            RequestMovePiece(1, first.result);
            Debug.Log("이동 요청 처리 끝, 현재 단계: " + CurrentTurn.currentPhase);
        }

        [ContextMenu("테스트: 현재 상태 보기")]
        public void TestPrintCurrentPhase()
        {
            Debug.Log("현재 단계: " + CurrentTurn.currentPhase
                + " / 결과묶음 개수: " + pendingResults.Count
                + " / 현재 플레이어: " + CurrentTurn.currentPlayer);
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

        // UI에서 플레이어가 [결과 묶음] 중 하나를 골라(chosenResult), 어떤 말을 옮길지(pieceId) 정하면 호출됨
        // pieceId, chosenResult는 이 함수의 매개변수 - UI가 호출할 때 직접 넣어주는 값
        // 여기서는 실제 이동은 안 하고, "이동해도 되는지" 3가지만 검사함
        public void RequestMovePiece(int pieceId, YutResult chosenResult)
        {
            // 검사 1: 지금 말 이동 가능한 단계(WaitAction)가 맞는지
            if (CurrentTurn.currentPhase != TurnPhase.WaitAction)
            {
                Debug.LogWarning("지금은 말을 이동할 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }

            // 검사 2: chosenResult가 실제로 던져서 얻은 결과(pendingResults 안)가 맞는지 찾음
            var matched = pendingResults.Find(r => r.result == chosenResult);
            if (matched == null)
            {
                Debug.LogWarning("결과 묶음에 없는 결과를 사용하려 함: " + chosenResult);
                return;
            }
            // 검사 3: 상태이상(속박/기절 등)으로 이 말이 못 움직이는 상태는 아닌지 영서 쪽에 확인
            if (boardExecutor != null && !boardExecutor.CanMove(pieceId))
            {
                Debug.LogWarning("상태이상 등으로 이동할 수 없는 말: " + pieceId);
                return;
            }

            // 3가지 검사 다 통과 -> "말 선택함" 단계로 표시 (이 아래에서 실제 이동 요청으로 이어짐)
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

        // 영서(보드) 쪽이 말 이동 처리를 다 끝내면 자동으로 호출되는 함수 (59번째 줄에서 등록해둠)
        // 여기서부터 다시 내(턴 관리자) 담당 - 이동 결과를 보고 다음에 뭘 할지 결정만 함
        private void HandleBoardMoveResolved(BoardMoveResult result)
        {
            SetPhase(TurnPhase.ResolveTile); // 도착 칸 처리 단계로 표시 (특수효과는 아직 미구현)

            SetPhase(TurnPhase.ResolveBoardRule); // 잡기/업기/완주 결과 처리 단계로 표시

            // 완주했는지 확인은 여기서 안 하고, WinConditionManager한테 결과를 넘겨서 대신 확인시킴
            winConditionManager.OnPieceMoveResolved(CurrentTurn.currentPlayer, CurrentTurn.currentTeam, result);

            SetPhase(TurnPhase.CheckBonusThrow); // 잡기로 보너스 던지기 생겼는지 확인하는 단계로 표시
            if (result.capturedPieceIds.Count > 0) // 이번 이동으로 상대 말을 잡았으면
            {
                pendingCaptureThrows++;             // 나중에 쓸 보너스 던지기 개수 +1 (지금 바로 던지는 거 아님)
                CurrentTurn.extraThrowByCaptureCount++;
            }

            // 아직 안 쓴 윷 결과가 남아있으면 -> 계속 말 이동시키는 단계로 돌아감
            if (pendingResults.Count > 0)
            {
                SetPhase(TurnPhase.WaitAction);
                return;
            }

            // 쓸 결과는 다 썼는데, 쌓아둔 보너스 던지기가 있으면 -> 그거 쓰러 던지기 단계로 돌아감
            if (pendingCaptureThrows > 0)
            {
                SetPhase(TurnPhase.WaitThrow);
                return;
            }

            EndTurn(); // 쓸 결과도, 보너스 던지기도 없으면 이 턴은 여기서 끝
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

        // 다음 사람 차례로 넘김
        private void AdvanceToNextPlayer()
        {
            // 순서 끝까지 가면 다시 처음 사람으로 돌아감 (4명이면 3 다음은 다시 0)
            TurnOrder.currentIndex = (TurnOrder.currentIndex + 1) % TurnOrder.order.Count;

            if (TurnOrder.currentIndex == 0) CurrentTurn.roundNumber++; // 처음으로 돌아왔다 = 한 바퀴 다 돔 -> 라운드 +1
            CurrentTurn.turnNumber++;        // 턴 진행될 때마다 무조건 +1
            BeginTurnFor(TurnOrder.Current);  // 다음 사람 턴 시작
        }

        // WinConditionManager가 승리 조건을 확인했을 때 호출해서 부탁하는 함수
        // (CurrentTurn은 이 클래스만 바꿀 수 있어서, 다른 클래스는 직접 못 바꾸고 이 함수로 부탁함)
        // 여기서 바로 게임을 멈추지 않고, "끝났다"는 표시만 해둠 -> 지금 턴이 끝날 때(EndTurn) 실제로 멈춤
        public void NotifyGameEnded()
        {
            CurrentTurn.isGameEnded = true;
        }

        // ===================================================================
        // 다인전 순위 시스템용: 목표를 먼저 채운 팀이 있으면, 게임을 끝내지 않고
        // 그 팀 소속 플레이어들만 턴 순서에서 빼달라고 WinConditionManager가 부탁하는 함수
        // (예: 4명 개인전에서 Player1이 먼저 목표 채우면, Player1만 순서에서 빠지고
        //  Player2/3/4끼리 계속 진행됨)
        // ===================================================================
        public void RemovePlayersOfTeam(TeamSlot team, GameStartSettings gameSettings)
        {
            // 지금 순서 목록에서, 방금 빠져야 할 팀 소속이 아닌 사람들만 남김
            var remainingPlayers = TurnOrder.order
                .Where(p => MatchCompositionRule.GetTeamSlot(gameSettings.matchComposition, p) != team)
                .ToList();

            // 지금 차례였던 사람이 방금 제외된 팀이면, 남은 목록에서 자연스럽게 다음 사람부터 이어가게 보정
            PlayerSlot currentBefore = TurnOrder.Current;
            int newIndex = remainingPlayers.IndexOf(currentBefore);
            if (newIndex < 0) newIndex = 0; // 지금 차례인 사람이 제외됐으면, 새 목록 맨 앞 사람부터

            TurnOrder = new TurnOrderData
            {
                order = remainingPlayers,
                currentIndex = remainingPlayers.Count > 0 ? newIndex : 0
            };
        }

        // 턴 단계를 바꾸는 유일한 통로. 값 바꾸기 + 방송하기를 항상 같이 실행되게 묶어둠
        private void SetPhase(TurnPhase phase)
        {
            CurrentTurn.currentPhase = phase;
            OnTurnPhaseChanged?.Invoke(CurrentTurn);
        }
    }
}   