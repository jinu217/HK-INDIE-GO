using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_006_Status : CharacterStatusBehaviour
{
    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (IsPassiveReady && Random.value < 0.25f && TryStartPassiveCooldown())
        {
            Debug.Log(
                $"[CharacterSkill][Passive] {nameof(CHAR_006_Status)} prevented capture. " +
                $"Player={PlayerId}, Piece={PieceId}",
                this);
            return CharacterCaptureDecision.Prevent;
        }

        return base.EvaluateIncomingCapture(request);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Sword Aura requires a piece on the board.");
        CharacterPieceReference target;
        if (request.HasTarget)
        {
            if (!TryGetPiece(request.TargetPlayerId, request.TargetPieceId, out target))
                return CharacterActiveResult.Failure("The selected target does not exist.");
        }
        else if (!TryFindAutomaticTarget(caster, out target))
        {
            return CharacterActiveResult.Failure(
                "There is no target on the caster's tile or the next tile.");
        }

        if (target.Player.PlayerId == PlayerId || target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Sword Aura can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");

        BoardTileId forward = CharacterBoardUtility.GetNextForwardTile(
            caster.CurrentTileId,
            caster.PreviousTileId,
            true);
        if (target.Piece.CurrentTileId != caster.CurrentTileId &&
            target.Piece.CurrentTileId != forward)
            return CharacterActiveResult.Failure("The enemy is not on the caster's tile or the next tile.");

        bool captured = CharacterBoardUtility.TryCapture(
            PlayerId,
            PieceId,
            target,
            GetStackPieceCount(caster),
            true,
            out CharacterCaptureDecision decision);
        Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_006_Status)} activated against " +
            $"Player={target.Player.PlayerId}, Piece={target.Piece.PieceId}. " +
            $"Owner={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            captured
                ? "The selected enemy was captured by Sword Aura."
                : $"Sword Aura was resolved with {decision}.");
    }

    private bool TryFindAutomaticTarget(
        PlayerRuntimeData.PieceRuntimeData caster,
        out CharacterPieceReference target)
    {
        BoardTileId forward = CharacterBoardUtility.GetNextForwardTile(
            caster.CurrentTileId,
            caster.PreviousTileId,
            true);
        foreach (CharacterPieceReference enemy in
                 CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
        {
            if ((enemy.Piece.CurrentTileId == caster.CurrentTileId ||
                 enemy.Piece.CurrentTileId == forward) &&
                CharacterSkillRegistry.IsTargetable(
                    enemy.Player.PlayerId,
                    enemy.Piece.PieceId))
            {
                target = enemy;
                return true;
            }
        }

        target = default;
        return false;
    }
}
