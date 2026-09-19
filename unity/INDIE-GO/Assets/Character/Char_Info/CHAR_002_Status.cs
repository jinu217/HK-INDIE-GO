using YutArena.InGame;

public sealed class CHAR_002_Status : CharacterStatusBehaviour
{
    private bool talismanAvailable = true;

    public override bool CanSelectAsActiveCaster(
        PlayerRuntimeData.PieceRuntimeData piece)
    {
        return CcEffectService.CanUseSkill(piece);
    }

    public override void OnPieceEnteredBoard()
    {
        if (!talismanAvailable ||
            !TryGetPiece(out PlayerRuntimeData.PieceRuntimeData source))
            return;

        CharacterPieceReference? nearest = CharacterBoardUtility.FindNearestAlly(
            Players,
            PlayerId,
            PieceId,
            source.CurrentTileId);
        if (!nearest.HasValue || !TryStartPassiveCooldown())
            return;

        CcEffectService.Apply(nearest.Value.Piece, CcDefine.Protection, 1,
            sourcePlayerId: PlayerId, sourcePieceId: PieceId);
        talismanAvailable = false;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_002_Status)} granted protection to " +
            $"Player={nearest.Value.Player.PlayerId}, Piece={nearest.Value.Piece.PieceId}. Owner={PlayerId}, Piece={PieceId}",
            this);
    }

    public override void OnPieceRetired()
    {
        talismanAvailable = true;
        ResetPassiveCooldown();
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        ApplyEffect(CcDefine.DoubleMove);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_002_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The caster's next move count will be doubled.");
    }

}
