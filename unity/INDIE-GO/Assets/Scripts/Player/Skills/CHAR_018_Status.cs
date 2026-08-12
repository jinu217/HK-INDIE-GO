using System.Collections.Generic;
using UnityEngine;
using YutArena.InGame;

public sealed class CHAR_018_Status : CharacterStatusBehaviour
{
    private int markedPlayerId = -1;
    private int markedPieceId = -1;

    public override void OnPieceEnteredBoard()
    {
        List<CharacterPieceReference> enemies = CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId);
        enemies.RemoveAll(reference =>
            !CharacterSkillRegistry.IsTargetable(
                reference.Player.PlayerId,
                reference.Piece.PieceId));

        if (enemies.Count == 0)
        {
            ClearMark();
            return;
        }

        CharacterPieceReference selected = enemies[Random.Range(0, enemies.Count)];
        markedPlayerId = selected.Player.PlayerId;
        markedPieceId = selected.Piece.PieceId;
    }

    public override void OnPieceRetired()
    {
        ClearMark();
    }

    public override void OnCaptureCompleted(CharacterCaptureRequest request)
    {
        if (request.TargetPlayerId != markedPlayerId || request.TargetPieceId != markedPieceId)
            return;

        RequestSkillPoint();
        ClearMark();
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (!request.HasTarget)
            return CharacterActiveResult.Failure("Assassination requires an enemy target.");
        if (!TryGetPiece(request.TargetPlayerId, request.TargetPieceId, out CharacterPieceReference target))
            return CharacterActiveResult.Failure("The selected target does not exist.");
        if (target.Player.PlayerId == PlayerId || target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Assassination can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");

        caster.MoveTo(target.Piece.CurrentTileId);
        CharacterBoardUtility.Retire(target.Piece, true);

        var capture = new CharacterCaptureRequest(
            PlayerId,
            PieceId,
            target.Player.PlayerId,
            target.Piece.PieceId,
            1,
            true);
        OnCaptureCompleted(capture);

        return CharacterActiveResult.Success("Moved to and captured the selected enemy.");
    }

    private void ClearMark()
    {
        markedPlayerId = -1;
        markedPieceId = -1;
    }
}
