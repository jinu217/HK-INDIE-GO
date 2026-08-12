using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_019_Status : CharacterStatusBehaviour
{
    private static readonly HashSet<int> ActiveRulePlayers = new HashSet<int>();

    public override int YutMoExtraThrowLimitBonus => 1;

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
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

        return defaultValue;
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
