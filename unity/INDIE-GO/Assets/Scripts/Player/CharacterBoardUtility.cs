using System;
using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.GameCore;
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
        all.RemoveAll(reference => players.AreAllies(reference.Player.PlayerId, ownerPlayerId));
        return all;
    }

    public static CharacterPieceReference? FindNearestAlly(
        PlayerManager players,
        int ownerPlayerId,
        int sourcePieceId,
        BoardTileId sourceTile)
    {
        if (players == null || !players.TryGetPlayer(ownerPlayerId, out _))
            return null;

        CharacterPieceReference? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (PlayerController ally in players.ActivePlayers)
        {
            if (!players.AreAllies(ownerPlayerId, ally.PlayerId)) continue;

            foreach (PlayerRuntimeData.PieceRuntimeData piece in ally.RuntimeData.Pieces)
            {
                if ((ally.PlayerId == ownerPlayerId && piece.PieceId == sourcePieceId) ||
                    piece.State != PieceState.InBoard)
                    continue;

                int distance = GetDistance(sourceTile, piece.CurrentTileId);
                if (distance < nearestDistance)
                {
                    nearest = new CharacterPieceReference(ally, piece);
                    nearestDistance = distance;
                }
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
            BoardTileId next = BoardGraph.GetNextForward(current, previous, step == 0);
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

    public static void Retire(
        PlayerRuntimeData.PieceRuntimeData piece,
        bool grantsExtraThrow)
    {
        if (piece == null) throw new ArgumentNullException(nameof(piece));
        piece.SetCaptured(grantsExtraThrow ? CcDefine.Kill : CcDefine.Retire);
    }

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
