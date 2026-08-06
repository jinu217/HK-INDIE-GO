using System.Collections.Generic;
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
            leftPlayers.Clear();              // [추가] 새 게임 시작이니 나간 사람 기록도 초기화
            eliminatedTeams.Clear();          // [추가]
            awaitingContinueDecision = false; // [추가]
            pendingLeaveTeam = TeamSlot.None; // [추가]

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

            bool won = rule.CheckWin(team, escapeCountByTeam, settings);

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
                finalRanking = finalRanking
            };
            gameManager.EndGame(result);
        }

        /* 항복 버튼을 눌렀을 때 UI 쪽에서 호출하는 함수 (예전 버전, 지금은 안 쓰이지만 남겨둠)
        public void DeclareSurrender(PlayerSlot surrenderingPlayer, TeamSlot winningTeam)
        {
            turnManager.NotifyGameEnded();

            var result = new GameResultData
            {
                resultType = GameResultType.Surrender,
                winningTeam = winningTeam
            };
            gameManager.EndGame(result);
        }*/

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

            // 아직 게임에 남아있는 팀들 중, 완주 개수가 가장 많은 팀을 찾음
            TeamSlot topTeam = TeamSlot.None;
            int topCount = -1;
            bool isTie = false;

            foreach (var team in activeTeams)
            {
                int count = escapeCountByTeam.TryGetValue(team, out var c) ? c : 0;
                if (count > topCount)
                {
                    topCount = count;
                    topTeam = team;
                    isTie = false;
                }
                else if (count == topCount)
                {
                    isTie = true; // 1등이 여러 팀이면 동점
                }
            }

            // 같은 시간내에 여러팀이 동점이면 승리조건을 어떻게 해야될지?
            // 지금은 임시로 "먼저 찾은 팀"이 이기는 걸로 처리해둠 - 규칙 확정되면 교체 필요
            if (isTie)
                Debug.LogWarning("TestWinConditionManager: 제한시간 도달 시 동점 발생 (" + topCount +
                    "개로 동점) - 규칙 미정이라 임시로 " + topTeam + " 를 승자로 처리함");

            if (topTeam != TeamSlot.None)
                Declare(topTeam, GameResultType.TimeOver);
        }
    }
}