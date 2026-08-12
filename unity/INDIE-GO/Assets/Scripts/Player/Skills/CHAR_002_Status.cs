using YutArena.InGame;

public sealed class CHAR_002_Status : CharacterStatusBehaviour
{
    private bool talismanAvailable = true;
    private bool doubleNextMove;

    public override void OnPieceEnteredBoard()
    {
        if (!talismanAvailable || !TryGetPiece(out PlayerRuntimeData.PieceRuntimeData source))
            return;

        CharacterPieceReference? nearest = CharacterBoardUtility.FindNearestAlly(
            Players,
            PlayerId,
            PieceId,
            source.CurrentTileId);
        if (!nearest.HasValue ||
            !CharacterSkillRegistry.TryGet(
                nearest.Value.Player.PlayerId,
                nearest.Value.Piece.PieceId,
                out CharacterStatusBehaviour ally))
            return;

        ally.GrantProtection(1, 1);
        talismanAvailable = false;
    }

    public override void OnPieceRetired()
    {
        talismanAvailable = true;
    }

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (!doubleNextMove) return request.MoveCount;
        doubleNextMove = false;
        return request.MoveCount * 2;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        doubleNextMove = true;
        return CharacterActiveResult.Success("The caster's next move count will be doubled.");
    }
}
