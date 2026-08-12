using YutArena.InGame;

public sealed class CHAR_004_Status : CharacterStatusBehaviour
{
    private bool charmShieldAvailable = true;
    private int hiddenOwnerTurns;

    public override bool IsTargetable => hiddenOwnerTurns <= 0;

    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (charmShieldAvailable)
        {
            charmShieldAvailable = false;
            return CharacterCaptureDecision.Prevent;
        }

        return base.EvaluateIncomingCapture(request);
    }

    public override void OnPieceRetired()
    {
        charmShieldAvailable = true;
        hiddenOwnerTurns = 0;
    }

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        if (hiddenOwnerTurns > 0) hiddenOwnerTurns--;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        hiddenOwnerTurns = 3;
        return CharacterActiveResult.Success("The caster cannot be targeted for three owner turns.");
    }
}
