using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_001_1_Status : CharacterStatusBehaviour
{
    private static readonly HashSet<int> ForceDoOrMoPlayers = new HashSet<int>();
    private bool firstMovePassiveAvailable = true;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ForceDoOrMoPlayers.Clear();
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
    }

    protected override bool CanUseActiveDuringPhase(TurnPhase phase)
    {
        return phase == TurnPhase.WaitThrow;
    }
}
