using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_001_1_Status : CharacterStatusBehaviour
{
    //수정: 첫 던지기 이후의 윷/모 및 잡기 재던지기 단계에서는 액티브를 막습니다.
    private static readonly HashSet<int> PlayersWhoHaveThrown = new HashSet<int>();
    private bool firstMovePassiveAvailable = true;
    private bool firstMovePassivePending;

    //수정: 모 아니면 도는 특정 말이 아닌 플레이어의 다음 윷 결과에 적용됩니다.
    public override bool RequiresCasterPieceSelection => false;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        PlayersWhoHaveThrown.Clear();
    }

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        PlayersWhoHaveThrown.Remove(PlayerId);
    }

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
        bool moved = ApplyEffect(CcDefine.Move, value: 1);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_001_1_Status)} forced a Do move " +
            $"after the piece's first move. Player={PlayerId}, Piece={PieceId}, Moved={moved}",
            this);
    }

    public override void OnPieceRetired()
    {
        firstMovePassiveAvailable = true;
        firstMovePassivePending = false;
    }

    //수정: 첫 번째 윷 결과가 확정되는 시점부터 이번 턴에는 액티브를 사용할 수 없습니다.
    public override bool ShouldGrantExtraThrow(YutResult result, bool defaultValue)
    {
        PlayersWhoHaveThrown.Add(PlayerId);
        return base.ShouldGrantExtraThrow(result, defaultValue);
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

    public override void OnOwnerTurnEnded()
    {
        PlayersWhoHaveThrown.Remove(PlayerId);
    }

    protected override bool CanUseActiveDuringPhase(TurnPhase phase)
    {
        return phase == TurnPhase.WaitThrow && !PlayersWhoHaveThrown.Contains(PlayerId);
    }
}
