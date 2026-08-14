using YutArena.InGame;

public sealed class CHAR_004_Status : CharacterStatusBehaviour
{
    private bool charmShieldAvailable = true;
    private int hiddenOwnerTurns;

    public override bool IsTargetable => hiddenOwnerTurns <= 0;

    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (charmShieldAvailable && TryStartPassiveCooldown())
        {
            charmShieldAvailable = false;
            UnityEngine.Debug.Log(
                $"[CharacterSkill][Passive] {nameof(CHAR_004_Status)} prevented capture. " +
                $"Player={PlayerId}, Piece={PieceId}",
                this);
            return CharacterCaptureDecision.Prevent;
        }

        return base.EvaluateIncomingCapture(request);
    }

    public override void OnPieceRetired()
    {
        charmShieldAvailable = true;
        hiddenOwnerTurns = 0;
        ResetPassiveCooldown();
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
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Illusion requires a piece on the board.");

        hiddenOwnerTurns = 3;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_004_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The caster cannot be targeted for three owner turns.");
    }
}
