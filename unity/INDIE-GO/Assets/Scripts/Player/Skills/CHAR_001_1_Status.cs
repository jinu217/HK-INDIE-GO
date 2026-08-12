using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_001_1_Status : CharacterStatusBehaviour
{
    private static readonly HashSet<int> ForceDoOrMoPlayers = new HashSet<int>();
    private bool firstMovePassiveAvailable = true;

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        if (!firstMovePassiveAvailable || !request.IsFirstBoardMove)
            return request.MoveCount;

        firstMovePassiveAvailable = false;
        return request.MoveCount < 0
            ? request.MoveCount - 1
            : request.MoveCount + 1;
    }

    public override void OnPieceRetired()
    {
        firstMovePassiveAvailable = true;
    }

    public override (YutResult, float)[] ModifyYutProbability(
        (YutResult, float)[] currentTable)
    {
        if (!ForceDoOrMoPlayers.Remove(PlayerId)) return currentTable;

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
        return CharacterActiveResult.Success("The next throw is limited to Do or Mo at 50% each.");
    }
}
