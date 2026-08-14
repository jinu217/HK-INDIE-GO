using YutArena.InGame;
using YutArena.Common;

public sealed class CHAR_001_2_Status : CharacterStatusBehaviour
{
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
