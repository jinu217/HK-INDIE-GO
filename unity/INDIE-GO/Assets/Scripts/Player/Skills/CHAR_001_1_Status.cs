using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_001_1_Status : CharacterStatusBehaviour
{
    private static readonly HashSet<int> ForceDoOrMoPlayers = new HashSet<int>();
    //수정: 첫 던지기 이후의 윷/모 및 잡기 재던지기 단계에서는 액티브를 막습니다.
    private static readonly HashSet<int> PlayersWhoHaveThrown = new HashSet<int>();
    private bool firstMovePassiveAvailable = true;

    //수정: 모 아니면 도는 특정 말이 아닌 플레이어의 다음 윷 결과에 적용됩니다.
    public override bool RequiresCasterPieceSelection => false;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ForceDoOrMoPlayers.Clear();
        PlayersWhoHaveThrown.Clear();
    }

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        PlayersWhoHaveThrown.Remove(PlayerId);
    }

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (!firstMovePassiveAvailable || !request.IsFirstBoardMove ||
            request.IsActiveSkillMove || !TryStartPassiveCooldown())
            return request.MoveCount;

        firstMovePassiveAvailable = false;
        int modifiedMoveCount = request.MoveCount < 0
            ? request.MoveCount - 1
            : request.MoveCount + 1;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_001_1_Status)} first-move bonus: " +
            $"{request.MoveCount} -> {modifiedMoveCount}. Player={PlayerId}, Piece={PieceId}",
            this);
        return modifiedMoveCount;
    }

    public override void OnPieceRetired()
    {
        firstMovePassiveAvailable = true;
        ResetPassiveCooldown();
    }

    public override (YutResult, float)[] ModifyYutProbability(
        (YutResult, float)[] currentTable)
    {
        if (!ForceDoOrMoPlayers.Remove(PlayerId)) return currentTable;

        UnityEngine.Debug.Log(
            $"[CharacterSkill][ActiveEffect] {nameof(CHAR_001_1_Status)} applied Do/Mo table. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);

        return new[]
        {
            (YutResult.Do, 50f),
            (YutResult.Mo, 50f)
        };
    }

    //수정: 첫 번째 윷 결과가 확정되는 시점부터 이번 턴에는 액티브를 사용할 수 없습니다.
    public override bool ShouldGrantExtraThrow(YutResult result, bool defaultValue)
    {
        PlayersWhoHaveThrown.Add(PlayerId);
        return defaultValue;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        ForceDoOrMoPlayers.Add(PlayerId);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_001_1_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The next throw is limited to Do or Mo at 50% each.");
    }

    public override void OnOwnerTurnEnded()
    {
        ForceDoOrMoPlayers.Remove(PlayerId);
        PlayersWhoHaveThrown.Remove(PlayerId);
    }

    protected override bool CanUseActiveDuringPhase(TurnPhase phase)
    {
        return phase == TurnPhase.WaitThrow && !PlayersWhoHaveThrown.Contains(PlayerId);
    }
}
