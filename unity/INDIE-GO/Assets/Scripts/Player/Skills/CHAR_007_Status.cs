using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_007_Status : CharacterStatusBehaviour
{
    public override void OnCaptureCompleted(CharacterCaptureRequest request)
    {
        if (!TryStartPassiveCooldown()) return;

        RequestSkillPoint();
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_007_Status)} requested 1 skill point. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Frenzy Charge requires a piece on the board.");

        List<BoardTileId> path = GetPathToStraightEnd(caster);
        if (path.Count == 0)
            return CharacterActiveResult.Failure("No forward straight path is available.");

        CharacterPieceReference? firstEnemy = FindFirstEnemyOnPath(path);
        CharacterBoardUtility.MoveStackAlongPath(
            Owner,
            caster,
            path,
            ignoresInstalledItems: true);

        if (firstEnemy.HasValue &&
            CharacterSkillRegistry.IsTargetable(
                firstEnemy.Value.Player.PlayerId,
                firstEnemy.Value.Piece.PieceId))
            // CC is decremented at the start of its owner's turn. Two stored
            // ticks therefore produce one complete turn in which movement is blocked.
            firstEnemy.Value.Piece.SetCc(CcDefine.Stun, 2);

        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_007_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            "Charged to the end of the straight path and stunned the first enemy ahead.");
    }

    private List<BoardTileId> GetPathToStraightEnd(PlayerRuntimeData.PieceRuntimeData caster)
    {
        var path = new List<BoardTileId>();
        BoardTileId current = caster.CurrentTileId;
        BoardTileId previous = caster.PreviousTileId;

        for (int i = 0; i < 20; i++)
        {
            BoardTileId next = CharacterBoardUtility.GetNextForwardTile(current, previous, i == 0);
            path.Add(next);
            previous = current;
            current = next;

            if (current == BoardTileId.None ||
                current == BoardTileId.Center ||
                current == BoardTileId.Corner01 ||
                current == BoardTileId.Corner02 ||
                current == BoardTileId.Corner03 ||
                current == BoardTileId.Corner04)
                break;
        }

        return path;
    }

    private CharacterPieceReference? FindFirstEnemyOnPath(IReadOnlyList<BoardTileId> path)
    {
        foreach (BoardTileId tile in path)
        {
            foreach (CharacterPieceReference enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
            {
                if (enemy.Piece.CurrentTileId == tile) return enemy;
            }
        }

        return null;
    }
}
