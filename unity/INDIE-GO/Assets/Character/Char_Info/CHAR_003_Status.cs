using System;
using YutArena.Common;
using YutArena.InGame;

// 실제 말은 리더와 함께 생존/퇴장하며, 분신만 별도로 소모합니다.
public sealed class CHAR_003_Status : CharacterStatusBehaviour
{
    public override bool CanSelectAsActiveCaster(PlayerRuntimeData.PieceRuntimeData piece) =>
        base.CanSelectAsActiveCaster(piece) && CcBoardEffects.CountStackUnits(Owner, piece) < 4;

    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (!TryGetPiece(out var target)) return base.EvaluateIncomingCapture(request);
        if (request.AttackingPieceCount <= 0 || target.State != PieceState.InBoard)
            return base.EvaluateIncomingCapture(request);
        if (!CcEffectService.IsTargetable(target) || target.Cc.Has(CcDefine.protect) ||
            target.Cc.Has(CcDefine.Charm))
            return base.EvaluateIncomingCapture(request);

        int defendingCount = CcBoardEffects.CountStackUnits(Owner, target);
        bool limitCapture = defendingCount > request.AttackingPieceCount && IsPassiveReady;
        int clones = target.Cc.Get(CcDefine.Stack_fake)?.Value ?? 0;
        if (limitCapture) TryStartPassiveCooldown();
        if (clones > 0)
        {
            CcEffectService.ConsumeClones(target, Math.Min(clones, request.AttackingPieceCount));
            if (limitCapture || clones >= request.AttackingPieceCount)
                return CharacterCaptureDecision.ConsumeCloneWithoutBonus;
        }
        return limitCapture ? CharacterCaptureDecision.Prevent : CharacterCaptureDecision.Proceed;
    }

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
        ResetPassiveCooldown();
    }
}
