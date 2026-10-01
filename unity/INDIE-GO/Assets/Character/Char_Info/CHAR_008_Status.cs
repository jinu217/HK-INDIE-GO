using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_008_Status : CharacterStatusBehaviour
{
    public override bool RequiresTargetPieceSelection => true;

    private static readonly Dictionary<int, IReadOnlyList<BoardTileId>> PendingLastPaths =
        new Dictionary<int, IReadOnlyList<BoardTileId>>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        PendingLastPaths.Clear();
    }

    public override void OnMoveCompleted(CharacterMoveRecord record)
    {
        PendingLastPaths[PlayerId] = record.Path;
    }

    public override void OnOwnerTurnEnded()
    {
        if (!PendingLastPaths.TryGetValue(PlayerId, out IReadOnlyList<BoardTileId> path))
            return;

        if (TryGetPiece(out var piece))
            CcEffectService.Apply(piece, CcDefine.WindPath, sourcePlayerId: PlayerId,
                sourcePieceId: PieceId, path: path);
        PendingLastPaths.Remove(PlayerId);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (!request.HasTarget)
        {
            if (!TryFindAutomaticTarget(caster, out CharacterPieceReference automaticTarget))
                return CharacterActiveResult.Failure("There is no target within five tiles.");

            return BindTarget(caster, automaticTarget);
        }

        if (!TryGetPiece(request.TargetPlayerId, request.TargetPieceId, out CharacterPieceReference target))
            return CharacterActiveResult.Failure("The selected target does not exist.");

        return BindTarget(caster, target);
    }

    private CharacterActiveResult BindTarget(
        PlayerRuntimeData.PieceRuntimeData caster,
        CharacterPieceReference target)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Spirit Arrow requires a piece on the board.");
        if (target.Player.PlayerId == PlayerId || target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Spirit Arrow can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");
        if (!CharacterBoardUtility.IsWithinDistance(
                caster.CurrentTileId,
                target.Piece.CurrentTileId,
                5))
            return CharacterActiveResult.Failure("The selected enemy is farther than five tiles.");

        // CC is decremented at the start of its owner's turn. Two stored
        // ticks therefore produce one complete turn in which movement is blocked.
        CcEffectService.Apply(target.Piece, CcDefine.Stun, 2,
            sourcePlayerId: PlayerId, sourcePieceId: PieceId);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_008_Status)} activated against " +
            $"Player={target.Player.PlayerId}, Piece={target.Piece.PieceId}. " +
            $"Owner={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The selected enemy was bound for one turn.");
    }

    private bool TryFindAutomaticTarget(
        PlayerRuntimeData.PieceRuntimeData caster,
        out CharacterPieceReference target)
    {
        target = default;
        if (caster.State != PieceState.InBoard) return false;

        int nearestDistance = int.MaxValue;
        foreach (CharacterPieceReference enemy in
                 CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
        {
            if (!CharacterSkillRegistry.IsTargetable(
                    enemy.Player.PlayerId,
                    enemy.Piece.PieceId))
                continue;

            int distance = CharacterBoardUtility.GetDistance(
                caster.CurrentTileId,
                enemy.Piece.CurrentTileId);
            if (distance > 5 || distance >= nearestDistance) continue;

            nearestDistance = distance;
            target = enemy;
        }

        return nearestDistance != int.MaxValue;
    }

    public override bool CanSelectActiveTarget(int targetPlayerId, int targetPieceId)
    {
        return base.CanSelectActiveTarget(targetPlayerId, targetPieceId) &&
            TryGetPiece(out var caster) && TryGetPiece(targetPlayerId, targetPieceId, out var target) &&
            CharacterBoardUtility.IsWithinDistance(caster.CurrentTileId, target.Piece.CurrentTileId, 5);
    }
}
