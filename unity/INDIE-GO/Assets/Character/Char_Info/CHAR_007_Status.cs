using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_007_Status : CharacterStatusBehaviour
{
    // 전투광은 기본 턴 SP 대신 적을 잡았을 때만 SP를 얻습니다.
    public override bool GainsBaseTurnSkillPoint => false;

    public override void OnCaptureCompleted(CharacterCaptureRequest request)
    {
        if (!IsPassiveReady || !ApplyPassiveEffect()) return;
        TryStartPassiveCooldown();
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_007_Status)} requested {PassiveEffect().amount} skill point(s). " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard || !CcEffectService.CanMove(caster))
            return CharacterActiveResult.Failure("Frenzy Charge requires a piece on the board.");

        List<BoardTileId> path = GetPathToStraightEnd(caster);
        if (path.Count == 0)
            return CharacterActiveResult.Failure("No forward straight path is available.");

        var enemiesOnPath = FindEnemiesOnPath(path);
        if (ActiveEffect(1) == null || !ApplyActiveEffect(path: path))
            return CharacterActiveResult.Failure("Charge effect settings/movement could not be resolved.");

        foreach (CharacterPieceReference enemy in enemiesOnPath)
            ApplyActiveEffect(1, enemy.Piece);

        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_007_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            $"Charged to the end of the straight path and stunned {enemiesOnPath.Count} enemy piece(s).");
    }

    private List<BoardTileId> GetPathToStraightEnd(PlayerRuntimeData.PieceRuntimeData caster)
    {
        var path = new List<BoardTileId>();
        BoardTileId current = caster.CurrentTileId;
        BoardTileId previous = caster.PreviousTileId;

        if (current == BoardTileId.None && caster.State == PieceState.InBoard)
        {
            path.Add(BoardTileId.None);
            return path;
        }

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

    private List<CharacterPieceReference> FindEnemiesOnPath(IReadOnlyList<BoardTileId> path)
    {
        var result = new List<CharacterPieceReference>();
        foreach (BoardTileId tile in path)
        {
            foreach (CharacterPieceReference enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
            {
                if (enemy.Piece.CurrentTileId == tile && !result.Contains(enemy))
                    result.Add(enemy);
            }
        }
        return result;
    }
}
