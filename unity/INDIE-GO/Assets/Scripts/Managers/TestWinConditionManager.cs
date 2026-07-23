using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    // 완주/탈출 여부를 집계해서 승리 조건을 판정. 완주 "판정"(좌표 계산 등) 자체는 보드 쪽이 하고,
    // 여기서는 BoardMoveResult.isFinished 값만 받아서 카운트하고 승패를 확인함
    public class TestWinConditionManager : MonoBehaviour
    {
        [SerializeField] private TestGameManager gameManager;
        [SerializeField] private TestTurnManager turnManager;

        private GameStartSettings settings; // 대기실에서 정한 모드/목표탈출수 등을 기억해둠

        // 팀(TeamSlot)마다 지금까지 완주한 말 개수(int)를 저장하는 표
        // 예: { TeamA: 2, TeamB: 1 } -> A팀은 2마리, B팀은 1마리 완주했다는 뜻
        private readonly Dictionary<TeamSlot, int> escapeCountByTeam = new Dictionary<TeamSlot, int>();

        // 팀별 완주 수가 바뀔 때마다 UI(점수판 등)에 알려주기 위한 이벤트
        public System.Action<TeamSlot, int> OnEscapeCountChanged;

        // GameManager.StartGame()에서 호출됨. 새 게임 시작하니까 이전 판 점수 기록은 초기화
        public void Initialize(GameStartSettings gameSettings)
        {
            settings = gameSettings;
            escapeCountByTeam.Clear();
        }

        // TurnManager가 보드 쪽 이동 결과를 받을 때마다(=말이 움직일 때마다) 호출해주는 함수.
        // "매 득점 시 반드시 실행", 이동이 일어날 때마다 무조건 호출됨
        public void OnPieceMoveResolved(PlayerSlot player, TeamSlot team, BoardMoveResult result)
        {
            if (!result.isFinished) return; // 완주가 아니면 여기서 할 일 없음

            // 업은 상태로 완주하면 업은 말 + 업힌 말 모두 완주 처리 (기획서 규칙)
            // stackedWithPieceIds.Count = 같이 업혀있던 말이 몇 마리인지, 거기에 자기 자신(1)을 더함
            int finishedCount = 1 + result.stackedWithPieceIds.Count;
            AddEscapeCount(team, finishedCount);

            CheckWinCondition(team); // 점수 올랐으니 승리 조건 채웠는지 바로 확인
        }

        // 특정 팀의 완주 카운트를 amount만큼 늘리고 바뀐 값을 알려주는 함수
        private void AddEscapeCount(TeamSlot team, int amount)
        {
            // 표에 아직 그 팀 키가 없으면 0으로 먼저 만들어줌 (없는 키에 그냥 += 하면 에러남)
            if (!escapeCountByTeam.ContainsKey(team)) escapeCountByTeam[team] = 0;
            escapeCountByTeam[team] += amount;
            OnEscapeCountChanged?.Invoke(team, escapeCountByTeam[team]);
        }

        // 지금 카운트를 기준으로 이 팀이 승리 조건을 만족했는지 확인
        private void CheckWinCondition(TeamSlot team)
        {
            if (settings == null) return;

            bool won = false;

            if (settings.gameMode == GameMode.Escape)
            {
                // TryGetValue = 그 키가 있으면 값을 꺼내서 count에 담고 true, 없으면 false
                won = escapeCountByTeam.TryGetValue(team, out int count) &&
                      count >= settings.targetEscapeCount; // 목표 탈출 수(기본 4) 이상이면 승리
                // TODO: 승리 조건 2(제한시간 안에 더 많은 말 탈출)는 타이머 완성되면 별도로 처리 필요
            }
            else if (settings.gameMode == GameMode.Classic)
            {
                // Classic은 그 팀이 가진 말 전체(pieceCountPerPlayer)가 다 완주해야 승리
                won = escapeCountByTeam.TryGetValue(team, out int count) &&
                      count >= settings.pieceCountPerPlayer;
            }
            // TODO: KillTheKing 모드 승리 조건은 추후 구현

            if (won)
                Declare(team);
        }

        // 승리를 확정짓고 TurnManager, GameManager에 알리는 함수
        private void Declare(TeamSlot team)
        {
            turnManager.NotifyGameEnded(); // "게임 끝났다" 표시만 해둠 지금 턴이 끝날 때 실제로 멈춤

            var result = new GameResultData
            {
                resultType = GameResultType.TeamWin,
                winningTeam = team
            };
            gameManager.EndGame(result); // GameManager가 Result 화면으로 전환하고 이벤트를 알려줌
        }

        // 항복 버튼을 눌렀을 때 UI 쪽에서 호출하는 함수
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