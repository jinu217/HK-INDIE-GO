using YutArena.InGame;

// 스킬 발동 조건만 담당합니다. 분신 수/표현/업기/소모는 Clone CC가 관리합니다.
public sealed class CHAR_003_Status : CharacterStatusBehaviour
{
    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        var existing = base.EvaluateIncomingCapture(request);
        if (existing != CharacterCaptureDecision.Proceed) return existing;
        if (request.AttackingPieceCount > 0 && TryStartPassiveCooldown())
        {
            ApplyEffect(CcDefine.LimitCapture);
            return base.EvaluateIncomingCapture(request);
        }
        return CharacterCaptureDecision.Proceed;
    }

    protected override CharacterActiveResult ExecuteActive(CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Clone Technique requires a piece on the board.");
        return ApplyEffect(CcDefine.Clone)
            ? CharacterActiveResult.Success("A non-scoring clone was stacked on the caster.")
            : CharacterActiveResult.Failure("Clone could not be applied.");
    }

    public override void OnPieceRetired() { ResetPassiveCooldown(); }
}
