using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_007_Status : CharacterStatusBehaviour
{
    public override void OnCaptureCompleted(CharacterCaptureRequest request)
    {
        RequestSkillPoint();
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Frenzy Charge requires a piece on the board.");

        List<BoardTileId> path = GetPathToStraightEnd(caster);
        if (path.Count == 0)
            return CharacterActiveResult.Failure("No forward straight path is available.");

        CharacterPieceReference? firstEnemy = FindFirstEnemyOnPath(path);
        foreach (BoardTileId tile in path)
        {
            foreach (PlayerRuntimeData.PieceRuntimeData piece in Owner.RuntimeData.Pieces)
            {
                if (piece.PieceId == caster.PieceId ||
                    (caster.IsStacked && piece.StackGroupId == caster.StackGroupId))
                    piece.MoveTo(tile);
            }
        }

        if (firstEnemy.HasValue &&
            CharacterSkillRegistry.IsTargetable(
                firstEnemy.Value.Player.PlayerId,
                firstEnemy.Value.Piece.PieceId))
            firstEnemy.Value.Piece.SetCc(CcDefine.Stun, 1);

        // 설치형 아이템 시스템은 현재 프로젝트에 없으므로 무시 플래그를 전달할 대상이 없습니다.
        // 아이템 시스템 추가 시 이 액티브 결과에 item-ignore 컨텍스트를 연결해야 합니다.
        return CharacterActiveResult.Success(
            "Charged to the end of the straight path and stunned the first enemy ahead.");
    }

    private List<BoardTileId> GetPathToStraightEnd(PlayerRuntimeData.PieceRuntimeData caster)
    {
        var path = new List<BoardTileId>();
        BoardTileId current = caster.CurrentTileId;
        BoardTileId previous = caster.PreviousTileId;

        for (int i = 0; i < 20; i++)
        {
            BoardTileId next = CharacterBoardUtility.GetNextForwardTile(current, previous, i == 0);
            path.Add(next);
            previous = current;
            current = next;

            if (current == BoardTileId.None ||
                current == BoardTileId.Center ||
                current == BoardTileId.Corner01 ||
                current == BoardTileId.Corner02 ||
                current == BoardTileId.Corner03 ||
                current == BoardTileId.Corner04)
                break;
        }

        return path;
    }

    private CharacterPieceReference? FindFirstEnemyOnPath(IReadOnlyList<BoardTileId> path)
    {
        foreach (BoardTileId tile in path)
        {
            foreach (CharacterPieceReference enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
            {
                if (enemy.Piece.CurrentTileId == tile) return enemy;
            }
        }

        return null;
    }
}
