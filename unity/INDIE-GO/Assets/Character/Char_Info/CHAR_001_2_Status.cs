using YutArena.InGame;
using YutArena.Common;

public sealed class CHAR_001_2_Status : CharacterStatusBehaviour
{
    private int stackSizeBeforeMove;
    private bool observingOwnMove;

    //수정: 한번 더는 특정 말이 아닌 현재 플레이어의 턴에 추가 던지기를 예약합니다.
    public override bool RequiresCasterPieceSelection => false;

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        // 이동 전에 크기를 기록해야 기존 스택의 단순 이동과 새 업기를 구별할 수 있습니다.
        observingOwnMove = TryGetPiece(out PlayerRuntimeData.PieceRuntimeData piece);
        stackSizeBeforeMove = observingOwnMove ? GetStackPieceCount(piece) : 0;
        return base.ModifyMoveCount(request);
    }

    public override void OnCaptureCompleted(CharacterCaptureRequest request)
    {
        if (request.AttackerPlayerId != PlayerId || request.AttackerPieceId != PieceId)
            return;

        GrantPassiveSkillPoint("capture");
    }

    public override void OnMoveCompleted(CharacterMoveRecord record)
    {
        if (record.PlayerId != PlayerId || record.PieceId != PieceId || !observingOwnMove)
            return;

        observingOwnMove = false;
        if (!TryGetPiece(out PlayerRuntimeData.PieceRuntimeData movedPiece) ||
            movedPiece.State != PieceState.InBoard || !movedPiece.IsStacked)
            return;

        if (GetStackPieceCount(movedPiece) > stackSizeBeforeMove)
            GrantPassiveSkillPoint("friendly stack");
    }

    public override void OnPieceRetired()
    {
        observingOwnMove = false;
    }

    private void GrantPassiveSkillPoint(string trigger)
    {
        if (!ApplyPassiveEffect()) return;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_001_2_Status)} requested {PassiveEffect().amount} SP " +
            $"from {trigger}. Player={PlayerId}, Piece={PieceId}",
            this);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (Turns == null)
            return CharacterActiveResult.Failure("TestTurnManager is not available.");
        if ((int)Turns.CurrentTurn.currentPlayer != PlayerId)
            return CharacterActiveResult.Failure("The active skill can be used only during its owner's turn.");
        if (Turns.CurrentTurn.currentPhase != TurnPhase.WaitAction)
            return CharacterActiveResult.Failure("Use Once More after throwing yut and before moving a piece.");

        if (!ApplyActiveEffect())
            return CharacterActiveResult.Failure("The extra throw effect could not be applied.");
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_001_2_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("One skill extra throw was granted.");
    }
}
