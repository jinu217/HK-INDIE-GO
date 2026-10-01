using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_019_Status : CharacterStatusBehaviour
{
    private static readonly Dictionary<int, int> AdditionalYutMoAllowance =
        new Dictionary<int, int>();

    public override bool CanSelectAsActiveCaster(
        PlayerRuntimeData.PieceRuntimeData piece)
    {
        return CcEffectService.CanUseSkill(piece);
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        AdditionalYutMoAllowance.Clear();
    }

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        AdditionalYutMoAllowance[PlayerId] = 1;
    }

    public override bool ShouldGrantExtraThrow(YutResult result, bool defaultValue)
    {
        if (CcEffectService.HasPlayerEffect(PlayerId, CcDefine.ReverseExtraThrow))
            return base.ShouldGrantExtraThrow(result, defaultValue);

        if (defaultValue) return true;
        if (result != YutResult.Yut && result != YutResult.Mo) return false;
        if (!AdditionalYutMoAllowance.TryGetValue(PlayerId, out int allowance))
            allowance = 1;
        if (allowance <= 0) return false;
        if (!TryStartPassiveCooldown()) return false;

        AdditionalYutMoAllowance[PlayerId] = allowance - 1;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_019_Status)} granted an additional " +
            $"throw for {result}. Player={PlayerId}, Piece={PieceId}",
            this);
        ApplyEffect(CcDefine.YutMoExtraThrow);
        return base.ShouldGrantExtraThrow(result, defaultValue);
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        ApplyEffect(CcDefine.ReverseExtraThrow);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_019_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            "The next resolved throw grants an extra throw for Do, Gae, Geol, or BackDo only.");
    }

    public override void OnOwnerTurnEnded()
    {
    }

    protected override bool CanUseActiveDuringPhase(TurnPhase phase)
    {
        return phase == TurnPhase.WaitThrow;
    }
}
