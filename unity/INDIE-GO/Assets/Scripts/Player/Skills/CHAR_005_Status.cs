using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_005_Status : CharacterStatusBehaviour
{
    private static readonly (YutResult, float)[] DefaultTable =
    {
        (YutResult.Do, 10.79f),
        (YutResult.Gae, 33.89f),
        (YutResult.Geol, 35.49f),
        (YutResult.Yut, 13.94f),
        (YutResult.Mo, 2.29f),
        (YutResult.BackDo, 3.59f),
        (YutResult.Nak, 0.01f)
    };

    public override (YutResult, float)[] ModifyYutProbability(
        (YutResult, float)[] currentTable)
    {
        (YutResult, float)[] source = currentTable == null || currentTable.Length == 0
            ? DefaultTable
            : currentTable;
        var result = new List<(YutResult, float)>();
        float redistributedWeight = 0f;

        foreach ((YutResult yutResult, float weight) entry in source)
        {
            if (entry.yutResult == YutResult.BackDo)
                redistributedWeight += entry.weight;
            else
                result.Add(entry);
        }

        AddWeight(result, YutResult.Yut, redistributedWeight * 0.5f);
        AddWeight(result, YutResult.Mo, redistributedWeight * 0.5f);
        return result.ToArray();
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Issen requires a piece on the board.");

        List<BoardTileId> path = CharacterBoardUtility.GetForwardPath(caster, 3);
        foreach (BoardTileId tile in path)
        {
            foreach (CharacterPieceReference enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
            {
                if (enemy.Piece.CurrentTileId == tile &&
                    CharacterSkillRegistry.IsTargetable(enemy.Player.PlayerId, enemy.Piece.PieceId))
                    CharacterBoardUtility.Retire(enemy.Piece, false);
            }
        }

        MoveStackAlongPath(caster, path);
        return CharacterActiveResult.Success(
            "Moved three spaces and retired enemies along the path.",
            suppressExtraThrow: true);
    }

    private void MoveStackAlongPath(
        PlayerRuntimeData.PieceRuntimeData caster,
        IReadOnlyList<BoardTileId> path)
    {
        foreach (BoardTileId tile in path)
        {
            foreach (PlayerRuntimeData.PieceRuntimeData piece in Owner.RuntimeData.Pieces)
            {
                if (piece.PieceId == caster.PieceId ||
                    (caster.IsStacked && piece.StackGroupId == caster.StackGroupId))
                    piece.MoveTo(tile);
            }
        }
    }

    private static void AddWeight(
        List<(YutResult, float)> table,
        YutResult result,
        float additionalWeight)
    {
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i].Item1 != result) continue;
            table[i] = (result, table[i].Item2 + additionalWeight);
            return;
        }

        table.Add((result, additionalWeight));
    }
}
