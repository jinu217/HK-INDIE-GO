using YutArena.InGame;

public sealed class CHAR_004_Status : CharacterStatusBehaviour
{
    private bool charmShieldAvailable = true;
    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        var existing = base.EvaluateIncomingCapture(request);
        if (existing != CharacterCaptureDecision.Proceed) return existing;
        if (!charmShieldAvailable || !IsPassiveReady) return existing;
        if (!ApplyPassiveEffect()) return existing;
        TryStartPassiveCooldown();
        charmShieldAvailable = false;
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
        if (!ApplyActiveEffect())
            return CharacterActiveResult.Failure("Stealth effect could not be applied.");
        return CharacterActiveResult.Success($"Stealth applied for {ActiveEffect().ownerTurnTicks} owner-turn ticks.");
    }
}
