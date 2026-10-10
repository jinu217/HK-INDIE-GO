using System;
using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

// Capture callbacks arrive once per real piece. Plan the whole stack on the
// first callback so piece iteration order cannot retire the carrier first.
public sealed class CHAR_003_Status : CharacterStatusBehaviour
{
    private bool hasCapturePlan;
    private int plannedAttackerPlayerId;
    private int plannedAttackerPieceId;
    private BoardTileId plannedTile;
    private int plannedCloneLoss;
    private CharacterCaptureDecision plannedDecision;

    public override bool CanSelectAsActiveCaster(PlayerRuntimeData.PieceRuntimeData piece) =>
        base.CanSelectAsActiveCaster(piece) && CcBoardEffects.CountStackUnits(Owner, piece) < 4;

    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (!TryGetPiece(out var target)) return base.EvaluateIncomingCapture(request);
        if (hasCapturePlan)
        {
            bool matches = plannedAttackerPlayerId == request.AttackerPlayerId &&
                           plannedAttackerPieceId == request.AttackerPieceId &&
                           plannedTile == target.CurrentTileId;
            if (matches) return ExecuteCapturePlan(request, target);
            hasCapturePlan = false;
        }

        if (request.AttackingPieceCount <= 0 || target.State != PieceState.InBoard)
            return base.EvaluateIncomingCapture(request);

        var defenders = new List<PlayerRuntimeData.PieceRuntimeData>();
        int defendingCount = 0;
        bool hasClone = false;
        foreach (var piece in Owner.RuntimeData.Pieces)
        {
            if (piece.State != PieceState.InBoard || piece.CurrentTileId != target.CurrentTileId ||
                (target.IsStacked ? piece.StackGroupId != target.StackGroupId : piece != target))
                continue;
            defenders.Add(piece);
            int clones = piece.Cc.Get(CcDefine.Stack_fake)?.Value ?? 0;
            defendingCount += 1 + clones;
            hasClone |= clones > 0;
        }

        bool limitCapture = defendingCount > request.AttackingPieceCount && IsPassiveReady;
        if (!hasClone && !limitCapture) return base.EvaluateIncomingCapture(request);

        // An incomplete character stack keeps the ordinary CC path.
        var statuses = new List<CHAR_003_Status>();
        foreach (var piece in defenders)
        {
            if (!CharacterSkillRegistry.TryGet(PlayerId, piece.PieceId, out var status) ||
                !(status is CHAR_003_Status gildong))
                return base.EvaluateIncomingCapture(request);
            statuses.Add(gildong);
        }
        if (limitCapture && !TryStartPassiveCooldown() && !hasClone)
            return base.EvaluateIncomingCapture(request);

        int remaining = request.AttackingPieceCount;
        var cloneLosses = new int[defenders.Count];
        var retire = new bool[defenders.Count];
        for (int i = 0; i < defenders.Count && remaining > 0; i++)
        {
            if (IsProtected(defenders[i])) continue;
            int clones = defenders[i].Cc.Get(CcDefine.Stack_fake)?.Value ?? 0;
            cloneLosses[i] = Math.Min(clones, remaining);
            remaining -= cloneLosses[i];
        }
        // Real cargo goes before the stack leader.
        for (int pass = 0; pass < 2 && remaining > 0; pass++)
        {
            for (int i = 0; i < defenders.Count && remaining > 0; i++)
            {
                var piece = defenders[i];
                bool isLeader = !piece.IsStacked || piece.PieceId == piece.StackLeaderPieceId;
                if (isLeader != (pass == 1) || IsProtected(piece)) continue;
                retire[i] = true;
                remaining--;
            }
        }

        for (int i = 0; i < defenders.Count; i++)
        {
            var status = statuses[i];
            status.hasCapturePlan = true;
            status.plannedAttackerPlayerId = request.AttackerPlayerId;
            status.plannedAttackerPieceId = request.AttackerPieceId;
            status.plannedTile = target.CurrentTileId;
            status.plannedCloneLoss = cloneLosses[i];
            status.plannedDecision = retire[i]
                ? CharacterCaptureDecision.LimitRetireToAttackingCount
                : cloneLosses[i] > 0
                    ? CharacterCaptureDecision.ConsumeCloneWithoutBonus
                    : CharacterCaptureDecision.Prevent;
        }
        return ExecuteCapturePlan(request, target);
    }

    private CharacterCaptureDecision ExecuteCapturePlan(
        CharacterCaptureRequest request, PlayerRuntimeData.PieceRuntimeData target)
    {
        hasCapturePlan = false;
        if (IsProtected(target)) return base.EvaluateIncomingCapture(request);
        if (plannedCloneLoss > 0)
            CcEffectService.ConsumeClones(target, plannedCloneLoss);
        return plannedDecision;
    }

    private static bool IsProtected(PlayerRuntimeData.PieceRuntimeData piece) =>
        !CcEffectService.IsTargetable(piece) ||
        piece.Cc.Has(CcDefine.protect) || piece.Cc.Has(CcDefine.Charm);

    protected override CharacterActiveResult ExecuteActive(CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Clone Technique requires a piece on the board.");
        return ApplyActiveEffect()
            ? CharacterActiveResult.Success("A non-scoring clone was stacked on the caster.")
            : CharacterActiveResult.Failure("Clone could not be applied.");
    }

    public override void OnPieceRetired()
    {
        hasCapturePlan = false;
        ResetPassiveCooldown();
    }

    public override void OnAnyPieceMoveCompleted(CharacterMoveRecord record)
    {
        hasCapturePlan = false;
    }
}
