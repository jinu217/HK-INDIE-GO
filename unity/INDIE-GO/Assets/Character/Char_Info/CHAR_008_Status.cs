using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

public sealed class CHAR_008_Status : CharacterStatusBehaviour
{
    private static readonly Dictionary<int, IReadOnlyList<BoardTileId>> PendingLastPaths =
        new Dictionary<int, IReadOnlyList<BoardTileId>>();
    private static readonly Dictionary<int, IReadOnlyList<BoardTileId>> ActiveWindPaths =
        new Dictionary<int, IReadOnlyList<BoardTileId>>();
    private static readonly HashSet<(int playerId, int pieceId)> TriggeredPieces =
        new HashSet<(int, int)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        PendingLastPaths.Clear();
        ActiveWindPaths.Clear();
        TriggeredPieces.Clear();
    }

    public override void OnMoveCompleted(CharacterMoveRecord record)
    {
        PendingLastPaths[PlayerId] = record.Path;
    }

    public override void OnOwnerTurnStarted()
    {
        base.OnOwnerTurnStarted();
        TriggeredPieces.RemoveWhere(key => key.playerId == PlayerId);
    }

    public override void OnOwnerTurnEnded()
    {
        if (!PendingLastPaths.TryGetValue(PlayerId, out IReadOnlyList<BoardTileId> path))
            return;

        ActiveWindPaths[PlayerId] = path;
        PendingLastPaths.Remove(PlayerId);
    }

    public override void OnAnyPieceMoveCompleted(CharacterMoveRecord record)
    {
        if (record.PlayerId != PlayerId ||
            !ActiveWindPaths.TryGetValue(PlayerId, out IReadOnlyList<BoardTileId> activeWindPath) ||
            TriggeredPieces.Contains((PlayerId, record.PieceId)) ||
            !Contains(activeWindPath, record.To) ||
            !TryStartPassiveCooldown())
            return;

        if (Movement == null) return;
        TriggeredPieces.Add((PlayerId, record.PieceId));
        if (Movement.TryMovePiece(record.PlayerId, record.PieceId, 1, true))
        {
            UnityEngine.Debug.Log(
                $"[CharacterSkill][Passive] {nameof(CHAR_008_Status)} moved " +
                $"Player={record.PlayerId}, Piece={record.PieceId} by 1. " +
                $"Owner={PlayerId}, Piece={PieceId}",
                this);
        }
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (!request.HasTarget)
        {
            if (!TryFindAutomaticTarget(caster, out CharacterPieceReference automaticTarget))
                return CharacterActiveResult.Failure("There is no target within five tiles.");

            return BindTarget(caster, automaticTarget);
        }

        if (!TryGetPiece(request.TargetPlayerId, request.TargetPieceId, out CharacterPieceReference target))
            return CharacterActiveResult.Failure("The selected target does not exist.");

        return BindTarget(caster, target);
    }

    private CharacterActiveResult BindTarget(
        PlayerRuntimeData.PieceRuntimeData caster,
        CharacterPieceReference target)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Spirit Arrow requires a piece on the board.");
        if (target.Player.PlayerId == PlayerId || target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Spirit Arrow can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");
        if (!CharacterBoardUtility.IsWithinDistance(
                caster.CurrentTileId,
                target.Piece.CurrentTileId,
                5))
            return CharacterActiveResult.Failure("The selected enemy is farther than five tiles.");

        // CC is decremented at the start of its owner's turn. Two stored
        // ticks therefore produce one complete turn in which movement is blocked.
        target.Piece.SetCc(CcDefine.Stun, 2);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_008_Status)} activated against " +
            $"Player={target.Player.PlayerId}, Piece={target.Piece.PieceId}. " +
            $"Owner={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The selected enemy was bound for one turn.");
    }

    private bool TryFindAutomaticTarget(
        PlayerRuntimeData.PieceRuntimeData caster,
        out CharacterPieceReference target)
    {
        target = default;
        if (caster.State != PieceState.InBoard) return false;

        int nearestDistance = int.MaxValue;
        foreach (CharacterPieceReference enemy in
                 CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
        {
            if (!CharacterSkillRegistry.IsTargetable(
                    enemy.Player.PlayerId,
                    enemy.Piece.PieceId))
                continue;

            int distance = CharacterBoardUtility.GetDistance(
                caster.CurrentTileId,
                enemy.Piece.CurrentTileId);
            if (distance > 5 || distance >= nearestDistance) continue;

            nearestDistance = distance;
            target = enemy;
        }

        return nearestDistance != int.MaxValue;
    }

    private static bool Contains(IReadOnlyList<BoardTileId> path, BoardTileId tile)
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == tile) return true;
        }

        return false;
    }
}
