using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_010_Status : CharacterStatusBehaviour
{
    private int pendingExtraSteps;
    private int pendingLastMovingPieceId;
    private BoardTileId pendingStartingTile;

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (request.IsActiveSkillMove)
            return base.ModifyMoveCount(request);

        if (!TryGetPiece(out PlayerRuntimeData.PieceRuntimeData caster) || !caster.IsStacked)
            return base.ModifyMoveCount(request);

        int carriedPieceCount = GetStackPieceCount(caster) - 1;
        if (carriedPieceCount > 0 && TryStartPassiveCooldown())
        {
            pendingExtraSteps = request.MoveCount < 0 ? -carriedPieceCount : carriedPieceCount;
            pendingStartingTile = caster.CurrentTileId;
            pendingLastMovingPieceId = caster.PieceId;
            foreach (var piece in Owner.RuntimeData.Pieces)
                if (piece.State == PieceState.InBoard &&
                    piece.StackGroupId == caster.StackGroupId &&
                    piece.PieceId > pendingLastMovingPieceId)
                    pendingLastMovingPieceId = piece.PieceId;
        }
        // Let the original throw land and capture before the extra movement.
        return base.ModifyMoveCount(request);
    }

    public override void OnAnyPieceMoveCompleted(CharacterMoveRecord record)
    {
        if (record.PlayerId != PlayerId ||
            record.PieceId != pendingLastMovingPieceId ||
            record.From != pendingStartingTile || pendingExtraSteps == 0)
            return;

        int steps = pendingExtraSteps;
        pendingExtraSteps = 0;
        if (TryGetPiece(out var piece) && piece.State == PieceState.InBoard)
            ApplyEffect(CcDefine.Move, value: steps);
    }

    public override void OnPieceRetired()
    {
        pendingExtraSteps = 0;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Tactical Retreat requires a piece on the board.");

        int steps = GetStackPieceCount(caster);
        var path = new List<BoardTileId>();
        BoardTileId current = caster.CurrentTileId;
        BoardTileId previous = caster.PreviousTileId;
        for (int i = 0; i < steps; i++)
        {
            // A single step retraces the incoming branch; multi-step retreat
            // takes the outer route at the two shortcut exits.
            BoardTileId next = steps > 1 && current == BoardTileId.Corner03
                ? BoardTileId.Outer12
                : steps > 1 && current == BoardTileId.None
                    ? BoardTileId.Outer16
                    : CharacterBoardUtility.GetNextBackwardTile(current, previous);
            if (next == current) break;
            path.Add(next);
            previous = current;
            current = next;
        }
        if (path.Count == 0 || !CcEffectService.Apply(caster, CcDefine.MovePath,
                sourcePlayerId: PlayerId, sourcePieceId: PieceId, path: path))
            return CharacterActiveResult.Failure("The one-tile retreat could not be resolved.");

        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_010_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            $"Retreated {path.Count} tile(s).",
            suppressExtraThrow: true,
            endTurnAfterResolution: true);
    }
}
