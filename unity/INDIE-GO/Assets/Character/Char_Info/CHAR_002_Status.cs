using YutArena.InGame;

public sealed class CHAR_002_Status : CharacterStatusBehaviour
{
    private bool talismanAvailable = true;
    private bool doubleNextMove;

    public override bool CanSelectAsActiveCaster(
        PlayerRuntimeData.PieceRuntimeData piece)
    {
        return piece != null && piece.State != PieceState.Goal;
    }

    public override void OnPieceEnteredBoard()
    {
        if (!talismanAvailable || !TryStartPassiveCooldown() ||
            !TryGetPiece(out PlayerRuntimeData.PieceRuntimeData source))
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
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_002_Status)} granted protection to " +
            $"Player={ally.PlayerId}, Piece={ally.PieceId}. Owner={PlayerId}, Piece={PieceId}",
            this);
    }

    public override void OnPieceRetired()
    {
        talismanAvailable = true;
        doubleNextMove = false;
        ResetPassiveCooldown();
    }

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (!doubleNextMove || request.IsActiveSkillMove) return request.MoveCount;
        doubleNextMove = false;
        int modifiedMoveCount = request.MoveCount * 2;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][ActiveEffect] {nameof(CHAR_002_Status)} doubled movement: " +
            $"{request.MoveCount} -> {modifiedMoveCount}. Player={PlayerId}, Piece={PieceId}",
            this);
        return modifiedMoveCount;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        doubleNextMove = true;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_002_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The caster's next move count will be doubled.");
    }

    public override void OnOwnerTurnEnded()
    {
        doubleNextMove = false;
    }
}
