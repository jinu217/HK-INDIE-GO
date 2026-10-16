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
        // 팀(TeamSlot)마다 지금까지 얻은 "점수"(int)를 저장하는 표
        // 기획(10/17) 이스케이프 점수제: 점수 = 탈출한 말 개수(업기면 개수만큼) + 보너스 점수
        // 이름은 예전 그대로(escapeCount)지만 내용은 점수. 목표 점수는 settings.targetEscapeCount를 그대로 씀
        private readonly Dictionary<TeamSlot, int> escapeCountByTeam = new Dictionary<TeamSlot, int>();
        // 첫 탈출 보너스를 이미 줬는지 (한 게임에 딱 한 번, 가장 먼저 탈출한 팀에게만)
        private bool firstEscapeBonusGiven;
        // 연속 탈출 보너스용: 마지막으로 득점한 팀과, 그 팀이 다른 팀 득점 없이 연달아 득점한 횟수
        private TeamSlot lastScoringTeam = TeamSlot.None;
        private int scoringStreak;
        // 기획 확인 중: 연속 득점할 때마다 매번 +1인지. false로 바꾸면 처음 연속(2연속)일 때만 +1
        private static readonly bool ConsecutiveBonusEveryTime = true;
        // ===================================================================
        // 다인전 순위 시스템용 (기획서: "최후 2인이 남을 때까지 계속 진행, 등수 표시")
        // activeTeams  = 아직 게임에서 안 빠진 팀들 (목표 달성 안 한 팀들)
        // finishedRanking = 목표를 먼저 채우고 빠진 팀들이 "빠진 순서대로" 쌓이는 리스트 (1등부터)
        // ===================================================================
        private HashSet<TeamSlot> activeTeams = new HashSet<TeamSlot>();
        private List<TeamSlot> finishedRanking = new List<TeamSlot>();
        // 팀별 완주 수가 바뀔 때마다 UI(점수판 등)에 알려주기 위한 이벤트
        public System.Action<TeamSlot, int> OnEscapeCountChanged;

        // UI(점수판)가 "지금 이 팀 몇 개 탈출시켰는지" 직접 물어볼 때 씀.
        // Escape 모드는 완주한 말을 바로 대기 상태로 되돌리기 때문에, 말 상태를 다시 세는 방식으로는
        // 항상 0이 나온다 - 반드시 이 누적값을 읽어야 한다. 개인전은 팀=본인 한 명이라 그대로 개인 점수가 됨.
        public int GetEscapeCount(TeamSlot team)
            => escapeCountByTeam.TryGetValue(team, out var count) ? count : 0;
        // GameManager.StartGame()에서 호출됨. 새 게임 시작하니까 이전 판 점수 기록은 초기화
        public void Initialize(GameStartSettings gameSettings)
        {
            settings = gameSettings;
            escapeCountByTeam.Clear();
            firstEscapeBonusGiven = false;
            lastScoringTeam = TeamSlot.None;
            scoringStreak = 0;
            pendingRankGroups.Clear();
            turnLimitRanking.Clear();
            isTieBreakerActive = false;
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
                var team = MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)i);
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
            int bonus = GetEscapeBonus(team);
            AddEscapeCount(team, finishedCount + bonus); // 탈출한 말 개수 + 보너스 = 이번에 얻은 점수
            Debug.Log("[점수] " + team + " 탈출 " + finishedCount + "개 + 보너스 " + bonus +
                " -> 총 " + GetEscapeCount(team) + "/" + settings.targetEscapeCount + "점"); // 확인용 로그
            CheckWinCondition(team); // 점수 올랐으니 승리 조건 채웠는지 바로 확인
        }
        // ===================================================================
        // 이스케이프 보너스 점수 계산 (클래식 등 다른 모드는 0점)
        // 1. 첫 탈출 보너스: 게임에서 가장 먼저 탈출한 팀 +1 (업어서 2개 첫 탈출이면 2+1=3점)
        // 2. 연속 탈출 보너스: 다른 팀이 득점하기 전에 같은 팀이 또 득점하면 +1
        //    (턴이 달라도 연속으로 침. 업어서 여러 개가 한 번에 탈출한 건 득점 1번으로 셈 = "업기가 아닌 연속")
        //    예: 1P 탈출 -> 1P 탈출(+1) -> 1P 탈출(+1) -> 2P 탈출(연속 끊김) -> 1P 탈출(보너스 없음)
        // ===================================================================
        private int GetEscapeBonus(TeamSlot team)
        {
            if (settings == null || settings.gameMode != GameMode.Escape) return 0;

            int bonus = 0;
            if (!firstEscapeBonusGiven)
            {
                firstEscapeBonusGiven = true;
                bonus += 1;
                Debug.Log("[점수] " + team + " 첫 탈출 보너스 +1");
            }

            scoringStreak = team == lastScoringTeam ? scoringStreak + 1 : 1;
            lastScoringTeam = team;
            if (scoringStreak >= 2 && (ConsecutiveBonusEveryTime || scoringStreak == 2))
            {
                bonus += 1;
                Debug.Log("[점수] " + team + " 연속 탈출 보너스 +1 (" + scoringStreak + "연속)");
            }
            return bonus;
        }
        // 특정 팀의 점수를 amount만큼 늘리고 바뀐 값을 알려주는 함수
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
                ClearBoardPiecesOfTeam(team);
                Debug.Log(team + " 목표 달성! " + finishedRanking.Count + "등으로 기록, 남은 " + activeTeams.Count + "팀으로 게임 계속");
            }
        }
        // ===================================================================
        // 버그 수정: 게임에서 빠진 팀(순위 확정/탈주)의 말이 판 위에 그대로 남아서,
        // 남은 사람들이 그 말을 잡고 보너스 던지기를 받거나 길을 막는 문제가 있었음.
        // 빠지는 순간 그 팀 말을 전부 대기 상태로 되돌려 판에서 내림 (업기/상태이상도 같이 정리됨)
        // 게임이 완전히 끝날 때(Declare)는 결과 화면에서 판이 그대로 보이게 안 치움
        // ===================================================================
        private void ClearBoardPiecesOfTeam(TeamSlot team)
        {
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                if (MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)i) != team) continue;
                if (!playerManager.TryGetPlayer(i, out var player)) continue;

                foreach (var piece in player.RuntimeData.Pieces)
                    piece.Reset();
            }
        }
        // 승리를 확정짓고 TurnManager, GameManager에 알리는 함수 (진짜로 게임이 끝날 때만 호출됨)
        // resultType을 매개변수로 받게 함 (기본값 TeamWin) - 항복/탈주로 끝난 경우 Surrender로 구분하기 위함
        // orderedActiveTeams: 턴 수가 다 돼서 끝날 때, 남은 팀들을 점수/거리/타이브레이커로 줄 세운 순위 (null이면 예전 방식)
        private void Declare(TeamSlot winningTeam, GameResultType resultType = GameResultType.TeamWin,
            List<TeamSlot> orderedActiveTeams = null)
        {
            turnManager.NotifyGameEnded();
            // 최종 순위 구성: 먼저 빠진 팀들(finishedRanking, 완주로 먼저 끝난 순) 뒤에 -> 이번 승자
            // -> 마지막까지 남았던 나머지 팀 -> 맨 끝에 탈주로 탈락한 팀들(나중에 나간 팀일수록 그나마 나은 등수)
            var finalRanking = new List<TeamSlot>(finishedRanking);
            if (orderedActiveTeams != null)
            {
                finalRanking.AddRange(orderedActiveTeams);
            }
            else
            {
                finalRanking.Add(winningTeam);
                foreach (var t in activeTeams)
                {
                    if (t != winningTeam)
                        finalRanking.Add(t); // 마지막 대결에서 진 팀 (꼴찌 후보)
                }
            }
            // eliminatedTeams는 먼저 나간 팀이 리스트 앞쪽에 쌓여있으므로, 뒤집어서 붙이면 "가장 먼저 나간 팀이 제일 마지막(꼴찌)"가 됨
            for (int i = eliminatedTeams.Count - 1; i >= 0; i--)
                finalRanking.Add(eliminatedTeams[i]);

            // 버그 수정: 다인전(3팀 이상)에서 어떤 팀이 먼저 완주해서 finishedRanking에 먼저 들어가 있으면,
            // 그 팀이 finalRanking의 진짜 1등인데 - "승자"로 표시되는 값(winningTeam)에는 그거랑 상관없이
            // 그냥 이 Declare() 호출을 유발한(=마지막까지 남았던 대결의) 팀만 들어가고 있었음.
            // 1:1이나 팀이 2개뿐인 대전은 finishedRanking이 항상 비어있어서 결과가 똑같고(동작 그대로),
            // 3팀 이상 다인전일 때만 "화면에 표시될 승자"가 실제 1등과 맞게 바로잡힘.
            TeamSlot displayedWinner = finalRanking.Count > 0 ? finalRanking[0] : winningTeam;

            var result = new GameResultData
            {
                resultType = resultType,
                winningTeam = displayedWinner,
                winningPlayer = FindSoloWinningPlayer(displayedWinner), // displayedWinner 소속이 1명뿐이면(개인전) 그 사람을, 팀전이면 None을 채움
                finalRanking = finalRanking
            };
            Debug.Log("[승리확정] winningTeam=" + displayedWinner + " / winningPlayer=" + result.winningPlayer +
                " / settings.matchComposition=" + settings.matchComposition + " / settings.playerCount=" + settings.playerCount); // 확인용 로그
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
                if (MatchCompositionRule.GetTeamSlot(settings, p) != winningTeam) continue;
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
            TeamSlot team = MatchCompositionRule.GetTeamSlot(settings, leavingPlayer);
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
                if (MatchCompositionRule.GetTeamSlot(settings, p) == team &&
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
                ClearBoardPiecesOfTeam(team);
                Debug.Log(team + " 탈주로 탈락. 남은 " + activeTeams.Count + "팀으로 게임 계속");
            }
        }
        // ===================================================================
        //  Escape 승리조건 2번 (기획 10/17): 지정 턴 수의 마지막 플레이어 턴이 끝났을 때 순위 결정
        //  (TurnManager가 라운드가 maxTurnCount를 넘는 순간 호출. 함수 이름은 예전 "제한시간" 그대로 둠)
        // 남은 팀들을 1단계: 점수 높은 순 -> 2단계: 참먹이에 가장 가까운 말 기준 가까운 순으로 줄 세우고,
        // 3단계: 점수/거리까지 같은 팀끼리는 [윷 던지기] 타이브레이커로 순위를 가림
        //   - 개인전: 동률인 플레이어만 던짐 / 팀전: 동률 팀 플레이어 전부 던지고 합산
        //   - 동점 묶음이 여러 개면 높은 순위 묶음부터 차례로 진행
        // 먼저 목표 점수를 채워 빠진 팀(finishedRanking)은 이미 순위가 정해져 있어서 그 앞에 그대로 둠
        // ===================================================================
        // 기획 확인 중: 2·3등 동점도 타이브레이커로 가리는지. false로 바꾸면 1등 묶음만 타이브레이커,
        // 나머지 동점은 플레이어 번호 순서대로 둠
        private static readonly bool TieBreakAllRanks = true;

        // 아직 순위가 안 정해진 동점 묶음들 (앞쪽일수록 높은 순위). 맨 앞부터 하나씩 꺼내서 처리함
        private readonly List<List<TeamSlot>> pendingRankGroups = new List<List<TeamSlot>>();
        // 턴 수 도달로 순위가 정해진 남은 팀들 (높은 순위부터 쌓임)
        private readonly List<TeamSlot> turnLimitRanking = new List<TeamSlot>();

        public void HandleTimeLimitReached()
        {
            if (settings == null) return;
            if (settings.gameMode != GameMode.Escape)
            {
                // Classic/KillTheKing은 아직 턴 제한 승부 없음
                Debug.LogWarning("TestWinConditionManager: " + settings.gameMode + " 턴 제한 승부 미구현");
                return;
            }
            Debug.Log("[승리조건2] 턴 수 도달, 순위 판정 시작"); // 확인용 로그

            // 버그 수정: 예전엔 타이브레이커를 시작해도 턴이 멈추지 않아서 바로 다음 턴이 시작됐음.
            // 순위가 다 정해질 때까지 턴 진행을 먼저 멈춤 (타이브레이커 결과가 나올 때까지 이 상태로 대기)
            turnManager.NotifyGameEnded();

            // 1·2단계: 점수 높은 순, 같으면 참먹이 가까운 순
            // (OrderBy는 값이 같으면 원래 순서를 유지 -> 동점일 때 기본 순서는 플레이어 번호 순)
            var teams = GetActiveTeamsInPlayerOrder();
            var distanceByTeam = new Dictionary<TeamSlot, int>();
            foreach (var team in teams)
                distanceByTeam[team] = GetClosestDistanceToGoal(team);
            var sorted = teams
                .OrderByDescending(t => GetEscapeCount(t))
                .ThenBy(t => distanceByTeam[t])
                .ToList();

            foreach (var team in sorted)
                Debug.Log("[승리조건2] " + team + " 점수 " + GetEscapeCount(team) + " / 참먹이 거리 " +
                    (distanceByTeam[team] == int.MaxValue ? "판 위 말 없음" : distanceByTeam[team] + "칸"));

            // 점수와 거리가 둘 다 같은 팀끼리 한 묶음으로 나눔 (혼자면 1팀짜리 묶음)
            pendingRankGroups.Clear();
            turnLimitRanking.Clear();
            foreach (var team in sorted)
            {
                var lastGroup = pendingRankGroups.Count > 0 ? pendingRankGroups[pendingRankGroups.Count - 1] : null;
                if (lastGroup != null &&
                    GetEscapeCount(lastGroup[0]) == GetEscapeCount(team) &&
                    distanceByTeam[lastGroup[0]] == distanceByTeam[team])
                    lastGroup.Add(team);
                else
                    pendingRankGroups.Add(new List<TeamSlot> { team });
            }

            ProcessNextRankGroup();
        }

        // 남은 묶음을 높은 순위부터 하나씩 처리. 혼자인 묶음은 바로 순위 확정,
        // 동점 묶음은 타이브레이커를 시작하고, 결과(RequestTieBreakerThrow)가 나오면 다시 여기로 돌아옴
        private void ProcessNextRankGroup()
        {
            while (pendingRankGroups.Count > 0)
            {
                var group = pendingRankGroups[0];
                bool decidesWinner = finishedRanking.Count == 0 && turnLimitRanking.Count == 0; // 1등 결정 묶음인지
                if (group.Count > 1 && (TieBreakAllRanks || decidesWinner))
                {
                    // 3단계: 타이브레이커 던지기 - 자동 계산이 아니라 대상 플레이어들이 실제로
                    // [던지기] 버튼을 눌러서 진행함. 여기서는 "누가 던져야 하는지" 세팅만 하고 대기
                    StartTieBreakerRound(group);
                    return;
                }
                turnLimitRanking.AddRange(group);
                pendingRankGroups.RemoveAt(0);
            }

            // 모든 순위가 정해짐 -> 게임 종료
            if (turnLimitRanking.Count == 0) return; // 방어 처리 (남은 팀이 없을 수는 없음)
            Debug.Log("[승리조건2] 순위 확정: " + string.Join(" > ", turnLimitRanking));
            Declare(turnLimitRanking[0], GameResultType.TimeOver, new List<TeamSlot>(turnLimitRanking));
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

        // UI가 확인: 지금 타이브레이커 중인지, 이번 라운드에 아직 안 던진 사람이 누구인지
        // (타이브레이커 중엔 던지기 버튼이 TurnManager가 아니라 RequestTieBreakerThrow(플레이어)를 불러야 함)
        public bool IsTieBreakerActive => isTieBreakerActive;
        public IReadOnlyList<PlayerSlot> TieBreakerPendingPlayers => tieBreakerPendingPlayers;

        // UI가 구독: 타이브레이커가 시작될 때(또는 재던지기로 다음 라운드 시작될 때) 방송
        // 매개변수: 지금 겨루는 팀들, 이번 라운드에 던져야 할 사람들
        public System.Action<List<TeamSlot>, List<PlayerSlot>> OnTieBreakerRoundStarted;
        // UI가 구독: 타이브레이커에서 누가 무엇을 던졌는지 (결과 표시용)
        public System.Action<PlayerSlot, YutResult> OnTieBreakerThrown;

        // 동점팀들을 받아서, 그 팀 소속 플레이어 전원을 "던져야 할 사람" 목록으로 세팅하고 대기 상태로 만듦
        private void StartTieBreakerRound(List<TeamSlot> tiedTeams)
        {
            isTieBreakerActive = true;
            tieBreakerTeams = new List<TeamSlot>(tiedTeams);
            tieBreakerResults.Clear();
            tieBreakerPendingPlayers = new List<PlayerSlot>();
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                var p = (PlayerSlot)i;
                if (tiedTeams.Contains(MatchCompositionRule.GetTeamSlot(settings, p)))
                    tieBreakerPendingPlayers.Add(p);
            }
            int rank = finishedRanking.Count + turnLimitRanking.Count + 1; // 이번 묶음이 몇 등 자리를 다투는지
            Debug.Log("[타이브레이커] " + rank + "등 결정전 시작! 대상 팀: " + string.Join(", ", tiedTeams) +
                " / 던져야 할 사람: " + string.Join(", ", tieBreakerPendingPlayers));
            OnTieBreakerRoundStarted?.Invoke(new List<TeamSlot>(tiedTeams), new List<PlayerSlot>(tieBreakerPendingPlayers));
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
        // 있는 사람만 던질 수 있음. 전원 다 던지면 자동으로 팀별 합산 -> 순위 결정 (또는 재던지기)까지 진행
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
            OnTieBreakerThrown?.Invoke(player, result);

            if (tieBreakerPendingPlayers.Count > 0) return; // 아직 다른 사람이 안 던졌으면 여기서 대기

            // 전원 다 던졌으니 팀별로 합산해서 비교
            var sumByTeam = new Dictionary<TeamSlot, int>();
            foreach (var team in tieBreakerTeams)
            {
                int sum = 0;
                for (int i = 1; i <= settings.playerCount && i <= 8; i++)
                {
                    var p = (PlayerSlot)i;
                    if (MatchCompositionRule.GetTeamSlot(settings, p) != team) continue;
                    if (tieBreakerResults.TryGetValue(p, out var r))
                        sum += YutResultRule.GetMoveCount(r);
                }
                sumByTeam[team] = sum;
            }
            foreach (var kv in sumByTeam)
                Debug.Log("[타이브레이커] " + kv.Key + " 합산 결과: " + kv.Value);

            // 합이 큰 순서대로 다시 묶음을 나눠서, 지금 묶음 자리에 끼워 넣음
            // 예: A 5, B 5, C 2 -> [A,B] [C] -> A·B는 다시 타이브레이커, C는 그 다음 순위로 확정
            var splitGroups = tieBreakerTeams
                .GroupBy(t => sumByTeam[t])
                .OrderByDescending(g => g.Key)
                .Select(g => g.ToList())
                .ToList();
            if (splitGroups.Count == 1)
                Debug.Log("[타이브레이커] 또 동점, 재던지기 라운드 시작");

            isTieBreakerActive = false;
            pendingRankGroups.RemoveAt(0);
            pendingRankGroups.InsertRange(0, splitGroups);
            ProcessNextRankGroup(); // 남은 묶음 계속 처리 (동점이면 다시 타이브레이커, 다 정해지면 게임 종료)
        }

        // ===================================================================
        // 이 팀의 "참먹이에 가장 가까운 말"까지 남은 칸수 = 팀의 대표 거리
        // 보드 위에 있는 말들만 보고, 말이 하나도 없으면 int.MaxValue로 남아서 자동으로 가장 불리해짐
        // ===================================================================
        private int GetClosestDistanceToGoal(TeamSlot team)
        {
            int minDistance = int.MaxValue;
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                if (MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)i) != team) continue;
                if (!playerManager.TryGetPlayer(i, out var player)) continue;

                foreach (var piece in player.RuntimeData.Pieces)
                {
                    // 영서 함수: 보드 위에 있는 말만 성공(true), 나머지(대기중/완주/없음)는 false
                    if (pieceMovementManager.TryGetRemainingStepsToGoal(i, piece.PieceId, out int remainingSteps) &&
                        remainingSteps < minDistance)
                        minDistance = remainingSteps;
                }
            }
            return minDistance;
        }

        // 아직 게임에 남은 팀들을 플레이어 번호 순서대로 모음 (동점일 때 기본 순서가 매번 같게)
        private List<TeamSlot> GetActiveTeamsInPlayerOrder()
        {
            var teams = new List<TeamSlot>();
            for (int i = 1; i <= settings.playerCount && i <= 8; i++)
            {
                var team = MatchCompositionRule.GetTeamSlot(settings, (PlayerSlot)i);
                if (activeTeams.Contains(team) && !teams.Contains(team))
                    teams.Add(team);
            }
            return teams;
        }
    }
}
