using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_019_Status : CharacterStatusBehaviour
{
    private static readonly Dictionary<int, int> AdditionalYutMoAllowance =
        new Dictionary<int, int>();
    private static readonly HashSet<int> ActiveRulePlayers = new HashSet<int>();

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        AdditionalYutMoAllowance[PlayerId] = 1;
        ActiveRulePlayers.Remove(PlayerId);
    }

    public override bool ShouldGrantExtraThrow(YutResult result, bool defaultValue)
    {
        if (ActiveRulePlayers.Remove(PlayerId))
        {
            return result == YutResult.Do ||
                   result == YutResult.Gae ||
                   result == YutResult.Geol ||
                   result == YutResult.BackDo;
        }

        if (defaultValue) return true;
        if (result != YutResult.Yut && result != YutResult.Mo) return false;
        if (!AdditionalYutMoAllowance.TryGetValue(PlayerId, out int allowance))
            allowance = 1;
        if (allowance <= 0) return false;

        AdditionalYutMoAllowance[PlayerId] = allowance - 1;
        return true;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        ActiveRulePlayers.Add(PlayerId);
        return CharacterActiveResult.Success(
            "The next resolved throw grants an extra throw for Do, Gae, Geol, or BackDo only.");
    }
}
