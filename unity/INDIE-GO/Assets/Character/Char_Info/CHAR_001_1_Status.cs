using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_001_1_Status : CharacterStatusBehaviour
{
    private bool firstMovePassiveAvailable = true;
    private bool firstMovePassivePending;
    // 첫 이동 후 추가 도(1칸) 이동을 바로 실행하지 않고, 플레이어가 화살표를 누를 때까지 대기
    private bool forcedMovePending;

    public bool HasPendingForcedMove => forcedMovePending;

    //수정: 모 아니면 도는 특정 말이 아닌 플레이어의 다음 윷 결과에 적용됩니다.
    public override bool RequiresCasterPieceSelection => false;

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        // 원래 이동량은 바꾸지 않습니다. 첫 이동이 실제로 끝난 뒤 별도의 도(1칸)
        // 이동을 실행해야 원래 착지와 추가 착지에서 각각 잡기를 처리할 수 있습니다.
        if (firstMovePassiveAvailable && request.IsFirstBoardMove && request.MoveCount > 0)
            firstMovePassivePending = true;

        return base.ModifyMoveCount(request);
    }

    public override void OnMoveCompleted(CharacterMoveRecord record)
    {
        if (!firstMovePassivePending || record.PlayerId != PlayerId || record.PieceId != PieceId)
            return;

        firstMovePassivePending = false;
        firstMovePassiveAvailable = false;
        forcedMovePending = true;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_001_1_Status)} queued a Do move " +
            $"after the piece's first move. Player={PlayerId}, Piece={PieceId}",
            this);
    }

    // 턴매니저가 호출: 플레이어가 추가 이동 화살표를 눌렀거나, 누르기 전에 턴이 끝날 때
    public bool ExecutePendingForcedMove()
    {
        if (!forcedMovePending) return false;

        forcedMovePending = false;
        bool moved = ApplyEffect(CcDefine.Move, value: 1);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_001_1_Status)} forced a Do move " +
            $"after the piece's first move. Player={PlayerId}, Piece={PieceId}, Moved={moved}",
            this);
        return moved;
    }

    public override void OnPieceRetired()
    {
        firstMovePassiveAvailable = true;
        firstMovePassivePending = false;
        forcedMovePending = false;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        ApplyEffect(CcDefine.DoOrMo);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_001_1_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The next throw is limited to Do or Mo at 50% each.");
    }

    protected override bool CanUseActiveDuringPhase(TurnPhase phase)
    {
        // 윷/모 또는 잡기로 다시 얻은 던지기 기회에도 사용할 수 있습니다.
        return phase == TurnPhase.WaitThrow;
    }
}
