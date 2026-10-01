using YutArena.InGame;

public sealed class CHAR_004_Status : CharacterStatusBehaviour
{
    private bool charmShieldAvailable = true;
    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        var existing = base.EvaluateIncomingCapture(request);
        if (existing != CharacterCaptureDecision.Proceed) return existing;
        if (!charmShieldAvailable || !TryStartPassiveCooldown()) return existing;
        charmShieldAvailable = false;
        ApplyEffect(CcDefine.Protection);
        return base.EvaluateIncomingCapture(request);
    }

    public override void OnPieceRetired()
    {
        charmShieldAvailable = true;
        ResetPassiveCooldown();
    }

    protected override CharacterActiveResult ExecuteActive(CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Illusion requires a piece on the board.");
        ApplyEffect(CcDefine.Hidden, 3);
        return CharacterActiveResult.Success("The caster cannot be targeted until its third owner turn starts.");
    }
}
