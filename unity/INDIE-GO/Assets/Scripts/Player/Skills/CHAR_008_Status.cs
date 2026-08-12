using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_008_Status : CharacterStatusBehaviour
{
    private readonly HashSet<int> windTriggeredPieces = new HashSet<int>();
    private IReadOnlyList<BoardTileId> pendingLastPath;
    private IReadOnlyList<BoardTileId> activeWindPath;

    public override void OnMoveCompleted(CharacterMoveRecord record)
    {
        pendingLastPath = record.Path;
    }

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        windTriggeredPieces.Clear();
    }

    public override void OnOwnerTurnEnded()
    {
        activeWindPath = pendingLastPath;
        pendingLastPath = null;
    }

    public override void OnAnyPieceMoveCompleted(CharacterMoveRecord record)
    {
        if (activeWindPath == null || record.PlayerId != PlayerId ||
            windTriggeredPieces.Contains(record.PieceId) ||
            !Contains(activeWindPath, record.To))
            return;

        if (Movement == null) return;
        windTriggeredPieces.Add(record.PieceId);
        Movement.TryMovePiece(record.PlayerId, record.PieceId, 1, out _, true);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (!request.HasTarget)
            return CharacterActiveResult.Failure("Spirit Arrow requires an enemy target.");
        if (!TryGetPiece(request.TargetPlayerId, request.TargetPieceId, out CharacterPieceReference target))
            return CharacterActiveResult.Failure("The selected target does not exist.");
        if (Players.AreAllies(target.Player.PlayerId, PlayerId) ||
            target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Spirit Arrow can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");
        if (!CharacterBoardUtility.IsWithinDistance(
                caster.CurrentTileId,
                target.Piece.CurrentTileId,
                5))
            return CharacterActiveResult.Failure("The selected enemy is farther than five tiles.");

        target.Piece.SetCc(CcDefine.Stun, 1);
        return CharacterActiveResult.Success("The selected enemy was bound for one turn.");
    }

    private static bool Contains(IReadOnlyList<BoardTileId> path, BoardTileId tile)
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == tile) return true;
        }

        return false;
    }
}
