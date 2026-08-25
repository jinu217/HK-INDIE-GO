using System.Collections.Generic;
using YutArena.InGame;
using YutArena.Common;

public sealed class CHAR_001_2_Status : CharacterStatusBehaviour
{
    private static readonly Dictionary<(int playerId, int stackGroupId), int>
        ObservedStackSizes = new Dictionary<(int, int), int>();

    //수정: 한번 더는 특정 말이 아닌 현재 플레이어의 턴에 추가 던지기를 예약합니다.
    public override bool RequiresCasterPieceSelection => false;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ObservedStackSizes.Clear();
    }

    public override void OnCaptureCompleted(CharacterCaptureRequest request)
    {
        if (request.AttackerPlayerId != PlayerId || request.AttackerPieceId != PieceId)
            return;

        GrantPassiveSkillPoint("capture");
    }

    public override void OnMoveCompleted(CharacterMoveRecord record)
    {
        if (record.PlayerId != PlayerId ||
            record.PieceId != PieceId ||
            !TryGetPiece(out PlayerRuntimeData.PieceRuntimeData movedPiece) ||
            movedPiece.State != PieceState.InBoard ||
            !movedPiece.IsStacked)
        {
            return;
        }

        int stackSize = GetStackPieceCount(movedPiece);
        if (stackSize < 2)
            return;

        var key = (PlayerId, movedPiece.StackGroupId);
        if (ObservedStackSizes.TryGetValue(key, out int observedSize) &&
            stackSize <= observedSize)
        {
            return;
        }

        ObservedStackSizes[key] = stackSize;
        GrantPassiveSkillPoint("friendly stack");
    }

    private void GrantPassiveSkillPoint(string trigger)
    {
        RequestSkillPoint();
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_001_2_Status)} gained 1 SP " +
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

        Turns.GrantSkillExtraThrow();
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_001_2_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("One skill extra throw was granted.");
    }
}
