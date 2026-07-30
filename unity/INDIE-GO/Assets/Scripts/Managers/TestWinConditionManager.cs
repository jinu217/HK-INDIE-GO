using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers.GameProgress;

namespace YutArena.Managers
{
    // 완주/탈출 여부를 집계해서 승리 조건을 판정. 완주 "판정"(좌표 계산 등) 자체는 보드 쪽이 하고,
    // 여기서는 BoardMoveResult.isFinished 값만 받아서 카운트하고 승패를 확인함
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

        // TurnManager가 보드 쪽 이동 결과를 받을 때마다(=말이 움직일 때마다) 호출해주는 함수.
        // "매 득점 시 반드시 실행", 이동이 일어날 때마다 무조건 호출됨
        public void OnPieceMoveResolved(PlayerSlot player, TeamSlot team, BoardMoveResult result)
        {
            if (!result.isFinished) return; // 완주가 아니면 여기서 할 일 없음

            // 업은 상태로 완주하면 업은 말 + 업힌 말 모두 완주 처리 (기획서 규칙)
            int finishedCount = 1 + result.stackedWithPieceIds.Count;
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
        // 계속 진행할 상황인지"를 판단하는 함수 (다인전 순위 시스템의 핵심)
        // ===================================================================
        private void HandleTeamFinished(TeamSlot team)
        {
            if (!activeTeams.Contains(team)) return; // 이미 처리된 팀이면 무시 (중복 방지)

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
        private void Declare(TeamSlot winningTeam)
        {
            turnManager.NotifyGameEnded();

            // 최종 순위 구성: 먼저 빠진 팀들(finishedRanking) 뒤에 -> 이번 승자 -> 마지막까지 남았던 나머지 팀
            var finalRanking = new List<TeamSlot>(finishedRanking);
            finalRanking.Add(winningTeam);
            foreach (var t in activeTeams)
            {
                if (t != winningTeam)
                    finalRanking.Add(t); // 마지막 대결에서 진 팀 (꼴찌)
            }

            var result = new GameResultData
            {
                resultType = GameResultType.TeamWin,
                winningTeam = winningTeam,
                finalRanking = finalRanking
            };
            gameManager.EndGame(result);
        }

        // 항복 버튼을 눌렀을 때 UI 쪽에서 호출하는 함수
        // TODO: 지금은 1vs1(팀이 2개뿐인 상황) 기준. 다인전 항복 처리는 기획 확정 후 별도 구현 필요
        public void DeclareSurrender(PlayerSlot surrenderingPlayer, TeamSlot winningTeam)
        {
            turnManager.NotifyGameEnded();

            var result = new GameResultData
            {
                resultType = GameResultType.Surrender,
                winningTeam = winningTeam
            };
            gameManager.EndGame(result);
        }
    }
}