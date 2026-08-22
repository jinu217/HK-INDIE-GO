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
        // 잡기로 얻은 보너스 던지기 횟수 저장( 던진 윷 결과를 다 소모하고 보너스 던지기를 하니까)
        private int pendingCaptureThrows = 0;
        // 스킬로 얻은 보너스 던지기 횟수 저장 (예: 기본형 "한번더")
        // 잡기(pendingCaptureThrows)랑 따로 관리하는 이유: 나중에 "왜 추가 던지기가 생겼는지"
        // (잡아서인지 스킬 때문인지) UI에서 구분해서 보여줘야 할 수도 있어서.
        private int pendingSkillThrows = 0;
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
                .GroupBy(p => MatchCompositionRule.GetTeamSlot(settings.matchComposition, p))
                .ToDictionary(g => g.Key, g => g.OrderBy(p => (int)p).ToList());

            var representatives = playersByTeam.Select(kv => kv.Value[0]).ToList();

            // 팀 순위(어느 팀이 먼저인지)만 뽑아둠
            var teamOrder = DetermineOrder(representatives)
                .Select(rep => MatchCompositionRule.GetTeamSlot(settings.matchComposition, rep))
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
            CurrentTurn.currentTeam = MatchCompositionRule.GetTeamSlot(settings.matchComposition, player);
            pendingResults.Clear();
            pendingCaptureThrows = 0;
            pendingSkillThrows = 0; // 새 턴 시작이니 스킬 보너스 던지기도 초기화
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
            foreach (var piece in playerController.RuntimeData.Pieces)
            {
                if (piece.CurrentCc == CcDefine.None) continue;
                if (piece.CurrentCc == CcDefine.Kill || piece.CurrentCc == CcDefine.Retire) continue; // 일회성이라 제외
                if (piece.RemainingCcTurns <= 0)
                {
                    piece.ClearCc(); // 혹시 턴수가 이미 0인데 안 풀려있었으면 여기서 정리
                    continue;
                }
                int newRemaining = piece.RemainingCcTurns - 1;
                if (newRemaining <= 0)
                    piece.ClearCc(); // 턴수 다 지났으니 CC 해제
                else
                    piece.SetCc(piece.CurrentCc, newRemaining); // 아직 남았으면 턴수만 줄여서 다시 세팅
            }
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
            if (CurrentTurn.currentPhase != TurnPhase.WaitThrow)
            {
                Debug.LogWarning("지금은 윷을 던질 수 있는 단계가 아님: " + CurrentTurn.currentPhase);
                return;
            }
            // 통합 타이머 : 턴 시작할 때 한 번 켜지면 계속 흐르고, 턴이 끝날 때만 멈춤
            // 잡기 보너스를 먼저 소비하고, 없으면 스킬 보너스를 소비함 
            bool isCaptureBonusThrow = pendingCaptureThrows > 0;
            bool isSkillBonusThrow = !isCaptureBonusThrow && pendingSkillThrows > 0;
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
            if (isCaptureBonusThrow)
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
                SetPhase(TurnPhase.WaitThrow);
                //  타이머를 재시작하지 않고, 남은 시간에 보너스만 더함(추가 던지기 보너스 시간)
                AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds);
                return;
            }
            SetPhase(TurnPhase.WaitAction);
            //  이동 단계로 넘어가도 타이머는 이미 계속 흐르고 있어서 따로 시작 안 함
        }
        // UI에서 플레이어가 [결과 묶음] 중 하나를 골라(chosenResult), 어떤 말을 옮길지(pieceId) 정하면 호출됨
        // pieceId, chosenResult는 이 함수의 매개변수 - UI가 호출할 때 직접 넣어주는 값
        // 이 함수 안에서 검사 -> 이동요청 -> 결과 확인 -> 다음 단계 결정까지 한번에 처리함
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
            SetPhase(TurnPhase.ResolveTile); // 도착 칸 처리 단계로 표시 (특수효과는 아직 미구현)
            SetPhase(TurnPhase.ResolveBoardRule); // 잡기/업기/완주 결과 처리 단계로 표시
                                                  // 완주했는지 확인은 여기서 안 하고, WinConditionManager한테 결과를 넘겨서 대신 확인시킴
            int finishedCountAfter = CountFinishedPieces(playerId);
            int newlyFinishedCount = finishedCountAfter - finishedCountBefore;
            bool isFinished = newlyFinishedCount > 0;
            winConditionManager.OnPieceMoveResolved(
                CurrentTurn.currentPlayer, CurrentTurn.currentTeam, isFinished, newlyFinishedCount);

            // ===================================================================
            // Escape 모드 전용: 완주한 말은 재사용해야 하므로, 상태를 다시
            // 대기(Waiting)로 되돌림. 영서 확인: "위치는 나중에 조정, 지금은 상태만 바꾸면 됨"
            // ===================================================================
            if (isFinished && settings.gameMode == GameMode.Escape)
            {
                ResetFinishedPiecesToWaiting(playerId);
            }
            SetPhase(TurnPhase.CheckBonusThrow); // 잡기로 보너스 던지기 생겼는지 확인하는 단계로 표시
                                                 //  상대 말들의 CC(Kill/Retire)를
                                                 // 직접 훑어서 확인함. 윷/모(4~5칸)로 잡으면 Retire(추가턴 없음),
                                                 // 도~걸/뒷도(1~3칸,-1칸)로 잡으면 Kill(추가턴 있음)
            bool gotKillCapture = ConsumeCaptureResults(playerId);
            if (gotKillCapture) // 이번 이동으로 상대 말을 Kill로 잡았으면
            {
                pendingCaptureThrows++;             // 나중에 쓸 보너스 던지기 개수 +1 (지금 바로 던지는 거 아님)
                CurrentTurn.extraThrowByCaptureCount++;
                AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds); //  잡기 보너스도 시간 더해줌
            }
            // 아직 안 쓴 윷 결과가 남아있으면 -> 계속 말 이동시키는 단계로 돌아감
            if (pendingResults.Count > 0)
            {
                SetPhase(TurnPhase.WaitAction);
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
            if (pieceData.CurrentCc == CcDefine.Stun) return false;

            //  업기 상태이고, 이 말이 리더(StackLeaderPieceId)가 아니면 이동 불가
            if (pieceData.IsStacked && pieceData.PieceId != pieceData.StackLeaderPieceId)
                return false;

            return true;
        }
        // 방금 이동으로 상대 말이 잡혔는지(Kill/Retire) 전체 상대 플레이어를 훑어서 확인.
        // 발견하면 그 즉시 CC를 지워서(ClearCc) "소비 완료" 처리함 (다음에 또 잡힌 걸로 착각 안 하게).
        // 반환값: Kill(추가턴 있는 잡기)이 하나라도 있었으면 true
        // 자폭 같은 스킬은 현재 플레이어 말의 Kill/Retire도 정리할 수 있습니다.
        private bool ConsumeCaptureResults(
            int movingPlayerId,
            bool includeMovingPlayer = false)
        {
            bool gotKill = false;
            foreach (var otherPlayer in playerManager.ActivePlayers)
            {
                // 일반 이동은 기존처럼 상대 말만, 스킬은 필요할 때 모든 말을 검사합니다.
                if (!includeMovingPlayer && otherPlayer.PlayerId == movingPlayerId)
                    continue;

                foreach (var piece in otherPlayer.RuntimeData.Pieces)
                {
                    if (piece.CurrentCc == CcDefine.Kill)
                    {
                        gotKill = true;
                        piece.ClearCc();
                    }
                    else if (piece.CurrentCc == CcDefine.Retire)
                    {
                        piece.ClearCc();
                    }
                }
            }
            return gotKill;
        }
        private void EndTurn()
        {
            StopTurnTimer(); // 턴이 진짜로 끝나는 지점이니, 흐르고 있던 통합 타이머 정리
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
        //  (캐릭터/스킬) : 스킬 효과로 재던지기를 부여했을 때 호출할 함수
        // (예: 기본형 [한번더] 스킬 사용 시 캐릭터 코드가 이 함수를 부름)
        // 윷/모로 얻는 추가 던지기(최대 3회 제한)와는 별도 카운트라서, 여기 카운트는 제한 없음
        //  스킬로 인한 추가 던지기도 통합 타이머에 시간 보너스를 더함
        // ===================================================================
        public void GrantSkillExtraThrow()
        {
            pendingSkillThrows++;
            AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds);
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

            bool gotKillCapture = ConsumeCaptureResults(
                (int)CurrentTurn.currentPlayer,
                includeMovingPlayer: true);
            if (!gotKillCapture || suppressExtraThrow)
                return;

            pendingCaptureThrows++;
            CurrentTurn.extraThrowByCaptureCount++;
            AddTurnTimerBonus(GameRuleDefine.ExtraThrowTimeBonusSeconds); //  스킬로 인한 잡기 보너스도 시간 더해줌
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
            int newIndex = remainingPlayers.IndexOf(currentBefore);
            if (newIndex < 0) newIndex = 0;
            TurnOrder = new TurnOrderData
            {
                order = remainingPlayers,
                currentIndex = remainingPlayers.Count > 0 ? newIndex : 0
            };
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
        // ===================================================================
        // 턴 전체(던지기+이동+스킬)를 하나의 타이머로 재는 방식
        // "윷 던지기, 말 이동과 스킬 사용에 정해진 시간 45초 + 추가 던지기가 나오면 15초 추가"
        // 그래서 턴 시작할 때 딱 한 번만 타이머를 켜고, 추가 던지기(윷/모/잡기/스킬)가 생길 때마다
        // "시간을 더해주기"만 하고 타이머 자체는 절대 다시 시작하지 않음 (턴이 끝날 때만 멈춤)
        // settings.turnTimeMode가 Unlimited면 아예 타이머를 안 켬
        // ===================================================================
        private Coroutine turnTimerCoroutine;
        private float turnTimerRemainingSeconds; // 지금 이 턴에 남은 시간(초). 매 프레임 줄어들고, 보너스 생기면 더해짐

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