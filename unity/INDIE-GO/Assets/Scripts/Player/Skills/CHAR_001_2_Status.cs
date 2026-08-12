using YutArena.InGame;

public sealed class CHAR_001_2_Status : CharacterStatusBehaviour
{
    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (Turns == null)
            return CharacterActiveResult.Failure("TestTurnManager is not available.");

        Turns.GrantSkillExtraThrow();
        return CharacterActiveResult.Success("One skill extra throw was granted.");
    }
}
