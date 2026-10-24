using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers.GameProgress;
using YutArena.InGame;
namespace YutArena.Managers
{
    // ===================================================================
    // 전체 흐름: 턴 시작 -> 윷 던지기(윷/모면 반복, 최대 3회) -> 결과 묶음 중 원하는 순서로 말 이동 -> 잡기 보너스 던지기(있으면) -> 턴 종료 -> 다음 플레이어
    // 아주 중요한 원칙:
    // 보드 좌표/다음칸/갈림길/잡기/업기/완주 "판정"은 절대 이 클래스에서 하지 않는다.
    // 영서의 PlayerManager/PieceMovementManager에게 "이동해줘"라고 요청만 하고, 처리된 결과
    // (PlayerRuntimeData)를 직접 찾아가서 읽기만 한다. 이 클래스는 좌표를 아예 모른다.
    // ( PlayerManager/ PieceMovementManager를 직접 호출하는 방식으로 전환함)
    // ===================================================================
    public class TestTurnManager : MonoBehaviour
    {
        //inspector창에서 드래그 할 수 있는 칸 만들기
        [Header("Dependencies")]
        [SerializeField] private TestYutRuleManager yutRuleManager;
        [SerializeField] private TestWinConditionManager winConditionManager;
        [SerializeField] private PlayerManager playerManager;               // 영서 코드: 플레이어/말 데이터 보관
        [SerializeField] private PieceMovementManager pieceMovementManager; // 영서 코드: 이동/잡기/업기/완주 실제 처리
        // 외부에서 볼 수 있지만 코드 수정은 내부에서 가능
        public TurnContext CurrentTurn { get; private set; } = new TurnContext();
        // 이번 턴에 던져서 "아직 이동에 쓰지 않은" 윷 결과들을 쌓아두는 리스트
        // 예: 윷-모-도 순서로 던졌다면 이 리스트에 [윷, 모, 도] 3개가 들어있다가 플레이어가 원하는 순서로 하나씩 골라서 꺼내 쓰게 됨
        private readonly List<YutThrowData> pendingResults = new List<YutThrowData>();
        // 하이라이트 UI(MoveDestinationSelector) 초기 시딩용 읽기 전용 뷰.
        // 실제 갱신 통보는 OnPendingResultsChanged 이벤트로 이뤄진다.
        public IReadOnlyList<YutThrowData> PendingResults => pendingResults;
        // 잡기로 얻은 보너스 던지기 횟수 저장( 던진 윷 결과를 다 소모하고 보너스 던지기를 하니까)
        private int pendingCaptureThrows = 0;
        // 스킬로 얻은 보너스 던지기 횟수 저장 (예: 기본형 "한번더")
        // 잡기(pendingCaptureThrows)랑 따로 관리하는 이유: 나중에 "왜 추가 던지기가 생겼는지"
        // (잡아서인지 스킬 때문인지) UI에서 구분해서 보여줘야 할 수도 있어서.
        private int pendingSkillThrows = 0;
        //수정: 기본/윷·모로 이미 확보한 던지기는 잡기·스킬 보너스와 별도로 소비합니다.
        private bool pendingRegularThrow = true;
        // 턴 순서 관리 데이터 (Common의 GameSessionDefine.cs에 새로 추가??)
        public TurnOrderData TurnOrder { get; private set; } = new TurnOrderData();
        private GameStartSettings settings;
        public GameStartSettings Settings => settings;
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
            // 보드 매니저 2개가 잘 연결됐는지만 확인
            if (playerManager == null || pieceMovementManager == null)
            {
                Debug.LogError("TestTurnManager: playerManager 또는 pieceMovementManager가 연결 안 됨");
                return;
            }
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
            // 순서 정하기: 팀전 규칙 확정됨: 각 팀의 1p(대표)가
            // 순서정하기 던지기로 "팀 순서"를 정하고, 그 팀 순서대로 팀을 배치한 뒤,
            // 각 팀 안에서는 1p->2p 순(할당된 순서)으로 고정.
            // 개인전(팀 인원 1명씩)은 팀=자기 자신이라, 결과적으로 예전 방식(전원이 다 던짐)과 동일함
            // ===================================================================
            var determinedOrder = DetermineTeamBasedOrder(TurnOrder.order);
            TurnOrder = new TurnOrderData
            {
                order = determinedOrder,
                currentIndex = 0
            };
            Debug.Log("[실제 게임] 확정된 순서: " + string.Join(" -> ", determinedOrder)); // 테스트용, 진짜 게임이 쓰는 순서를 딱 한 번 찍음
            seatOrder = new List<PlayerSlot>(determinedOrder); // 라운드 계산용 (IsNewRound 참고)
            CurrentTurn.roundNumber = 1;
            CurrentTurn.turnNumber = 1;
            maxTurnLimitHandled = false;
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
        // ===================================================================
        // 팀 단위로 순서를 정하는 함수
        // "(1팀1P→2팀1P→1팀2P→2팀2P) 방식
        // 1단계: 팀별로 소속 플레이어를 모으고, 팀 내부 순서는 할당된 순서(PlayerSlot 오름차순, 1p->2p)로 고정
        // 2단계: 각 팀의 1p(대표)들끼리만 기존 DetermineOrder()(순서정하기 던지기+동점재던지기)로 "팀 순위"를 정함
        // 3단계: 팀 순위를 라운드(1P끼리, 2P끼리...) 단위로 번갈아가며 이어붙여서 최종 순서를 만듦
        // (3팀 이상이면 "1팀2팀3팀 1팀2팀3팀"처럼 자동으로 확장됨. 개인전은 대표=본인이라
        //  자연스럽게 예전 방식(전원이 다 던짐)과 동일해짐)
        // ===================================================================
        private List<PlayerSlot> DetermineTeamBasedOrder(List<PlayerSlot> players)
        {
            var playersByTeam = players
                .GroupBy(p => MatchCompositionRule.GetTeamSlot(settings, p))
                .ToDictionary(g => g.Key, g => g.OrderBy(p => (int)p).ToList());

            var representatives = playersByTeam.Select(kv => kv.Value[0]).ToList();

            // 팀 순위(어느 팀이 먼저인지)만 뽑아둠
            var teamOrder = DetermineOrder(representatives)
                .Select(rep => MatchCompositionRule.GetTeamSlot(settings, rep))
                .ToList();

            // 라운드(1P끼리, 2P끼리...) 단위로 팀 순위를 반복해서 번갈아가며 이어붙임
            var finalOrder = new List<PlayerSlot>();
            int maxTeamSize = playersByTeam.Values.Max(list => list.Count);
            for (int roundIndex = 0; roundIndex < maxTeamSize; roundIndex++)
            {
                foreach (var team in teamOrder)
                {
                    var teamPlayers = playersByTeam[team];
                    if (roundIndex < teamPlayers.Count)
                        finalOrder.Add(teamPlayers[roundIndex]);
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
        // 테스트용: 인스펙터 우클릭으로 팀 단위 순서정하기 전체 과정을 확인
        [ContextMenu("테스트: 팀 단위 순서 다시 정하기")]
        public void TestDetermineTeamBasedOrder()
        {
            var result = DetermineTeamBasedOrder(TurnOrder.order);
            Debug.Log("팀 단위로 정해진 순서: " + string.Join(" -> ", result));
        }
        private void BeginTurnFor(PlayerSlot player)
        {
            CurrentTurn.currentPlayer = player;
            // settings.matchComposition에 "1vs1vs1vs1(개인전)"인지 "2vs2(팀전)"인지가 담겨있으므로
            // Common의 MatchCompositionRule을 그대로 써서 team을 계산함
            CurrentTurn.currentTeam = MatchCompositionRule.GetTeamSlot(settings, player);
            pendingResults.Clear();
            pendingCaptureThrows = 0;
            pendingSkillThrows = 0; // 새 턴 시작이니 스킬 보너스 던지기도 초기화
            pendingRegularThrow = true;
            throwCountInTurn = 0;
            CurrentTurn.extraThrowByYutMoCount = 0;
            CurrentTurn.extraThrowByCaptureCount = 0;
            CurrentTurn.isTurnCanceledByNak = false;
            SetPhase(TurnPhase.TurnStart);
            OnTurnStarted?.Invoke(player);
            SetPhase(TurnPhase.ApplyTurnStartRule);
            ApplyTurnStartCcRule(player); //  이 플레이어 말들의 CC 남은 턴수를 -1하고, 0되면 해제
            SetPhase(TurnPhase.WaitThrow);
            // 통합 타이머  던지기 따로 이동 따로가 아니라, 턴 시작할 때 한 번만
            // 통합 타이머(45초, 던지기+이동+스킬 전부 포함)를 켬. 아래 StartTurnTimer() 참고
            StartTurnTimer();
        }
        // ===================================================================
        // 턴 시작마다 이 플레이어 소유 말들의 CC(상태이상) 지속시간을 관리함
        // 영서가 CC를 걸 때 남은 턴수(RemainingCcTurns)까지 같이 넣어주면, 저희가 매 턴마다
        // -1씩 깎아주고 0이 되면 풀어주는 역할. (실제로 "말을 못 움직이게 막는 것" 자체는
        // 이미 CanPieceMove()에서 CurrentCc == Stun 체크로 처리하고 있음 - 이건 그거랑 별개로
        // "얼마나 오래 걸려있는지" 시간 관리만 하는 부분)
        // Kill/Retire는 즉시 소비되는 일회성 마커라 턴수 감소 대상이 아님 (ConsumeCaptureResults가 처리)
        // ===================================================================
        private void ApplyTurnStartCcRule(PlayerSlot player)
        {
            if (playerManager == null) return;
            int playerId = (int)player;
            if (!playerManager.TryGetPlayer(playerId, out var playerController)) return;
            //수정: 복합 CC의 지속시간/만료 효과는 Character 효과 처리기 한 곳에서 관리합니다.
            CcEffectService.TickOwnerTurn(playerController);
        }

        // ===================================================================
        // 아래 3개는 인스펙터 우클릭 테스트용으로 만든 함수들.
        // 주의: 인게임 던지기 버튼이 TestRequestThrow를 호출하고 있어서 지우면 버튼이 고장남.
        // (버튼 연결을 RequestThrow로 바꾼 뒤에만 지울 것)
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
            RequestMovePiece(0, first.result); //  테스트용 pieceId를 0으로 (영서 코드는 pieceId가 0부터 시작함)
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
            if (!LocalSelectionInput.SharedInputAvailable) return;
            // 턴 수 도달 후 타이브레이커 중이면, 일반 던지기 대신 타이브레이커 던지기로 보냄
            // (던지기 버튼을 그대로 같이 씀. 한 화면에서 하니까 아직 안 던진 사람이 순서대로 한 명씩 던짐)
            // 확률은 RequestTieBreakerThrow 안에서 타이브레이커 전용표(낙 없음)를 씀
            if (winConditionManager != null && winConditionManager.IsTieBreakerActive)
            {
                if (winConditionManager.TieBreakerPendingPlayers.Count > 0)
                    winConditionManager.RequestTieBreakerThrow(winConditionManager.TieBreakerPendingPlayers[0]);
                return;
            }
            if (EndTurnIfCurrentPlayerOutOfTurnOrder()) return; // 스킬로 순위 확정돼 빠진 사람이면 턴 종료
            if (CurrentTurn.currentPhase != TurnPhase.WaitThrow)
            {
                Debug.LogWarning("지금은 윷을 던질 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }
            // 통합 타이머 : 턴 시작할 때 한 번 켜지면 계속 흐르고, 턴이 끝날 때만 멈춤
            // 기본/윷·모 기회를 먼저 소비한 뒤, 잡기 → 스킬 보너스 순서로 소비함
            bool isCaptureBonusThrow = !pendingRegularThrow && pendingCaptureThrows > 0;
            bool isSkillBonusThrow = !pendingRegularThrow && !isCaptureBonusThrow && pendingSkillThrows > 0;
            SetPhase(TurnPhase.Throwing);
            YutResult result = yutRuleManager.Throw(CurrentTurn.currentPlayer);
            Debug.Log("[던지기] " + CurrentTurn.currentPlayer + " → " + result); // 던진 결과 확인용
            CurrentTurn.lastYutResult = result;
            SetPhase(TurnPhase.SaveThrowResult);
            if (YutResultRule.IsTurnCancelResult(result))
            {
                CurrentTurn.isTurnCanceledByNak = true;
                EndTurn();
                return;
            }
            if (pendingRegularThrow)
                pendingRegularThrow = false;
            else if (isCaptureBonusThrow)
                pendingCaptureThrows--;
            else if (isSkillBonusThrow)
                pendingSkillThrows--;
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

            // 기본 윷 규칙을 계산한 뒤 Player 스킬 시스템이 최종 추가 던지기를 결정합니다.
            bool grantsDefaultExtraThrow =
                YutResultRule.IsExtraThrowResult(result) &&
                CurrentTurn.extraThrowByYutMoCount < GameRuleDefine.MaxYutMoExtraThrowCount;
            bool grantsExtraThrow = CharacterSkillRegistry.ShouldGrantExtraThrow(
                (int)CurrentTurn.currentPlayer,
                result,
                grantsDefaultExtraThrow);

            if (grantsExtraThrow)
            {
                CurrentTurn.extraThrowByYutMoCount++;
                pendingRegularThrow = true;
                SetPhase(TurnPhase.WaitThrow);
                //  타이머를 재시작하지 않고, 남은 시간에 보너스만 더함(추가 던지기 보너스 시간)
                AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds);
                return;
            }
            // 수정: 잡기/스킬로 받은 보너스 던지기가 남아 있으면 이동하기 전에 마저 던짐.
            // 가이드 "추가 던지기가 남아있으면 [윷 던지기]로 복귀, 없으면 다음 단계로"
            // 예: 두 팀 말을 한 번에 잡아 보너스 2개 → 던지기, 던지기 → 이동, 이동 (예전엔 던지기 → 이동 → 던지기 → 이동)
            if (pendingCaptureThrows > 0 || pendingSkillThrows > 0)
            {
                SetPhase(TurnPhase.WaitThrow);
                return;
            }
            SetPhase(TurnPhase.WaitAction);
            //  이동 단계로 넘어가도 타이머는 이미 계속 흐르고 있어서 따로 시작 안 함
            EndTurnIfNoUsableResult();
        }
        // UI에서 플레이어가 [결과 묶음] 중 하나를 골라(chosenResult), 어떤 말을 옮길지(pieceId) 정하면 호출됨
        // pieceId, chosenResult는 이 함수의 매개변수 - UI가 호출할 때 직접 넣어주는 값
        // 이 함수 안에서 검사 -> 이동요청 -> 결과 확인 -> 다음 단계 결정까지 한번에 처리함
        public void RequestMovePiece(int pieceId, YutResult chosenResult)
        {
            if (!LocalSelectionInput.SharedInputAvailable) return;
            if (EndTurnIfCurrentPlayerOutOfTurnOrder()) return; // 스킬로 순위 확정돼 빠진 사람이면 턴 종료
            // 검사 1: 지금 말 이동 가능한 단계(WaitAction)가 맞는지
            if (CurrentTurn.currentPhase != TurnPhase.WaitAction)
            {
                Debug.LogWarning("지금은 말을 이동할 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }
            if (TryGetPendingForcedMove(out _))
            {
                Debug.LogWarning("윤누리 패시브 추가 1칸 이동을 먼저 해야 함 (RequestForcedMove)");
                return;
            }
            // 검사 2: chosenResult가 실제로 던져서 얻은 결과(pendingResults 안)가 맞는지 찾음
            var matched = pendingResults.Find(r => r.result == chosenResult);
            if (matched == null)
            {
                Debug.LogWarning("결과 묶음에 없는 결과를 사용하려 함: " + chosenResult);
                return;
            }
            int playerId = (int)CurrentTurn.currentPlayer; // PlayerSlot(Player1=1..) 값 그대로 영서쪽 playerId(int)로 씀
                                                           // 검사 3: 상태이상(Stun)이나 업힌 말(리더 아님)이라 못 움직이는 상태는 아닌지 확인
            if (!CanPieceMove(playerId, pieceId))
            {
                Debug.LogWarning("상태이상 또는 업힌 말이라 이동할 수 없는 말: " + pieceId);
                return;
            }
            // 3가지 검사 다 통과 -> "말 선택함" 단계로 표시 (이 아래에서 실제 이동 요청으로 이어짐)
            SetPhase(TurnPhase.SelectPiece);
            pendingResults.Remove(matched);
            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));
            int moveCount = YutResultRule.GetMoveCount(chosenResult);
            // ===================================================================
            // 뒷도(BackDo)를 "실제로 쓰려는 이 시점"에 확인함 
            // "모→뒷도"처럼 먼저 다른 결과로 말을 내보낸 뒤에 뒷도를 쓰는 경우가 있어서,
            // 던진 그 순간이 아니라 여기(실제 이동 시도하는 시점)에서 확인해야
            // "이미 나간 말이 있으면 정상 후진 처리"까지 정확히 반영됨
            // 규칙: 이 플레이어 말이 보드 위에(State == InBoard) 하나도 없으면(전부 대기중)
            //       뒷도를 쓸 수 없으므로 바로 턴 종료
            // ===================================================================
            if (chosenResult == YutResult.BackDo && !HasAnyPieceInBoard(playerId))
            {
                Debug.LogWarning("뒷도인데 보드 위에 말이 하나도 없음 -> 턴 종료");
                EndTurn();
                return;
            }
            // 완주 개수는 "이동 전/후 완주한 말 개수 차이"로 계산 (업기로 여러 마리가 한꺼번에 골인할 수 있어서)
            int finishedCountBefore = CountFinishedPieces(playerId);
            SetPhase(TurnPhase.MovePiece);
            //  pieceMovementManager.TryMovePiece()가 호출 즉시 이동/잡기/업기/완주를 다 처리함
            bool moveSucceeded = pieceMovementManager.TryMovePiece(playerId, pieceId, moveCount);
            if (!moveSucceeded)
            {
                Debug.LogWarning("영서 쪽 이동 처리 실패: player=" + playerId + " piece=" + pieceId);
            }
            ResolveAfterMove(playerId, finishedCountBefore);
        }

        // 이동 한 번이 끝난 뒤 공통 처리: 완주/승리 확인 -> 잡기 보너스 -> 다음 단계 결정
        // (일반 이동과 윤누리 패시브 추가 이동이 같이 씀)
        private void ResolveAfterMove(int playerId, int finishedCountBefore)
        {
            SetPhase(TurnPhase.ResolveTile); // 도착 칸 처리 단계로 표시 (특수효과는 아직 미구현)
            SetPhase(TurnPhase.ResolveBoardRule); // 잡기/업기/완주 결과 처리 단계로 표시
                                                  // 완주했는지 확인은 여기서 안 하고, WinConditionManager한테 결과를 넘겨서 대신 확인시킴
            int finishedCountAfter = CountFinishedPieces(playerId);
            int newlyFinishedCount = finishedCountAfter - finishedCountBefore;
            bool isFinished = newlyFinishedCount > 0;
            winConditionManager.OnPieceMoveResolved(
                CurrentTurn.currentPlayer, CurrentTurn.currentTeam, isFinished, newlyFinishedCount);

            // 버그 수정: 이 이동으로 승리가 확정되면 NotifyGameEnded가 GameEnd로 바꿔놓는데, 예전엔 아래에서
            // 남은 윷 결과/보너스 던지기를 보고 단계를 WaitAction/WaitThrow로 다시 덮어써서
            // 게임이 끝난 뒤에도 말 이동/던지기가 가능했음. 게임이 끝났으면 여기서 멈춤.
            if (CurrentTurn.isGameEnded) return;

            // ===================================================================
            // Escape 모드 전용: 완주한 말은 재사용해야 하므로, 상태를 다시
            // 대기(Waiting)로 되돌림. 영서 확인: "위치는 나중에 조정, 지금은 상태만 바꾸면 됨"
            // ===================================================================
            if (isFinished && settings.gameMode == GameMode.Escape)
            {
                ResetFinishedPiecesToWaiting(playerId);
            }
            // ===================================================================
            // 다인전(개인전 3~4명): 이번 이동으로 목표를 채워 순위가 확정되면 이 플레이어는
            // 턴 순서에서 빠짐(RemovePlayersOfTeam). 빠진 사람이 남은 윷 결과/보너스 던지기로
            // 아직 경쟁 중인 사람 말을 잡으면 안 되므로, 전부 버리고 바로 턴을 끝냄.
            // (팀전 2vs2는 목표 채우면 바로 게임이 끝나서 여기 안 옴)
            // ===================================================================
            if (IsCurrentPlayerOutOfTurnOrder())
            {
                DiscardRemainingAndEndTurn();
                return;
            }
            SetPhase(TurnPhase.CheckBonusThrow); // 잡기로 보너스 던지기 생겼는지 확인하는 단계로 표시
                                                 //  상대 말들의 CC(Kill/Retire)를
                                                 // 직접 훑어서 확인함. 윷/모(4~5칸)로 잡으면 Retire(추가턴 없음),
                                                 // 도~걸/뒷도(1~3칸,-1칸)로 잡으면 Kill(추가턴 있음)
            int captureBonusCount = ConsumeCaptureResults(playerId);
            if (captureBonusCount > 0) // 이번 이동으로 상대 말을 Kill로 잡았으면
            {
                pendingCaptureThrows += captureBonusCount; // 잡은 팀 수만큼 보너스 던지기 (지금 바로 던지는 거 아님)
                CurrentTurn.extraThrowByCaptureCount += captureBonusCount;
                AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds * captureBonusCount); //  잡기 보너스도 시간 더해줌
            }
            // 윤누리 첫 이동 +1: 추가 1칸 이동이 대기 중이면 남은 결과/보너스 던지기보다 먼저 처리.
            // QA 수정(10/23): 예전엔 플레이어가 두 번째 화살표를 눌러야 움직였는데, 패시브라 자동으로 바로 1칸 이동함.
            // 업혀서(리더 아님) 못 움직이면 이동 실패로 그냥 넘어감 → 추가 이동 화살표도 안 뜸.
            // 추가 1칸으로 잡거나 탈출하면 아래에서 다시 ResolveAfterMove 로 똑같이 처리 (잡기 보너스 포함).
            if (TryGetPendingForcedMove(out _, out CHAR_001_1_Status forcedMoveOwner))
            {
                int finishedBeforeForcedMove = CountFinishedPieces(playerId);
                SetPhase(TurnPhase.MovePiece);
                if (!forcedMoveOwner.ExecutePendingForcedMove())
                    Debug.Log("[턴] 윤누리 추가 1칸 이동 못 함 (업힘 등) → 건너뜀");
                ResolveAfterMove(playerId, finishedBeforeForcedMove);
                return;
            }
            // 아직 안 쓴 윷 결과가 남아있으면 -> 계속 말 이동시키는 단계로 돌아감
            if (pendingResults.Count > 0)
            {
                SetPhase(TurnPhase.WaitAction);
                EndTurnIfNoUsableResult();
                return;
            }
            // 쓸 결과는 다 썼는데, 쌓아둔 보너스 던지기(잡기 또는 스킬)가 있으면 -> 그거 쓰러 던지기 단계로 돌아감
            if (pendingCaptureThrows > 0 || pendingSkillThrows > 0) // 스킬 보너스도 같이 확인
            {
                SetPhase(TurnPhase.WaitThrow);
                // 타이머는 이미 계속 흐르고 있어서 여기서 다시 시작 안 함
                return;
            }
            EndTurn(); // 쓸 결과도, 보너스 던지기도 없으면 이 턴은 여기서 끝
        }

        // 윤누리 첫 이동 +1: 플레이어가 두 번째 화살표(추가 1칸)를 눌렀을 때 UI가 호출
        public void RequestForcedMove()
        {
            if (EndTurnIfCurrentPlayerOutOfTurnOrder()) return; // 스킬로 순위 확정돼 빠진 사람이면 턴 종료
            if (CurrentTurn.currentPhase != TurnPhase.WaitAction)
            {
                Debug.LogWarning("지금은 말을 이동할 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }
            if (!TryGetPendingForcedMove(out _, out CHAR_001_1_Status forcedMoveOwner))
            {
                Debug.LogWarning("대기 중인 윤누리 추가 이동이 없음");
                return;
            }
            int playerId = (int)CurrentTurn.currentPlayer;
            int finishedCountBefore = CountFinishedPieces(playerId);
            SetPhase(TurnPhase.MovePiece);
            if (!forcedMoveOwner.ExecutePendingForcedMove())
            {
                Debug.LogWarning("윤누리 추가 1칸 이동 실패: player=" + playerId);
            }
            ResolveAfterMove(playerId, finishedCountBefore);
        }

        // 지금 차례인 플레이어 말 중에 윤누리 패시브 추가 1칸 이동이 대기 중인 말이 있는지
        public bool TryGetPendingForcedMove(out int pieceId) => TryGetPendingForcedMove(out pieceId, out _);

        private bool TryGetPendingForcedMove(out int pieceId, out CHAR_001_1_Status forcedMoveOwner)
        {
            pieceId = -1;
            forcedMoveOwner = null;
            int playerId = (int)CurrentTurn.currentPlayer;
            if (!playerManager.TryGetPlayer(playerId, out var player)) return false;

            foreach (var piece in player.RuntimeData.Pieces)
            {
                if (CharacterSkillRegistry.TryGet(playerId, piece.PieceId, out CharacterStatusBehaviour behaviour) &&
                    behaviour is CHAR_001_1_Status yunnuri && yunnuri.HasPendingForcedMove)
                {
                    pieceId = piece.PieceId;
                    forcedMoveOwner = yunnuri;
                    return true;
                }
            }
            return false;
        }
        // 이 플레이어가 지금까지 완주시킨 말이 몇 개인지 셈 (State == Goal)
        private int CountFinishedPieces(int playerId)
        {
            if (!playerManager.TryGetPlayer(playerId, out var player)) return 0;
            int count = 0;
            foreach (var piece in player.RuntimeData.Pieces)
                if (piece.IsFinished) count++;
            return count;
        }
        // ===================================================================
        // Escape 모드 전용: 이번에 완주한 말(들)을 다시 Waiting 상태로 되돌림
        // (재사용 가능하게). 위치는 영서가 나중에(UI 나오면) 조정 예정, 지금은 상태만 바꿔둠
        // ===================================================================
        private void ResetFinishedPiecesToWaiting(int playerId)
        {
            if (!playerManager.TryGetPlayer(playerId, out var player)) return;
            foreach (var piece in player.RuntimeData.Pieces)
            {
                if (piece.State == PieceState.Goal)
                {
                    piece.State = PieceState.Waiting;
                }
            }
        }
        // ===================================================================
        // 남은 pendingResults 중 하나라도 실제로 말을 움직일 수 있으면 false 를 돌려주고,
        // 아무 결과도 쓸 수 없으면(예: 판에 말이 하나도 없는데 뒷도만 남음) 즉시 턴을 종료한다.
        // 이 처리가 없으면 마커가 하나도 안 떠서 플레이어가 시간 초과까지 기다려야 한다.
        // 버그 수정: 예전엔 "말이 있냐"만 봐서, 판 위 말이 전부 기절/부품이면 화살표는 안 뜨는데
        // 턴도 안 넘어가서 45초를 기다렸음. 실제 이동 검사(CanPieceMove)와 같은 기준으로 판단함.
        // ===================================================================
        private void EndTurnIfNoUsableResult()
        {
            if (CurrentTurn.currentPhase != TurnPhase.WaitAction) return;
            if (HasAnyUsablePendingResult()) return;

            // 이번 턴에 잡기/스킬로 얻은 보너스 던지기가 남아 있으면, 못 쓰는 결과만 버리고 던지러 감
            if (pendingCaptureThrows > 0 || pendingSkillThrows > 0)
            {
                Debug.Log("[턴] 남은 윷 결과로 움직일 수 있는 말이 없어 버리고, 보너스 던지기로 넘어갑니다.");
                pendingResults.Clear();
                OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));
                SetPhase(TurnPhase.WaitThrow);
                return;
            }

            Debug.Log("[턴] 남은 윷 결과로 움직일 수 있는 말이 없어 턴을 종료합니다.");
            EndTurn();
        }

        private bool HasAnyUsablePendingResult()
        {
            if (pendingResults.Count == 0) return false;

            int playerId = (int)CurrentTurn.currentPlayer;
            bool hasBoardPiece = HasAnyMovablePiece(playerId, PieceState.InBoard);
            bool hasWaitingPiece = HasAnyMovablePiece(playerId, PieceState.Waiting);

            foreach (var throwData in pendingResults)
            {
                if (throwData.result == YutResult.BackDo)
                {
                    if (hasBoardPiece) return true; // 뒷도는 보드 위 말이 있어야 씀
                }
                else if (hasBoardPiece || hasWaitingPiece)
                {
                    return true; // 전진 결과는 대기 말이든 보드 위 말이든 있으면 사용 가능
                }
            }
            return false;
        }

        // 그 상태(판 위/대기)의 말 중에 실제로 움직일 수 있는(기절·부품·업힌 말 아님) 말이 있는지
        private bool HasAnyMovablePiece(int playerId, PieceState state)
        {
            if (!playerManager.TryGetPlayer(playerId, out var player)) return false;
            foreach (var piece in player.RuntimeData.Pieces)
            {
                if (piece.State == state && CanPieceMove(playerId, piece.PieceId))
                    return true;
            }
            return false;
        }

        // ===================================================================
        // 이 플레이어 말 중 하나라도 보드 위에(State == InBoard) 있는지 확인하는 함수
        // 뒷도(BackDo)를 쓸 수 있는지 판단하는 용도로 씀 - 전부 대기중(Waiting)이면
        // 뒤로 물러날 곳 자체가 없다는 뜻이라, 이 경우엔 뒷도를 쓸 수 없다고 판단함
        // ===================================================================
        private bool HasAnyPieceInBoard(int playerId)
        {
            if (!playerManager.TryGetPlayer(playerId, out var player)) return false;
            foreach (var piece in player.RuntimeData.Pieces)
            {
                if (piece.State == PieceState.InBoard)
                    return true;
            }
            return false;
        }
        //  이 말이 지금 이동 가능한 상태인지 (Stun이면 불가, 나머지는 가능)
        //  팀전에서 업힌 말(리더가 아닌 말)은 이동 불가하다는 규칙 추가
        // "이동과 스킬 사용은 리더 말만 가능, 업힌 말은 이동과 스킬 사용 불가능"
        private bool CanPieceMove(int playerId, int pieceId)
        {
            if (!playerManager.TryGetPlayer(playerId, out var player)) return false;
            if (!player.TryGetPieceData(pieceId, out var pieceData)) return false;
            if (!CcEffectService.CanMove(pieceData)) return false;

            //  업기 상태이고, 이 말이 리더(StackLeaderPieceId)가 아니면 이동 불가
            if (pieceData.IsStacked && pieceData.PieceId != pieceData.StackLeaderPieceId)
                return false;

            return true;
        }
        // 방금 이동으로 상대 말이 잡혔는지(Kill/Retire) 전체 상대 플레이어를 훑어서 확인.
        // 발견하면 그 즉시 CC를 지워서(ClearCc) "소비 완료" 처리함 (다음에 또 잡힌 걸로 착각 안 하게).
        // 반환값: Kill(추가턴 있는 잡기)로 잡힌 "팀 수" = 이번에 받을 잡기 보너스 던지기 횟수
        // 기획: "업은 말은 1회 / 다른 팀의 말 - 2팀의 말을 동시에 잡으면 2회"
        // (업힌 말은 리더만 Kill이라 1회, 팀전에서 같은 팀 두 명의 말을 같이 잡아도 1팀이라 1회)
        // 자폭 같은 스킬은 현재 플레이어 말의 Kill/Retire도 정리할 수 있습니다.
        private int ConsumeCaptureResults(
            int movingPlayerId,
            bool includeMovingPlayer = false)
        {
            var killedTeams = new HashSet<TeamSlot>();
            foreach (var otherPlayer in playerManager.ActivePlayers)
            {
                // 일반 이동은 기존처럼 상대 말만, 스킬은 필요할 때 모든 말을 검사합니다.
                if (!includeMovingPlayer && otherPlayer.PlayerId == movingPlayerId)
                    continue;

                foreach (var piece in otherPlayer.RuntimeData.Pieces)
                {
                    //수정: 잡기 마커만 소비하여 함께 존재하는 다른 효과를 지우지 않습니다.
                    if (CcEffectService.ConsumeCapture(piece))
                        killedTeams.Add(MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)otherPlayer.PlayerId));
                }
            }
            return killedTeams.Count;
        }
        private void EndTurn()
        {
            StopTurnTimer(); // 턴이 진짜로 끝나는 지점이니, 흐르고 있던 통합 타이머 정리
            // 윤누리 추가 1칸 이동을 안 누른 채 턴이 끝나면(시간 초과 등) 자동으로 이동시킴.
            // 턴이 끝나는 중이라 여기서 잡아도 보너스 던지기는 없고, 잡기 표시만 정리함.
            // 순위 확정으로 게임에서 빠진 사람은 추가 이동도 안 함.
            if (!CurrentTurn.isGameEnded && !IsCurrentPlayerOutOfTurnOrder() &&
                TryGetPendingForcedMove(out _, out CHAR_001_1_Status forcedMoveOwner))
            {
                forcedMoveOwner.ExecutePendingForcedMove();
                ConsumeCaptureResults((int)CurrentTurn.currentPlayer);
            }
            SetPhase(TurnPhase.TurnEnd);
            OnTurnEnded?.Invoke(CurrentTurn.currentPlayer);
            if (CurrentTurn.isGameEnded)
            {
                SetPhase(TurnPhase.GameEnd);
                return;
            }
            AdvanceToNextPlayer();
        }
        // 최대 턴수 도달 처리를 이미 했는지(한 게임당 한 번만). Classic/KillTheKing처럼
        // HandleTimeLimitReached가 게임을 끝내지 않는 모드에서, 매 턴마다 같은 경고 로그가
        // 무한 반복되는 걸 막는다. 새 게임 시작 시(StartFirstTurn) false로 초기화됨.
        private bool maxTurnLimitHandled;
        // 라운드 계산용: 게임 시작 때 확정된 순서를 그대로 기억해둠 (중간에 사람이 빠져도 안 바뀜)
        private List<PlayerSlot> seatOrder = new List<PlayerSlot>();

        // 다음 사람 차례로 넘김
        private void AdvanceToNextPlayer()
        {
            PlayerSlot previousPlayer = CurrentTurn.currentPlayer; // 방금 턴이 끝난 사람 (이미 순서에서 빠졌을 수도 있음)
            TurnOrder.currentIndex = (TurnOrder.currentIndex + 1) % TurnOrder.order.Count;
            bool isNewRound = IsNewRound(previousPlayer, TurnOrder.Current);
            CurrentTurn.turnNumber++;        // 턴 진행될 때마다 무조건 +1

            // 기획(10/17): 턴 수는 "4명이 다 돌면 1턴" = 라운드 기준.
            // "15턴의 마지막 플레이어가 턴이 끝났을 때" 판정 -> 마지막 라운드(15라운드)까지는 전원이 실제로 플레이함.
            // 버그 수정: 예전엔 라운드를 16으로 먼저 올린 뒤 판정해서 게임이 멈출 때 화면에 "16/15"가 떴음.
            // 그래서 라운드를 올리기 전에 "방금 끝난 게 마지막 라운드였는지" 먼저 확인하고, 마지막이면 15에서 멈춤
            if (isNewRound && !maxTurnLimitHandled && settings != null && settings.maxTurnCount > 0 &&
                CurrentTurn.roundNumber >= settings.maxTurnCount)
            {
                maxTurnLimitHandled = true;   // 이 게임에서 다시는 안 찍음 (Classic 등 게임이 안 끝나는 모드 대비)
                Debug.Log("[턴제한] 최대 턴수(" + settings.maxTurnCount + ") 도달, 지금까지 점수로 승부 판정");
                winConditionManager.HandleTimeLimitReached();
                if (CurrentTurn.isGameEnded) return; // 턴제한 함수
            }
            if (isNewRound) CurrentTurn.roundNumber++;

            BeginTurnFor(TurnOrder.Current);
        }

        // ===================================================================
        // 버그 수정: 예전엔 "순서 목록 0번 자리로 돌아오면" 라운드 +1 이었는데, 0번 자리 사람이
        // 자기 차례에 목표를 채워 빠지면 다음 사람이 새 0번이 돼서, 한 바퀴가 안 끝났는데도
        // 라운드가 +1 됐음 (라운드 하나가 그냥 날아감).
        // 그래서 게임 시작 때 순서(seatOrder) 기준으로 "다음 사람이 방금 사람보다 앞자리(또는 같은 사람)면"
        // 한 바퀴 돈 걸로 셈. 누가 빠져도 처음 자리 번호는 안 바뀌어서 정확함.
        // ===================================================================
        private bool IsNewRound(PlayerSlot previousPlayer, PlayerSlot nextPlayer)
        {
            int previousSeat = seatOrder.IndexOf(previousPlayer);
            int nextSeat = seatOrder.IndexOf(nextPlayer);
            if (previousSeat < 0 || nextSeat < 0)
                return TurnOrder.currentIndex == 0; // 방어 처리: 자리 정보가 없으면 예전 방식
            return nextSeat <= previousSeat;
        }

        // 지금 차례인 사람이 턴 순서에서 빠졌는지 (다인전 순위 확정 / 탈주)
        private bool IsCurrentPlayerOutOfTurnOrder()
        {
            return !TurnOrder.order.Contains(CurrentTurn.currentPlayer);
        }

        // 게임에서 빠진 사람의 남은 윷 결과/보너스 던지기를 전부 버리고 바로 턴을 끝냄
        private void DiscardRemainingAndEndTurn()
        {
            ConsumeCaptureResults((int)CurrentTurn.currentPlayer); // 이번 이동으로 잡은 표시만 정리 (보너스 없음)
            pendingResults.Clear();
            pendingCaptureThrows = 0;
            pendingSkillThrows = 0;
            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));
            Debug.Log("[턴] " + CurrentTurn.currentPlayer + " 순위 확정으로 게임에서 빠짐 -> 남은 결과 버리고 턴 종료");
            EndTurn();
        }

        // ===================================================================
        // 버그 수정: 스킬 이동으로 목표 점수를 채우면 순서에서는 빠지는데(스킬 결과 처리는 건희 코드),
        // 턴이 안 끝나서 빠진 사람이 남은 윷/던지기로 계속 플레이할 수 있었음.
        // 그래서 던지기/이동 요청이 들어올 때 "이미 빠진 사람이면" 남은 결과를 버리고 턴을 끝냄.
        // 반환값: 턴을 끝냈으면 true (요청한 쪽은 바로 return 해야 함)
        // ===================================================================
        private bool EndTurnIfCurrentPlayerOutOfTurnOrder()
        {
            if (CurrentTurn.isGameEnded || !IsCurrentPlayerOutOfTurnOrder()) return false;
            if (CurrentTurn.currentPhase != TurnPhase.WaitThrow &&
                CurrentTurn.currentPhase != TurnPhase.WaitAction) return false;

            DiscardRemainingAndEndTurn();
            return true;
        }
        // WinConditionManager가 승리 조건을 확인했을 때 호출해서 부탁하는 함수
        // (CurrentTurn은 이 클래스만 바꿀 수 있어서, 다른 클래스는 직접 못 바꾸고 이 함수로 부탁함)
        // 결과 팝업이 떠 있는 동안 뒤에서 게임이 계속 굴러가면 안 되므로, 플래그만 세우지 않고
        // 즉시 턴 타이머를 멈추고 단계를 GameEnd로 바꿔 던지기/이동 요청을 전부 차단한다.
        public void NotifyGameEnded()
        {
            CurrentTurn.isGameEnded = true;
            if (CurrentTurn.currentPhase == TurnPhase.GameEnd) return; // 중복 호출 방어
            StopTurnTimer();
            SetPhase(TurnPhase.GameEnd);
        }
        // ===================================================================
        //  (캐릭터/스킬) : 스킬 효과로 재던지기를 부여했을 때 호출할 함수
        // (예: 기본형 [한번더] 스킬 사용 시 캐릭터 코드가 이 함수를 부름)
        // 윷/모로 얻는 추가 던지기(최대 3회 제한)와는 별도 카운트라서, 여기 카운트는 제한 없음
        //  스킬로 인한 추가 던지기도 통합 타이머에 시간 보너스를 더함
        // ===================================================================
        public void GrantSkillExtraThrow()
        {
            pendingSkillThrows++;
            AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds);

            // QA 수정: 유가람 [한 번 더]처럼 이동 단계에서 던지기를 주는 스킬은 쓰는 즉시 던지기 단계로 돌아감.
            // (가이드: "재던지기를 부여하는 스킬을 사용한 경우 → [윷 던지기] 단계로 복귀")
            // 예전엔 남은 윷 결과를 다 써야 던질 수 있었음. 남은 결과는 그대로 두고, 다 던진 뒤 한꺼번에 이동.
            // 윤누리 추가 1칸 이동이 대기 중이면 그걸 먼저 끝내야 해서 단계는 안 바꿈.
            if (CurrentTurn != null && !CurrentTurn.isGameEnded &&
                CurrentTurn.currentPhase == TurnPhase.WaitAction &&
                !TryGetPendingForcedMove(out _))
            {
                SetPhase(TurnPhase.WaitThrow);
            }
        }

        /// <summary>
        /// Character code reports only the generic result of a completed active skill.
        /// The turn manager remains responsible for consuming capture markers and
        /// scheduling any capture bonus throw.
        /// </summary>
        // 액티브 스킬의 잡기 결과와 추가 던지기를 턴 흐름에 반영합니다.
        public void ResolveSkillResult(bool suppressExtraThrow)
        {
            if (CurrentTurn == null || CurrentTurn.currentPlayer == PlayerSlot.None)
                return;

            int captureBonusCount = ConsumeCaptureResults(
                (int)CurrentTurn.currentPlayer,
                includeMovingPlayer: true);
            if (captureBonusCount > 0 && !suppressExtraThrow)
            {
                pendingCaptureThrows += captureBonusCount; // 잡은 팀 수만큼
                CurrentTurn.extraThrowByCaptureCount += captureBonusCount;
                AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds * captureBonusCount); //  스킬로 인한 잡기 보너스도 시간 더해줌
            }

            // 버그 수정: 스킬로 말이 움직이거나 잡혀도(예: 사무라이 일섬으로 개 → 모) 윷 결과/턴 단계가 안 바뀌어서
            // 네비게이팅이 스킬 전 위치 기준 화살표(개+개 = 윷 자리)를 그대로 보여줬음.
            // 남은 결과를 다시 방송해서 네비게이팅이 현재 위치 기준으로 말 선택부터 새로 계산하게 함.
            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));
        }

        // 스킬의 SP/쿨다운과 결과 반영을 마친 뒤 현재 플레이어의 턴을 종료합니다.
        public bool EndTurnAfterSkill(int expectedPlayerId)
        {
            if (CurrentTurn == null || CurrentTurn.isGameEnded ||
                CurrentTurn.currentPhase != TurnPhase.WaitAction ||
                (int)CurrentTurn.currentPlayer != expectedPlayerId)
                return false;

            pendingResults.Clear();
            OnPendingResultsChanged?.Invoke(new List<YutThrowData>(pendingResults));
            EndTurn();
            return true;
        }

        // ===================================================================
        //  항복/탈주 처리용: 팀 전체가 아니라 "이 사람 한 명만" 턴 순서에서 뺌
        // (팀전에서 한 명만 나가고 팀원은 남아있는 경우 씀. 팀원끼리는 남은 사람들끼리
        //  기존 순서 그대로 돌게 되는데, 그게 자연스럽게 "인원비율대로 턴 배분"이 됨.
        //  예: 2vs2에서 상대팀 1명 나가면 순서가 [상대1, 나1, 나2]가 되고, 이걸 그대로
        //  돌리면 상대1이 2번에 1번꼴로 도는 셈이라 "상대2:나1" 비율이 저절로 만들어짐)
        // ===================================================================
        public void RemovePlayerFromTurnOrder(PlayerSlot player)
        {
            var remainingPlayers = TurnOrder.order.Where(p => p != player).ToList();
            PlayerSlot currentBefore = TurnOrder.Current;
            int newIndex = ResolveIndexAfterRemoval(remainingPlayers, currentBefore);
            TurnOrder = new TurnOrderData
            {
                order = remainingPlayers,
                currentIndex = newIndex
            };
        }

        // ===================================================================
        // 버그 수정: 지금 순서 목록에서 몇 명이 빠졌을 때, "다음에 currentIndex가 될 값"을 정확히 계산.
        // - 지금 차례인 사람이 그대로 남아있으면 -> 그 사람의 새 자리로 그대로 매핑 (기존과 동일)
        // - 지금 차례인 사람 본인이 빠진 경우 -> 예전엔 그냥 0(목록 맨 앞)으로 가서, 원래 있던 순서상
        //   "다음 사람"이 아니라 엉뚱한 사람이 다음 차례가 될 수 있었음. 게다가 이 함수 직후 보통
        //   EndTurn() -> AdvanceToNextPlayer()가 자동으로 한 번 더 +1 하기 때문에, 여기서 "다음 사람"
        //   자리를 그대로 넣으면 그 다음 사람까지 건너뛰는 이중 진행 버그가 생겼음.
        //   그래서 원래 순서 기준으로 "다음에 와야 할, 아직 안 빠진 사람"을 찾은 뒤, 그 사람 자리보다
        //   한 칸 전을 가리키게 해서 뒤이은 자동 +1 과 정확히 맞아떨어지게 만든다.
        // ===================================================================
        private int ResolveIndexAfterRemoval(List<PlayerSlot> remainingPlayers, PlayerSlot currentBefore)
        {
            if (remainingPlayers.Count == 0) return 0;

            int foundIndex = remainingPlayers.IndexOf(currentBefore);
            if (foundIndex >= 0) return foundIndex; // 지금 차례인 사람이 안 빠졌으면 그대로 매핑

            List<PlayerSlot> oldOrder = TurnOrder.order;
            int oldIndex = oldOrder.IndexOf(currentBefore);
            if (oldIndex < 0) return 0; // 이론상 안 일어나는 방어 처리

            for (int step = 1; step <= oldOrder.Count; step++)
            {
                PlayerSlot candidate = oldOrder[(oldIndex + step) % oldOrder.Count];
                int candidateNewIndex = remainingPlayers.IndexOf(candidate);
                if (candidateNewIndex < 0) continue; // 이 사람도 이번에 같이 빠졌으면 건너뜀

                // "다음 사람" 자리보다 한 칸 전 -> 곧이어 실행될 AdvanceToNextPlayer()의 +1과 상쇄됨
                return (candidateNewIndex - 1 + remainingPlayers.Count) % remainingPlayers.Count;
            }
            return 0;
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
                .Where(p => MatchCompositionRule.GetTeamSlot(gameSettings, p) != team)
                .ToList();
            PlayerSlot currentBefore = TurnOrder.Current;
            int newIndex = ResolveIndexAfterRemoval(remainingPlayers, currentBefore);
            TurnOrder = new TurnOrderData
            {
                order = remainingPlayers,
                currentIndex = newIndex
            };
        }
        // 턴 단계를 바꾸는 유일한 통로. 값 바꾸기 + 방송하기를 항상 같이 실행되게 묶어둠
        private void SetPhase(TurnPhase phase)
        {
            CurrentTurn.currentPhase = phase;
            OnTurnPhaseChanged?.Invoke(CurrentTurn);
        }
        // ===================================================================
        // 턴 전체(던지기+이동+스킬)를 하나의 타이머로 재는 방식
        // "윷 던지기, 말 이동과 스킬 사용에 정해진 시간 45초 + 추가 던지기가 나오면 15초 추가"
        // 그래서 턴 시작할 때 딱 한 번만 타이머를 켜고, 추가 던지기(윷/모/잡기/스킬)가 생길 때마다
        // "시간을 더해주기"만 하고 타이머 자체는 절대 다시 시작하지 않음 (턴이 끝날 때만 멈춤)
        // settings.turnTimeMode가 Unlimited면 아예 타이머를 안 켬
        // ===================================================================
        private Coroutine turnTimerCoroutine;
        private float turnTimerRemainingSeconds; // 지금 이 턴에 남은 시간(초). 매 프레임 줄어들고, 보너스 생기면 더해짐

        public bool IsTurnTimerLimited =>
            settings != null && settings.turnTimeMode == TurnTimeMode.Limited;

        public float RemainingTurnSeconds =>
            IsTurnTimerLimited && turnTimerCoroutine != null
                ? Mathf.Max(0f, turnTimerRemainingSeconds)
                : 0f;

        // 턴 시작마다 딱 한 번만 호출됨 (BeginTurnFor에서)
        private void StartTurnTimer()
        {
            if (settings == null || settings.turnTimeMode != TurnTimeMode.Limited) return; // 무제한 모드면 시작 안 함
            StopTurnTimer(); // 혹시 이전 턴 타이머가 안 꺼져있으면 정리하고 새로 시작
            turnTimerRemainingSeconds = settings.throwTimeSeconds; // 기본 45초 (settings.throwTimeSeconds를 "턴 전체 기본시간"으로 사용)
            turnTimerCoroutine = StartCoroutine(TurnTimeoutRoutine());
        }

        // 추가 던지기(윷/모/잡기/스킬)가 생길 때마다 호출. 타이머를 다시 시작하지 않고, 남은 시간에
        // 그냥 더해주기만 함 (이미 흐르고 있던 시간은 그대로 유지됨)
        private void AddTurnTimerBonus(float bonusSeconds)
        {
            if (turnTimerCoroutine == null) return; // 무제한 모드거나 타이머가 이미 꺼진 상태면 아무것도 안 함
            turnTimerRemainingSeconds += bonusSeconds;
        }

        // 지금 돌고 있는 턴 타이머가 있으면 멈춤 (턴이 진짜로 끝날 때만 호출)
        private void StopTurnTimer()
        {
            if (turnTimerCoroutine != null)
            {
                StopCoroutine(turnTimerCoroutine);
                turnTimerCoroutine = null;
            }
        }

        // 매 프레임마다 남은 시간을 깎다가, 0 이하가 되면 그 시점에 뭘 하고 있었든 강제로 턴 종료
        private IEnumerator TurnTimeoutRoutine()
        {
            while (turnTimerRemainingSeconds > 0f)
            {
                yield return null; // 한 프레임 기다림
                turnTimerRemainingSeconds -= Time.deltaTime;
            }
            turnTimerCoroutine = null;
            Debug.Log("[타이머] 턴 제한시간 초과, 턴 강제 종료");
            EndTurn();
        }
    }
}
