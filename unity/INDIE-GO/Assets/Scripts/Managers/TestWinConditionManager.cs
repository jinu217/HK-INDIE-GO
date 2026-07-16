using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;

namespace YutArena.Managers
{
    public class TestWinConditionManager : MonoBehaviour
    {
        [SerializeField] private TestGameManager gameManager;
        [SerializeField] private TestTurnManager turnManager;

        private GameStartSettings settings;

        private readonly Dictionary<TeamSlot, int> escapeCountByTeam = new Dictionary<TeamSlot, int>();

        public System.Action<TeamSlot, int> OnEscapeCountChanged;

        public void Initialize(GameStartSettings gameSettings)
        {
            settings = gameSettings;
            escapeCountByTeam.Clear();
        }

        public void OnPieceMoveResolved(PlayerSlot player, TeamSlot team, BoardMoveResult result)
        {
            if (!result.isFinished) return;

            int finishedCount = 1 + result.stackedWithPieceIds.Count;
            AddEscapeCount(team, finishedCount);

            CheckWinCondition(team);
        }

        private void AddEscapeCount(TeamSlot team, int amount)
        {
            if (!escapeCountByTeam.ContainsKey(team)) escapeCountByTeam[team] = 0;
            escapeCountByTeam[team] += amount;
            OnEscapeCountChanged?.Invoke(team, escapeCountByTeam[team]);
        }

        private void CheckWinCondition(TeamSlot team)
        {
            if (settings == null) return;

            bool won = false;

            if (settings.gameMode == GameMode.Escape)
            {
                won = escapeCountByTeam.TryGetValue(team, out int count) &&
                      count >= settings.targetEscapeCount;
            }
            else if (settings.gameMode == GameMode.Classic)
            {
                won = escapeCountByTeam.TryGetValue(team, out int count) &&
                      count >= settings.pieceCountPerPlayer;
            }

            if (won)
                Declare(team);
        }

        private void Declare(TeamSlot team)
        {
            turnManager.NotifyGameEnded();

            var result = new GameResultData
            {
                resultType = GameResultType.TeamWin,
                winningTeam = team
            };
            gameManager.EndGame(result);
        }

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