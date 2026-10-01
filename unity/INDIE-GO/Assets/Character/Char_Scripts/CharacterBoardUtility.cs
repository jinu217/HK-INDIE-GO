using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

/// <summary>
/// 캐릭터 스킬이 보드 조회와 직접 효과 처리에 사용하는 공통 도구입니다.
/// PieceMovementManager의 기본 이동 규칙을 변경하지 않고 동일한 타일 연결만 참조합니다.
/// </summary>
public static class CharacterBoardUtility
{
    private static readonly Dictionary<BoardTileId, List<BoardTileId>> Graph = BuildGraph();

    public static bool TryGetPiece(
        PlayerManager players,
        int playerId,
        int pieceId,
        out CharacterPieceReference reference)
    {
        if (players != null &&
            players.TryGetPlayer(playerId, out PlayerController player) &&
            player.TryGetPieceData(pieceId, out PlayerRuntimeData.PieceRuntimeData piece))
        {
            reference = new CharacterPieceReference(player, piece);
            return true;
        }

        reference = default;
        return false;
    }

    public static List<CharacterPieceReference> GetPiecesOnBoard(PlayerManager players)
    {
        var result = new List<CharacterPieceReference>();
        if (players == null) return result;

        foreach (PlayerController player in players.ActivePlayers)
        {
            if (player == null || player.RuntimeData == null) continue;

            foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
            {
                if (piece.State == PieceState.InBoard)
                    result.Add(new CharacterPieceReference(player, piece));
            }
        }

        return result;
    }

    public static List<CharacterPieceReference> GetEnemiesOnBoard(
        PlayerManager players,
        int ownerPlayerId)
    {
        List<CharacterPieceReference> all = GetPiecesOnBoard(players);
        all.RemoveAll(reference => reference.Player.PlayerId == ownerPlayerId);
        return all;
    }

    public static CharacterPieceReference? FindNearestAlly(
        PlayerManager players,
        int ownerPlayerId,
        int sourcePieceId,
        BoardTileId sourceTile)
    {
        if (players == null || !players.TryGetPlayer(ownerPlayerId, out PlayerController owner))
            return null;

        CharacterPieceReference? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in owner.RuntimeData.Pieces)
        {
            if (piece.PieceId == sourcePieceId || piece.State != PieceState.InBoard)
                continue;

            int distance = GetDistance(sourceTile, piece.CurrentTileId);
            if (distance < nearestDistance)
            {
                nearest = new CharacterPieceReference(owner, piece);
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public static int GetDistance(BoardTileId from, BoardTileId to)
    {
        if (from == to) return 0;

        var visited = new HashSet<BoardTileId> { from };
        var queue = new Queue<(BoardTileId tile, int distance)>();
        queue.Enqueue((from, 0));

        while (queue.Count > 0)
        {
            (BoardTileId tile, int distance) = queue.Dequeue();
            if (!Graph.TryGetValue(tile, out List<BoardTileId> neighbours)) continue;

            foreach (BoardTileId neighbour in neighbours)
            {
                if (!visited.Add(neighbour)) continue;
                if (neighbour == to) return distance + 1;
                queue.Enqueue((neighbour, distance + 1));
            }
        }

        return int.MaxValue;
    }

    public static bool IsWithinDistance(BoardTileId from, BoardTileId to, int maxDistance)
    {
        if (maxDistance < 0) throw new ArgumentOutOfRangeException(nameof(maxDistance));
        return GetDistance(from, to) <= maxDistance;
    }

    public static List<BoardTileId> GetForwardPath(
        PlayerRuntimeData.PieceRuntimeData piece,
        int stepCount)
    {
        if (piece == null) throw new ArgumentNullException(nameof(piece));
        if (stepCount < 0) throw new ArgumentOutOfRangeException(nameof(stepCount));

        var result = new List<BoardTileId>();
        BoardTileId current = piece.CurrentTileId;
        BoardTileId previous = piece.PreviousTileId;

        for (int step = 0; step < stepCount; step++)
        {
            if (current == BoardTileId.None && (piece.State == PieceState.InBoard || step > 0))
            {
                result.Add(BoardTileId.None); // 공유 출발 칸을 지나 완주. 그 이후 경로는 생성하지 않습니다.
                break;
            }
            BoardTileId next = GetNextForwardTile(current, previous, step == 0);
            result.Add(next);
            previous = current;
            current = next;
        }

        return result;
    }

    public static BoardTileId GetNextForwardTile(
        BoardTileId current,
        BoardTileId previous,
        bool isStartingThisMove)
    {
        if (current == BoardTileId.None) return BoardTileId.Outer01;
        if (isStartingThisMove && current == BoardTileId.Corner01) return BoardTileId.Inner01;
        if (isStartingThisMove && current == BoardTileId.Corner02) return BoardTileId.Inner05;
        if (isStartingThisMove && current == BoardTileId.Center) return BoardTileId.Inner07;

        switch (current)
        {
            case BoardTileId.Outer01: return BoardTileId.Outer02;
            case BoardTileId.Outer02: return BoardTileId.Outer03;
            case BoardTileId.Outer03: return BoardTileId.Outer04;
            case BoardTileId.Outer04: return BoardTileId.Corner01;
            case BoardTileId.Corner01: return BoardTileId.Outer05;
            case BoardTileId.Outer05: return BoardTileId.Outer06;
            case BoardTileId.Outer06: return BoardTileId.Outer07;
            case BoardTileId.Outer07: return BoardTileId.Outer08;
            case BoardTileId.Outer08: return BoardTileId.Corner02;
            case BoardTileId.Corner02: return BoardTileId.Outer09;
            case BoardTileId.Outer09: return BoardTileId.Outer10;
            case BoardTileId.Outer10: return BoardTileId.Outer11;
            case BoardTileId.Outer11: return BoardTileId.Outer12;
            case BoardTileId.Outer12: return BoardTileId.Corner03;
            case BoardTileId.Corner03: return BoardTileId.Outer13;
            case BoardTileId.Outer13: return BoardTileId.Outer14;
            case BoardTileId.Outer14: return BoardTileId.Outer15;
            case BoardTileId.Outer15: return BoardTileId.Outer16;
            case BoardTileId.Outer16: return BoardTileId.None;
            case BoardTileId.Inner01: return BoardTileId.Inner02;
            case BoardTileId.Inner02: return BoardTileId.Center;
            case BoardTileId.Inner03: return BoardTileId.Inner04;
            case BoardTileId.Inner04: return BoardTileId.Corner03;
            case BoardTileId.Inner05: return BoardTileId.Inner06;
            case BoardTileId.Inner06: return BoardTileId.Center;
            case BoardTileId.Inner07: return BoardTileId.Inner08;
            case BoardTileId.Inner08: return BoardTileId.None;
            case BoardTileId.Center:
                return previous == BoardTileId.Inner02
                    ? BoardTileId.Inner03
                    : BoardTileId.Inner07;
            default:
                throw new ArgumentOutOfRangeException(nameof(current), current, "Undefined board tile.");
        }
    }

    public static BoardTileId GetNextBackwardTile(
        BoardTileId current,
        BoardTileId previous)
    {
        switch (current)
        {
            case BoardTileId.None:
                if (previous == BoardTileId.Outer01) return BoardTileId.Outer16;
                if (previous == BoardTileId.Inner08) return BoardTileId.Inner08;
                if (previous == BoardTileId.Outer16) return BoardTileId.Outer16;
                return BoardTileId.None;
            case BoardTileId.Outer01: return BoardTileId.None;
            case BoardTileId.Outer02: return BoardTileId.Outer01;
            case BoardTileId.Outer03: return BoardTileId.Outer02;
            case BoardTileId.Outer04: return BoardTileId.Outer03;
            case BoardTileId.Corner01: return BoardTileId.Outer04;
            case BoardTileId.Outer05: return BoardTileId.Corner01;
            case BoardTileId.Outer06: return BoardTileId.Outer05;
            case BoardTileId.Outer07: return BoardTileId.Outer06;
            case BoardTileId.Outer08: return BoardTileId.Outer07;
            case BoardTileId.Corner02: return BoardTileId.Outer08;
            case BoardTileId.Outer09: return BoardTileId.Corner02;
            case BoardTileId.Outer10: return BoardTileId.Outer09;
            case BoardTileId.Outer11: return BoardTileId.Outer10;
            case BoardTileId.Outer12: return BoardTileId.Outer11;
            case BoardTileId.Corner03:
                return previous == BoardTileId.Inner04 ? BoardTileId.Inner04 : BoardTileId.Outer12;
            case BoardTileId.Outer13: return BoardTileId.Corner03;
            case BoardTileId.Outer14: return BoardTileId.Outer13;
            case BoardTileId.Outer15: return BoardTileId.Outer14;
            case BoardTileId.Outer16: return BoardTileId.Outer15;
            case BoardTileId.Inner01: return BoardTileId.Corner01;
            case BoardTileId.Inner02: return BoardTileId.Inner01;
            case BoardTileId.Inner03: return BoardTileId.Center;
            case BoardTileId.Inner04: return BoardTileId.Inner03;
            case BoardTileId.Inner05: return BoardTileId.Corner02;
            case BoardTileId.Inner06: return BoardTileId.Inner05;
            case BoardTileId.Inner07: return BoardTileId.Center;
            case BoardTileId.Inner08: return BoardTileId.Inner07;
            case BoardTileId.Center:
                return previous == BoardTileId.Inner03 || previous == BoardTileId.Inner02
                    ? BoardTileId.Inner02
                    : BoardTileId.Inner06;
            default:
                throw new ArgumentOutOfRangeException(nameof(current), current, "Undefined board tile.");
        }
    }

    // 호환용 위임. 보드 효과 구현은 CcBoardEffects에만 존재합니다.
    public static void Retire(CharacterPieceReference target, bool grantsExtraThrow) =>
        CcBoardEffects.Retire(target, grantsExtraThrow);

    public static bool TryCapture(int attackerPlayerId, int attackerPieceId,
        CharacterPieceReference target, int attackingPieceCount, bool grantsExtraThrow,
        out CharacterCaptureDecision decision) =>
        CcBoardEffects.TryCapture(attackerPlayerId, attackerPieceId, target,
            attackingPieceCount, grantsExtraThrow, out decision);

    public static void MoveStackAlongPath(PlayerController owner,
        PlayerRuntimeData.PieceRuntimeData caster, IReadOnlyList<BoardTileId> path,
        bool ignoresInstalledItems = false) =>
        CcEffectService.Apply(caster, CcDefine.MovePath, value: ignoresInstalledItems ? 2 : 1,
            sourcePlayerId: owner.PlayerId, sourcePieceId: caster.PieceId, path: path);

    private static Dictionary<BoardTileId, List<BoardTileId>> BuildGraph()
    {
        var graph = new Dictionary<BoardTileId, List<BoardTileId>>();

        AddPath(graph, new[]
        {
            BoardTileId.None, BoardTileId.Outer01, BoardTileId.Outer02,
            BoardTileId.Outer03, BoardTileId.Outer04, BoardTileId.Corner01,
            BoardTileId.Outer05, BoardTileId.Outer06, BoardTileId.Outer07,
            BoardTileId.Outer08, BoardTileId.Corner02, BoardTileId.Outer09,
            BoardTileId.Outer10, BoardTileId.Outer11, BoardTileId.Outer12,
            BoardTileId.Corner03, BoardTileId.Outer13, BoardTileId.Outer14,
            BoardTileId.Outer15, BoardTileId.Outer16, BoardTileId.None
        });
        AddPath(graph, new[]
        {
            BoardTileId.Corner01, BoardTileId.Inner01, BoardTileId.Inner02,
            BoardTileId.Center, BoardTileId.Inner03, BoardTileId.Inner04,
            BoardTileId.Corner03
        });
        AddPath(graph, new[]
        {
            BoardTileId.Corner02, BoardTileId.Inner05, BoardTileId.Inner06,
            BoardTileId.Center, BoardTileId.Inner07, BoardTileId.Inner08,
            BoardTileId.None
        });

        return graph;
    }

    private static void AddPath(
        Dictionary<BoardTileId, List<BoardTileId>> graph,
        IReadOnlyList<BoardTileId> path)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            AddEdge(graph, path[i], path[i + 1]);
            AddEdge(graph, path[i + 1], path[i]);
        }
    }

    private static void AddEdge(
        Dictionary<BoardTileId, List<BoardTileId>> graph,
        BoardTileId from,
        BoardTileId to)
    {
        if (!graph.TryGetValue(from, out List<BoardTileId> neighbours))
        {
            neighbours = new List<BoardTileId>();
            graph[from] = neighbours;
        }

        if (!neighbours.Contains(to)) neighbours.Add(to);
    }
}

// CC가 결정한 보드 변경과 분신 표현만 실행합니다. 캐릭터별 발동 조건은 포함하지 않습니다.
public static class CcBoardEffects
{
    public static void Retire(
        CharacterPieceReference target,
        bool grantsExtraThrow)
    {
        CcEffectService.Apply(target.Piece, grantsExtraThrow ? CcDefine.Kill : CcDefine.Retire);
    }

    public static bool TryCapture(
        int attackerPlayerId,
        int attackerPieceId,
        CharacterPieceReference target,
        int attackingPieceCount,
        bool grantsExtraThrow,
        out CharacterCaptureDecision decision)
    {
        var request = new CharacterCaptureRequest(
            attackerPlayerId,
            attackerPieceId,
            target.Player.PlayerId,
            target.Piece.PieceId,
            attackingPieceCount,
            grantsExtraThrow);
        decision = CharacterSkillRegistry.EvaluateIncomingCapture(request);

        if (decision == CharacterCaptureDecision.Prevent ||
            decision == CharacterCaptureDecision.ConsumeCloneWithoutBonus ||
            decision == CharacterCaptureDecision.ConvertToParts)
        {
            return false;
        }

        bool finalExtraThrow =
            grantsExtraThrow &&
            decision != CharacterCaptureDecision.LimitRetireToAttackingCount;
        CcEffectService.RewardMarks(target.Piece, request);
        Retire(target, finalExtraThrow);
        CharacterSkillRegistry.NotifyCaptureCompleted(request);
        return true;
    }

    public static void MoveStackAlongPath(
        PlayerController owner,
        PlayerRuntimeData.PieceRuntimeData caster,
        IReadOnlyList<BoardTileId> path,
        bool ignoresInstalledItems = false)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (caster == null) throw new ArgumentNullException(nameof(caster));
        if (path == null) throw new ArgumentNullException(nameof(path));

        BoardTileId startingTile = caster.CurrentTileId;
        var movingPieces = new List<PlayerRuntimeData.PieceRuntimeData>();
        foreach (PlayerRuntimeData.PieceRuntimeData piece in owner.RuntimeData.Pieces)
        {
            if (piece.PieceId == caster.PieceId ||
                (caster.IsStacked && piece.StackGroupId == caster.StackGroupId))
            {
                movingPieces.Add(piece);
            }
        }

        if (!CcEffectService.CanMove(caster)) return;
        foreach (BoardTileId tile in path)
        {
            if (caster.State == PieceState.InBoard && caster.CurrentTileId == BoardTileId.None &&
                tile == BoardTileId.None)
            {
                foreach (var moving in movingPieces) moving.SetGoal();
                break;
            }
            foreach (PlayerRuntimeData.PieceRuntimeData piece in movingPieces)
                piece.MoveTo(tile);
        }

        if (path.Count > 0 && caster.State != PieceState.Goal)
            ResolveFriendlyStack(owner, movingPieces, path[path.Count - 1]);

        foreach (PlayerRuntimeData.PieceRuntimeData piece in movingPieces)
        {
            CharacterSkillRegistry.NotifyMoveCompleted(
                new CharacterMoveRecord(
                    owner.PlayerId,
                    piece.PieceId,
                    startingTile,
                    piece.CurrentTileId,
                    path,
                    ignoresInstalledItems));
        }
    }

    private static void ResolveFriendlyStack(
        PlayerController owner,
        IReadOnlyList<PlayerRuntimeData.PieceRuntimeData> movingPieces,
        BoardTileId landingTile)
    {
        PlayerRuntimeData.PieceRuntimeData stationaryPiece = null;
        var piecesOnTile = new List<PlayerRuntimeData.PieceRuntimeData>();
        foreach (PlayerRuntimeData.PieceRuntimeData piece in owner.RuntimeData.Pieces)
        {
            if (piece.State != PieceState.InBoard || piece.CurrentTileId != landingTile ||
                piece.Cc.Has(CcDefine.Parts))
                continue;

            piecesOnTile.Add(piece);
            if (stationaryPiece == null && !ContainsPiece(movingPieces, piece))
                stationaryPiece = piece;
        }

        if (stationaryPiece == null) return;

        int groupId = stationaryPiece.IsStacked
            ? stationaryPiece.StackGroupId
            : owner.RuntimeData.CreateStackGroupId();
        int leaderId = stationaryPiece.IsStacked
            ? stationaryPiece.StackLeaderPieceId
            : stationaryPiece.PieceId;
        foreach (PlayerRuntimeData.PieceRuntimeData piece in piecesOnTile)
            piece.SetStackGroup(groupId, leaderId);
    }

    private static bool ContainsPiece(
        IReadOnlyList<PlayerRuntimeData.PieceRuntimeData> pieces,
        PlayerRuntimeData.PieceRuntimeData target)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == target) return true;
        }

        return false;
    }


    public static void RefreshClones(PlayerController owner, PlayerRuntimeData.PieceRuntimeData piece)
    {
        if (owner == null) return;
        int count = piece.Cc.Get(CcDefine.Clone)?.Value ?? 0;
        if (count > 0 && !piece.IsStacked)
            piece.SetStackGroup(owner.RuntimeData.CreateStackGroupId(), piece.PieceId);
        else if (count == 0 && piece.IsStacked)
        {
            bool hasAlly = false;
            foreach (var other in owner.RuntimeData.Pieces)
                if (other != piece && other.StackGroupId == piece.StackGroupId) hasAlly = true;
            if (!hasAlly) piece.ClearStack();
        }
        if (!CharacterSkillRegistry.TryGet(owner.PlayerId, piece.PieceId, out var source)) return;
        var view = source.GetComponent<CcCloneView>();
        if (view == null && count > 0) view = source.gameObject.AddComponent<CcCloneView>();
        if (view != null) view.Refresh(piece, source);
    }

    internal static void NormalizeStack(PlayerController owner, int groupId)
    {
        if (owner == null || groupId < 0) return;
        var remaining = new List<PlayerRuntimeData.PieceRuntimeData>();
        foreach (var candidate in owner.RuntimeData.Pieces)
            if (candidate.State == PieceState.InBoard && candidate.StackGroupId == groupId)
                remaining.Add(candidate);
        if (remaining.Count == 0) return;
        int leader = remaining[0].PieceId;
        foreach (var candidate in remaining)
            if (candidate.PieceId == candidate.StackLeaderPieceId) leader = candidate.PieceId;
        foreach (var candidate in remaining)
        {
            if (remaining.Count == 1 && !candidate.Cc.Has(CcDefine.Clone)) candidate.ClearStack();
            else candidate.SetStackGroup(groupId, leader);
        }
    }
}

// 표현 전용. 분신 수/업기/잡기 규칙은 CcEffectService가 관리합니다.
public sealed class CcCloneView : MonoBehaviour
{
    private readonly List<GameObject> visuals = new List<GameObject>();

    public void Refresh(PlayerRuntimeData.PieceRuntimeData piece, CharacterStatusBehaviour source)
    {
        Clear();
        int count = piece.Cc.Get(CcDefine.Clone)?.Value ?? 0;
        for (int index = 0; index < count; index++)
        {
            var root = new GameObject($"SkillClone_{index + 1}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0.24f + index * 0.16f, 0.2f, 0);
            root.transform.localScale = Vector3.one * 0.65f;
            // 모델을 통째로 복제하지 않고 렌더러만 복제하여 스킬/콜라이더 중복 생성을 방지합니다.
            foreach (var mesh in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.GetComponent<MeshRenderer>() is not MeshRenderer renderer ||
                    mesh.GetComponentInParent<CcCloneView>() != this) continue;
                bool cloneChild = false;
                foreach (var visual in visuals)
                    if (mesh.transform.IsChildOf(visual.transform)) cloneChild = true;
                if (cloneChild) continue;
                var model = new GameObject("CloneMesh");
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = source.transform.InverseTransformPoint(mesh.transform.position);
                model.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * mesh.transform.rotation;
                Vector3 scale = source.transform.lossyScale;
                model.transform.localScale = new Vector3(mesh.transform.lossyScale.x / scale.x,
                    mesh.transform.lossyScale.y / scale.y, mesh.transform.lossyScale.z / scale.z);
                model.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                model.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            if (root.transform.childCount == 0)
            {
                var sprite = source.GetComponentInChildren<SpriteRenderer>();
                if (sprite != null)
                {
                    var renderer = root.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite.sprite;
                    renderer.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, 0.6f);
                    renderer.sortingLayerID = sprite.sortingLayerID;
                    renderer.sortingOrder = sprite.sortingOrder + index + 1;
                }
            }
            visuals.Add(root);
        }
    }

    private void OnDisable() { Clear(); }

    private void Clear()
    {
        foreach (var visual in visuals)
            if (visual != null)
            {
                visual.SetActive(false);
                visual.transform.SetParent(null);
                Destroy(visual);
            }
        visuals.Clear();
    }
}
