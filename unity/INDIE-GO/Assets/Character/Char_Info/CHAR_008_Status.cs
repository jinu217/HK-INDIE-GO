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
        // An earlier stack member can already have moved the whole stack via Wind.
        // Its remaining original notifications must not replace the final path.
        var moved = CcEffectService.GetPiece(record.PlayerId, record.PieceId);
        if (moved != null && moved.CurrentTileId != record.To) return;
        if (CcEffectService.IsResolvingWindMove &&
            PendingLastPaths.TryGetValue(PlayerId, out IReadOnlyList<BoardTileId> previous))
        {
            var combined = new List<BoardTileId>(previous);
            foreach (BoardTileId tile in record.Path)
                if (combined.Count == 0 || combined[combined.Count - 1] != tile)
                    combined.Add(tile);
            PendingLastPaths[PlayerId] = combined;
        }
        else PendingLastPaths[PlayerId] = new List<BoardTileId>(record.Path);
    }

    public override void OnOwnerTurnEnded()
    {
        if (TryGetPiece(out var currentPiece))
            CcEffectService.Remove(currentPiece, CcDefine.WindPath);
        if (!PendingLastPaths.TryGetValue(PlayerId, out IReadOnlyList<BoardTileId> path))
            return;

        if (TryGetPiece(out var piece))
            ApplyPassiveEffect(target: piece, path: path);
        PendingLastPaths.Remove(PlayerId);
    }

    internal override bool TryApplyWindMove(PlayerRuntimeData.PieceRuntimeData piece)
    {
        var effect = PassiveEffect(1);
        if (!IsPassiveReady || effect == null || effect.amount <= 0) return false;
        var path = CharacterBoardUtility.GetForwardPath(piece, effect.amount);
        if (!ApplyPassiveEffect(1, piece, path)) return false;
        return TryStartPassiveCooldown();
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (!request.HasTarget)
        {
            if (!TryFindAutomaticTarget(caster, out CharacterPieceReference automaticTarget))
                return CharacterActiveResult.Failure($"There is no target within {ActiveRange} tiles.");

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
        if (CharacterBoardUtility.AreAllies(PlayerId, target.Player.PlayerId) ||
            target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Spirit Arrow can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");
        if (!CharacterBoardUtility.IsWithinDistance(
                caster.CurrentTileId,
                target.Piece.CurrentTileId,
                ActiveRange))
            return CharacterActiveResult.Failure($"The selected enemy is farther than {ActiveRange} tiles.");

        if (!ApplyActiveEffect(target: CcEffectService.StackLeader(target.Piece)))
            return CharacterActiveResult.Failure("The selected enemy resisted Binding.");
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_008_Status)} activated against " +
            $"Player={target.Player.PlayerId}, Piece={target.Piece.PieceId}. " +
            $"Owner={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success($"Binding applied for {ActiveEffect().ownerTurnTicks} owner-turn ticks.");
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
            if (distance > ActiveRange || distance >= nearestDistance) continue;

            nearestDistance = distance;
            target = enemy;
        }

        return nearestDistance != int.MaxValue;
    }

    public override bool CanSelectActiveTarget(int targetPlayerId, int targetPieceId)
    {
        return base.CanSelectActiveTarget(targetPlayerId, targetPieceId) &&
            TryGetPiece(out var caster) && TryGetPiece(targetPlayerId, targetPieceId, out var target) &&
            CharacterBoardUtility.IsWithinDistance(caster.CurrentTileId, target.Piece.CurrentTileId, ActiveRange);
    }
}
