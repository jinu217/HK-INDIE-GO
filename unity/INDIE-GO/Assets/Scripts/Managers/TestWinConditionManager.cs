using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers.GameProgress;
namespace YutArena.Managers
{
    // 완주/탈출 여부를 집계해서 승리 조건을 판정. 완주 "판정"(좌표 계산 등) 자체는 보드 쪽이 하고,
    // 여기서는 TurnManager가 넘겨주는 isFinished 값만 받아서 카운트하고 승패를 확인함
    // (예전엔 BoardMoveResult를 통째로 받았는데, 영서 새 구조로 바뀌면서 TurnManager가
    //  필요한 값만 뽑아서 넘겨주는 방식으로 변경함)
    public class TestWinConditionManager : MonoBehaviour
    {
        [SerializeField] private TestGameManager gameManager;
        [SerializeField] private TestTurnManager turnManager;
        [SerializeField] private TestYutRuleManager yutRuleManager; // 동점 타이브레이커 던지기용
        [SerializeField] private PlayerManager playerManager; // Classic 모드가 말 상태 직접 확인할 때 필요
        [SerializeField] private PieceMovementManager pieceMovementManager; // 참먹이 거리 계산 호출용
        // 모드별 승리 규칙 스크립트들을 인스펙터에서 드래그로 연결 (ClassicModeRule, EscapeModeRule 등)
        [SerializeField] private List<MonoBehaviour> modeRuleSources;
        // 위 목록을 "어느 모드가 어느 규칙인지" 바로 찾을 수 있게 정리해둔 표
        private Dictionary<GameMode, IGameModeRule> modeRules = new Dictionary<GameMode, IGameModeRule>();
        private GameStartSettings settings; // 대기실에서 정한 모드/목표탈출수 등을 기억해둠
        // 팀(TeamSlot)마다 지금까지 완주한 말 개수(int)를 저장하는 표
        private readonly Dictionary<TeamSlot, int> escapeCountByTeam = new Dictionary<TeamSlot, int>();
        // ===================================================================
        // 다인전 순위 시스템용 (기획서: "최후 2인이 남을 때까지 계속 진행, 등수 표시")
        // activeTeams  = 아직 게임에서 안 빠진 팀들 (목표 달성 안 한 팀들)
        // finishedRanking = 목표를 먼저 채우고 빠진 팀들이 "빠진 순서대로" 쌓이는 리스트 (1등부터)
        // ===================================================================
        private HashSet<TeamSlot> activeTeams = new HashSet<TeamSlot>();
        private List<TeamSlot> finishedRanking = new List<TeamSlot>();
        // 팀별 완주 수가 바뀔 때마다 UI(점수판 등)에 알려주기 위한 이벤트
        public System.Action<TeamSlot, int> OnEscapeCountChanged;
        // GameManager.StartGame()에서 호출됨. 새 게임 시작하니까 이전 판 점수 기록은 초기화
        public void Initialize(GameStartSettings gameSettings)
        {
            settings = gameSettings;
            escapeCountByTeam.Clear();
            finishedRanking.Clear();
            leftPlayers.Clear();
            eliminatedTeams.Clear();
            awaitingContinueDecision = false;
            pendingLeaveTeam = TeamSlot.None;
            // 인스펙터에 꽂아둔 모드 룰 스크립트들을 IGameModeRule로 형변환해서,
            // "이 모드는 이 룰"이라고 찾기 쉽게 표(Dictionary)로 정리해둠 (최초 1회만 하면 됨)
            if (modeRules.Count == 0 && modeRuleSources != null)
            {
                foreach (var source in modeRuleSources)
                {
                    if (source is IGameModeRule rule)
                        modeRules[rule.Mode] = rule;
                    else
                        Debug.LogWarning("TestWinConditionManager: modeRuleSources에 IGameModeRule을 구현하지 않은 항목이 있음 - " + source);
                }
            }
            // 참가자 전체(1~playerCount)를 훑으면서, 실제로 존재하는 팀들을 다 모아서 activeTeams에 넣음
            // (예: 1vs1vs1vs1이면 팀 4개, 2vs2면 팀 2개)
            activeTeams.Clear();
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                var team = MatchCompositionRule.GetTeamSlot(settings.matchComposition, (PlayerSlot)i);
                if (team != TeamSlot.None)
                    activeTeams.Add(team);
            }
        }
        // TurnManager가 말 이동을 처리한 직후마다 호출해주는 함수.
        // "매 득점 시 반드시 실행", 이동이 일어날 때마다 무조건 호출됨
        // (isFinished: 이번 이동으로 완주가 발생했는지 / finishedCount: 몇 마리가 완주했는지, 업기 포함)
        public void OnPieceMoveResolved(PlayerSlot player, TeamSlot team, bool isFinished, int finishedCount)
        {
            if (!isFinished) return; // 완주가 아니면 여기서 할 일 없음
            AddEscapeCount(team, finishedCount);
            CheckWinCondition(team); // 점수 올랐으니 승리 조건 채웠는지 바로 확인
        }
        // 특정 팀의 완주 카운트를 amount만큼 늘리고 바뀐 값을 알려주는 함수
        private void AddEscapeCount(TeamSlot team, int amount)
        {
            if (!escapeCountByTeam.ContainsKey(team)) escapeCountByTeam[team] = 0;
            escapeCountByTeam[team] += amount;
            OnEscapeCountChanged?.Invoke(team, escapeCountByTeam[team]);
        }
        // 지금 카운트를 기준으로 이 팀이 승리 조건을 만족했는지 확인
        // if/else로 모드별 계산을 직접 하지 않고, 지금 모드에 맞는 룰(IGameModeRule)을 찾아서 그쪽에 물어봄
        private void CheckWinCondition(TeamSlot team)
        {
            if (settings == null) return;
            if (!modeRules.TryGetValue(settings.gameMode, out var rule))
            {
                Debug.LogWarning("TestWinConditionManager: " + settings.gameMode + " 모드에 연결된 룰이 없음");
                return;
            }
            bool won = rule.CheckWin(team, escapeCountByTeam, settings, playerManager); //  playerManager도 같이 넘김 (Classic 모드가 말 상태 직접 확인하려고 필요해짐)
            if (won)
                HandleTeamFinished(team); // 예전엔 여기서 바로 Declare 했는데, 다인전 순위 처리를 위해 분리
        }
        // ===================================================================
        // 목표를 채운 팀이 나왔을 때: "이게 게임을 완전히 끝낼 상황인지, 아니면 이 팀만 빠지고
        // 계속 진행할 상황인지"를 판단하는 함수 (다인전 순위 시스템)
        // ===================================================================
        private void HandleTeamFinished(TeamSlot team)
        {
            if (!activeTeams.Contains(team)) return; // 이미 처리된 팀이면 무시 
            if (activeTeams.Count <= 2)
            {
                // 남은 팀이 2개 이하 -> 이게 사실상 마지막 대결. 여기서 진짜로 게임을 끝냄
                Declare(team);
            }
            else
            {
                // 아직 3팀 이상 남음 -> 이 팀은 순위만 기록하고, 게임은 계속 진행
                finishedRanking.Add(team);
                activeTeams.Remove(team);
                // 턴 순서에서 이 팀 플레이어들을 빼달라고 TurnManager한테 부탁
                turnManager.RemovePlayersOfTeam(team, settings);
                Debug.Log(team + " 목표 달성! " + finishedRanking.Count + "등으로 기록, 남은 " + activeTeams.Count + "팀으로 게임 계속");
            }
        }
        // 승리를 확정짓고 TurnManager, GameManager에 알리는 함수 (진짜로 게임이 끝날 때만 호출됨)
        // resultType을 매개변수로 받게 함 (기본값 TeamWin) - 항복/탈주로 끝난 경우 Surrender로 구분하기 위함
        private void Declare(TeamSlot winningTeam, GameResultType resultType = GameResultType.TeamWin)
        {
            turnManager.NotifyGameEnded();
            // 최종 순위 구성: 먼저 빠진 팀들(finishedRanking, 완주로 먼저 끝난 순) 뒤에 -> 이번 승자
            // -> 마지막까지 남았던 나머지 팀 -> 맨 끝에 탈주로 탈락한 팀들(나중에 나간 팀일수록 그나마 나은 등수)
            var finalRanking = new List<TeamSlot>(finishedRanking);
            finalRanking.Add(winningTeam);
            foreach (var t in activeTeams)
            {
                if (t != winningTeam)
                    finalRanking.Add(t); // 마지막 대결에서 진 팀 (꼴찌 후보)
            }
            // eliminatedTeams는 먼저 나간 팀이 리스트 앞쪽에 쌓여있으므로, 뒤집어서 붙이면 "가장 먼저 나간 팀이 제일 마지막(꼴찌)"가 됨
            for (int i = eliminatedTeams.Count - 1; i >= 0; i--)
                finalRanking.Add(eliminatedTeams[i]);
            var result = new GameResultData
            {
                resultType = resultType,
                winningTeam = winningTeam,
                winningPlayer = FindSoloWinningPlayer(winningTeam), // winningTeam 소속이 1명뿐이면(개인전) 그 사람을, 팀전이면 None을 채움
                finalRanking = finalRanking
            };
            gameManager.EndGame(result);
        }
        // ===================================================================
        // winningTeam 소속 플레이어를 세어봐서, 딱 1명이면 개인전으로 판단하고 그 사람을 반환.
        // 팀전(소속 2명 이상)이면 PlayerSlot.None을 반환함 (winningPlayer는 개인전 전용 필드라서)
        // UI가 결과창에서 "OO님 승리"(개인전) 표시를 할지 "TeamA 승리"(팀전) 표시를 할지 구분할 때 씀
        // ===================================================================
        private PlayerSlot FindSoloWinningPlayer(TeamSlot winningTeam)
        {
            PlayerSlot found = PlayerSlot.None;
            int memberCount = 0;
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                var p = (PlayerSlot)i;
                if (MatchCompositionRule.GetTeamSlot(settings.matchComposition, p) != winningTeam) continue;
                memberCount++;
                found = p;
            }
            return memberCount == 1 ? found : PlayerSlot.None;
        }
        // ===================================================================
        //  항복/탈주 처리 
        // - 1vs1(팀 인원 1명씩): 나가면 그 팀은 바로 전멸 -> 상대 자동 승리 (즉시 패배)
        // - 팀전(팀 인원 2명 이상): 나가도 팀원이 남아있으면 -> "이어하시겠습니까" 확인 필요
        // - 개인전 다인전(팀 인원 1명씩, 3팀 이상): 나가면 그 팀 전멸 -> (다인전 순위 시스템처럼)
        //   순위만 기록하고 자동으로 계속 진행 (팀전과 달리 물어볼 필요 없음)
        // 셋 다 "팀에 아직 안 나간 사람이 남아있는지"만 확인하면 자동으로 구분됨
        // ===================================================================
        // 나간 사람들을 기록해둠 (같은 사람이 중복으로 나갔다고 처리되는 거 방지)
        private readonly HashSet<PlayerSlot> leftPlayers = new HashSet<PlayerSlot>();
        // 탈주로 완전히 탈락한 팀들 (나간 순서대로 쌓임, finishedRanking과 별개 - 탈락은 낮은 등수라서)
        private readonly List<TeamSlot> eliminatedTeams = new List<TeamSlot>();
        // "팀원이 나가서, 계속할지 확인이 필요한 상태"인지와 그 팀 정보
        private bool awaitingContinueDecision = false;
        private TeamSlot pendingLeaveTeam = TeamSlot.None;
        // UI가 구독: 팀원이 나갔고 "계속하시겠습니까" 확인이 필요할 때 방송 (팝업 띄우는 용도)
        public System.Action<TeamSlot> OnTeamMemberLeftAwaitingDecision;
        // UI(또는 네트워크 이탈 감지 코드)가 "이 플레이어가 나갔다"고 알려줄 때 호출하는 함수
        public void HandlePlayerLeft(PlayerSlot leavingPlayer)
        {
            if (settings == null) return;
            if (leftPlayers.Contains(leavingPlayer)) return; // 이미 처리한 사람이면 무시 (중복 방지)
            leftPlayers.Add(leavingPlayer);
            TeamSlot team = MatchCompositionRule.GetTeamSlot(settings.matchComposition, leavingPlayer);
            if (HasActiveTeammate(team))
            {
                // 아직 이 팀에 안 나간 사람이 남아있음 -> 팀전 상황, "이어할지" 확인 필요
                // 나간 사람만 턴 순서에서 빼둠 (팀원은 그대로 남아서 계속 도는 중)
                turnManager.RemovePlayerFromTurnOrder(leavingPlayer);
                awaitingContinueDecision = true;
                pendingLeaveTeam = team;
                OnTeamMemberLeftAwaitingDecision?.Invoke(team); // UI한테 팝업 띄우라고 알림
            }
            else
            {
                // 이 팀에 남은 사람이 아무도 없음 -> 팀 전멸 (1vs1이거나, 팀 마지막 남은 사람이 나간 경우)
                HandleTeamEliminated(team);
            }
        }
        // 이 팀 소속 중에 "아직 안 나간 사람"이 있는지 확인
        private bool HasActiveTeammate(TeamSlot team)
        {
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                var p = (PlayerSlot)i;
                if (MatchCompositionRule.GetTeamSlot(settings.matchComposition, p) == team &&
                    !leftPlayers.Contains(p))
                    return true;
            }
            return false;
        }
        // UI가 "이어하시겠습니까" 팝업에서 플레이어가 고른 답을 알려줄 때 호출하는 함수
        public void ConfirmContinueAfterLeave(bool continueGame)
        {
            if (!awaitingContinueDecision) return;
            awaitingContinueDecision = false;
            if (!continueGame)
            {
                // 이어 안 하기로 함 -> 이 팀 전체를 탈락 처리 (남은 팀원도 마저 순서에서 제외)
                HandleTeamEliminated(pendingLeaveTeam);
            }
            // continueGame == true면, 나간 사람은 이미 HandlePlayerLeft에서 순서 제외됐으니
            // 추가로 할 일 없이 그냥 게임 계속 진행됨 (남은 팀원끼리 자연스럽게 비율대로 돎)
        }
        // 팀이 완전히 탈락했을 때(전원 나갔거나, "이어하기" 거부) 처리
        private void HandleTeamEliminated(TeamSlot team)
        {
            if (!activeTeams.Contains(team)) return; // 이미 처리된 팀이면 무시
            if (activeTeams.Count <= 2)
            {
                // 남은 팀이 나 말고 하나뿐 -> 그 팀이 자동 승리 (1vs1 즉시패배가 여기 해당)
                TeamSlot winner = TeamSlot.None;
                foreach (var t in activeTeams)
                {
                    if (t != team) winner = t;
                }
                activeTeams.Remove(team);
                if (winner != TeamSlot.None)
                    Declare(winner, GameResultType.Surrender);
            }
            else
            {
                // 다인전에서 이 팀만 탈락, 나머지는 자동으로 계속 진행 (팝업 없이)
                eliminatedTeams.Add(team);
                activeTeams.Remove(team);
                turnManager.RemovePlayersOfTeam(team, settings); // 남은 팀원(있다면)도 마저 순서에서 제거
                Debug.Log(team + " 탈주로 탈락. 남은 " + activeTeams.Count + "팀으로 게임 계속");
            }
        }
        // ===================================================================
        //  Escape 승리조건 2번(제한 시간 안에 더 많은 말 탈출)
        // 1단계: 완주 개수 최다 팀 찾기 (여러 팀 동점 가능)
        // 2단계: 참먹이(골)에 더 가까운 말이 있는 팀 승리
        // 3단계: 그래도 동점이면, 팀 대표가 윷을 던져서 더 높은 결과가 나온 팀 승리 (동점이면 재던지기)
        //        "팀 대표 1명"이 아니라 "팀 소속 전원이 던져서 결과값을
        //        합산"하는 방식으로 변경. 개인전(팀 인원 1명)은 대표=본인이라 결과가 똑같아서 문제없을 듯
        // ===================================================================
        public void HandleTimeLimitReached()
        {
            if (settings == null) return;
            if (settings.gameMode != GameMode.Escape)
            {
                // Classic/KillTheKing은 일단 제한시간 X
                Debug.LogWarning("TestWinConditionManager: " + settings.gameMode + " 제한시간X");
                return;
            }
            Debug.Log("[승리조건2] 제한시간 도달, 동점 판정 시작"); // 확인용 로그
                                                    // 1단계: 완주 개수가 가장 많은 팀들을 다 모음 (동점이면 여러 팀 나올 수 있음)
            int topCount = -1;
            var topTeams = new List<TeamSlot>();
            foreach (var team in activeTeams)
            {
                int count = escapeCountByTeam.TryGetValue(team, out var c) ? c : 0;
                if (count > topCount)
                {
                    topCount = count;
                    topTeams.Clear();
                    topTeams.Add(team);
                }
                else if (count == topCount)
                {
                    topTeams.Add(team);
                }
            }
            if (topTeams.Count == 1)
            {
                Debug.Log("[승리조건2] 1단계에서 승자 확정: " + topTeams[0]); //
                Declare(topTeams[0], GameResultType.TimeOver);
                return;
            }
            // 2단계: 참먹이 거리 비교 
            Debug.Log("[승리조건2] 완주개수 동점(" + topCount + "개), 참먹이 거리 비교 진행"); //
            var distanceTiedTeams = FindClosestTeamsToGoal(topTeams);
            if (distanceTiedTeams.Count == 1)
            {
                Debug.Log("[승리조건2] 2단계에서 승자 확정: " + distanceTiedTeams[0]); //
                Declare(distanceTiedTeams[0], GameResultType.TimeOver);
                return;
            }

            // 3단계: 타이브레이커 던지기 - 자동 계산이 아니라 대상 팀 플레이어들이 실제로
            // [던지기] 버튼을 눌러서 진행함. 여기서는 "누가 던져야 하는지" 세팅만 하고 끝냄.
            // 결과 계산은 아래 RequestTieBreakerThrow()에서, 전원 다 던진 시점에 이루어짐
            StartTieBreakerRound(distanceTiedTeams);
        }

        // ===================================================================
        // 타이브레이커 진행 상태
        // isTieBreakerActive     = 지금 타이브레이커 진행 중인지
        // tieBreakerPendingPlayers = 이번 라운드에서 아직 안 던진 사람들
        // tieBreakerResults      = 이번 라운드에서 이미 던진 사람들의 결과
        // tieBreakerTeams        = 지금 겨루고 있는 팀들 (동점이면 다음 라운드에 이 중 일부로 좁혀짐)
        // ===================================================================
        private bool isTieBreakerActive = false;
        private List<PlayerSlot> tieBreakerPendingPlayers = new List<PlayerSlot>();
        private Dictionary<PlayerSlot, YutResult> tieBreakerResults = new Dictionary<PlayerSlot, YutResult>();
        private List<TeamSlot> tieBreakerTeams = new List<TeamSlot>();

        // UI가 구독: 타이브레이커가 시작될 때(또는 재던지기로 다음 라운드 시작될 때) 방송
        // 매개변수: 지금 겨루는 팀들, 이번 라운드에 던져야 할 사람들
        public System.Action<List<TeamSlot>, List<PlayerSlot>> OnTieBreakerRoundStarted;

        // 동점팀들을 받아서, 그 팀 소속 플레이어 전원을 "던져야 할 사람" 목록으로 세팅하고 대기 상태로 만듦
        private void StartTieBreakerRound(List<TeamSlot> tiedTeams)
        {
            isTieBreakerActive = true;
            tieBreakerTeams = tiedTeams;
            tieBreakerResults.Clear();
            tieBreakerPendingPlayers = new List<PlayerSlot>();
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                var p = (PlayerSlot)i;
                if (tiedTeams.Contains(MatchCompositionRule.GetTeamSlot(settings.matchComposition, p)))
                    tieBreakerPendingPlayers.Add(p);
            }
            Debug.Log("[타이브레이커] 시작됨! 대상 팀: " + string.Join(", ", tiedTeams) +
                " / 던져야 할 사람: " + string.Join(", ", tieBreakerPendingPlayers));
            OnTieBreakerRoundStarted?.Invoke(tiedTeams, new List<PlayerSlot>(tieBreakerPendingPlayers));
        }

        // 테스트용: 인스펙터 우클릭으로, 지금 타이브레이커에서 "아직 안 던진 사람 중 첫 번째"를 대신 던지게 함
        // 우클릭 여러 번 누르면 한 명씩 순서대로 다 던져지고, 전원 다 던지면 자동으로 결과까지 나옴
        [ContextMenu("테스트: 타이브레이커 던지기 (다음 사람)")]
        public void TestRequestTieBreakerThrow()
        {
            if (!isTieBreakerActive || tieBreakerPendingPlayers.Count == 0)
            {
                Debug.LogWarning("지금 타이브레이커 진행 중이 아니거나, 던질 사람이 없음");
                return;
            }
            RequestTieBreakerThrow(tieBreakerPendingPlayers[0]);
        }

        // ===================================================================
        // UI '던지기' 버튼이 타이브레이커 중에 호출하는 함수. 지금 던져야 할 사람 목록에
        // 있는 사람만 던질 수 있음. 전원 다 던지면 자동으로 팀별 합산 -> 승자 판정 (또는 재던지기)까지 진행
        // ===================================================================
        public void RequestTieBreakerThrow(PlayerSlot player)
        {
            if (!isTieBreakerActive)
            {
                Debug.LogWarning("TestWinConditionManager: 타이브레이커 진행 중이 아닌데 던지기 요청이 옴");
                return;
            }
            if (!tieBreakerPendingPlayers.Contains(player))
            {
                Debug.LogWarning("TestWinConditionManager: " + player + "는 지금 타이브레이커에서 던질 차례가 아님");
                return;
            }

            YutResult result = yutRuleManager.ThrowForTieBreaker(); // 낙 없는 전용 확률표 사용
            tieBreakerResults[player] = result;
            tieBreakerPendingPlayers.Remove(player);
            Debug.Log("[타이브레이커] " + player + " 던짐 → " + result +
                " (남은 사람: " + tieBreakerPendingPlayers.Count + "명)");

            if (tieBreakerPendingPlayers.Count > 0) return; // 아직 다른 사람이 안 던졌으면 여기서 대기

            // 전원 다 던졌으니 팀별로 합산해서 비교
            var sumByTeam = new Dictionary<TeamSlot, int>();
            foreach (var team in tieBreakerTeams)
            {
                int sum = 0;
                for (int i = 1; i <= settings.playerCount && i <= 8; i++)
                {
                    var p = (PlayerSlot)i;
                    if (MatchCompositionRule.GetTeamSlot(settings.matchComposition, p) != team) continue;
                    if (tieBreakerResults.TryGetValue(p, out var r))
                        sum += YutResultRule.GetMoveCount(r);
                }
                sumByTeam[team] = sum;
            }
            foreach (var kv in sumByTeam)
                Debug.Log("[타이브레이커] " + kv.Key + " 합산 결과: " + kv.Value);

            int bestSum = sumByTeam.Values.Max();
            var stillTied = tieBreakerTeams.Where(t => sumByTeam[t] == bestSum).ToList();

            if (stillTied.Count == 1)
            {
                isTieBreakerActive = false;
                Debug.Log("[타이브레이커] 종료! 승자: " + stillTied[0]);
                Declare(stillTied[0], GameResultType.TimeOver);
            }
            else
            {
                Debug.Log("[타이브레이커] 또 동점(" + bestSum + ") 발생, 재던지기 라운드 시작");
                StartTieBreakerRound(stillTied); // 그래도 동점이면 그 팀들끼리만 다시 라운드 시작
            }
        }

        // ===================================================================
        // 동점인 팀들 중, "참먹이(골)에 가장 가까운 말"이 있는 팀(들)만 추려냄
        // 각 팀에서 "보드 위에 있는 말들 중 남은 칸수가 가장 작은 말"을 그 팀의 대표 거리로 삼고,
        // 팀들끼리 그 대표 거리를 비교해서 제일 작은(가까운) 팀들만 남김 (동점이면 여러 팀 남을 수 있음)
        // 보드 위에 말이 하나도 없는 팀은 비교 대상에서 제외
        // ===================================================================
        private List<TeamSlot> FindClosestTeamsToGoal(List<TeamSlot> teams)
        {
            var closestDistanceByTeam = new Dictionary<TeamSlot, int>();

            foreach (var team in teams)
            {
                int minDistance = int.MaxValue;
                for (int i = 1; i <= settings.playerCount && i <= 8; i++)
                {
                    var p = (PlayerSlot)i;
                    if (MatchCompositionRule.GetTeamSlot(settings.matchComposition, p) != team) continue;
                    if (!playerManager.TryGetPlayer(i, out var player)) continue;

                    foreach (var piece in player.RuntimeData.Pieces)
                    {
                        // 영서 함수: 보드 위에 있는 말만 성공(true), 나머지(대기중/완주/없음)는 false
                        if (pieceMovementManager.TryGetRemainingStepsToGoal(i, piece.PieceId, out int remainingSteps))
                        {
                            if (remainingSteps < minDistance)
                                minDistance = remainingSteps;
                        }
                    }
                }
                closestDistanceByTeam[team] = minDistance; // 말이 하나도 없으면 int.MaxValue로 남아서 자동으로 불리해짐
            }

            int bestDistance = closestDistanceByTeam.Values.Min();
            return teams.Where(t => closestDistanceByTeam[t] == bestDistance).ToList();
        }
    }
}