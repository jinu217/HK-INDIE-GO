using System;
using System.Collections.Generic;
using YutArena.Common;
using YutArena.Managers;

/// <summary>
/// Player 폴더 밖의 인게임 코드가 캐릭터 구현을 호출하는 단일 진입점입니다.
/// 캐릭터 프리팹 네 개가 등록되어도 플레이어/말 ID로 정확히 한 구현만 조회합니다.
/// </summary>
public static class CharacterSkillRegistry
{
    private static readonly Dictionary<(int playerId, int pieceId), CharacterStatusBehaviour>
        Behaviours = new Dictionary<(int, int), CharacterStatusBehaviour>();
    private static readonly HashSet<TestYutRuleManager> BridgedYutManagers =
        new HashSet<TestYutRuleManager>();
    private static readonly HashSet<TestTurnManager> BridgedTurnManagers =
        new HashSet<TestTurnManager>();

    /// <summary>
    /// SP를 직접 변경하지 않고 플레이어 시스템에 획득 요청만 전달합니다.
    /// </summary>
    public static event Action<int, int> SkillPointRequested;

    internal static int Register(
        PlayerController owner,
        int requestedPieceId,
        CharacterStatusBehaviour behaviour)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (behaviour == null) throw new ArgumentNullException(nameof(behaviour));
        if (!owner.IsInitialized || owner.PlayerId <= 0)
            throw new InvalidOperationException("PlayerController must be initialized before character registration.");

        int pieceId = requestedPieceId;
        if (pieceId < 0 || IsOccupiedByAnother(owner.PlayerId, pieceId, behaviour))
            pieceId = FindAvailablePieceId(owner);

        if (!owner.TryGetPieceData(pieceId, out _))
            throw new ArgumentOutOfRangeException(
                nameof(requestedPieceId),
                $"Player {owner.PlayerId} has no piece with ID {pieceId}.");

        Behaviours[(owner.PlayerId, pieceId)] = behaviour;
        return pieceId;
    }

    internal static void Unregister(CharacterStatusBehaviour behaviour)
    {
        if (behaviour == null) return;

        var keysToRemove = new List<(int playerId, int pieceId)>();
        foreach (KeyValuePair<(int playerId, int pieceId), CharacterStatusBehaviour> entry in Behaviours)
        {
            if (entry.Value == behaviour)
                keysToRemove.Add(entry.Key);
        }

        foreach ((int playerId, int pieceId) key in keysToRemove)
            Behaviours.Remove(key);
    }

    public static bool TryGet(
        int playerId,
        int pieceId,
        out CharacterStatusBehaviour behaviour)
    {
        return Behaviours.TryGetValue((playerId, pieceId), out behaviour) && behaviour != null;
    }

    public static int ModifyMoveCount(CharacterMoveRequest request)
    {
        return TryGet(request.PlayerId, request.PieceId, out CharacterStatusBehaviour behaviour)
            ? behaviour.ModifyMoveCount(request)
            : request.MoveCount;
    }

    public static (YutResult, float)[] ModifyYutProbability(
        int playerId,
        (YutResult, float)[] currentTable)
    {
        (YutResult, float)[] result = currentTable;
        foreach (CharacterStatusBehaviour behaviour in SnapshotForPlayer(playerId))
            result = behaviour.ModifyYutProbability(result);
        return result;
    }

    public static bool ShouldGrantExtraThrow(
        int playerId,
        YutResult result,
        bool defaultValue)
    {
        bool resolved = defaultValue;
        foreach (CharacterStatusBehaviour behaviour in SnapshotForPlayer(playerId))
            resolved = behaviour.ShouldGrantExtraThrow(result, resolved);
        return resolved;
    }

    public static int GetYutMoExtraThrowLimit(int playerId, int baseLimit)
    {
        int bonus = 0;
        foreach (CharacterStatusBehaviour behaviour in SnapshotForPlayer(playerId))
            bonus = Math.Max(bonus, behaviour.YutMoExtraThrowLimitBonus);
        return Math.Max(0, baseLimit + bonus);
    }

    public static CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        return TryGet(request.TargetPlayerId, request.TargetPieceId, out CharacterStatusBehaviour target)
            ? target.EvaluateIncomingCapture(request)
            : CharacterCaptureDecision.Proceed;
    }

    public static void NotifyPieceEnteredBoard(int playerId, int pieceId)
    {
        if (TryGet(playerId, pieceId, out CharacterStatusBehaviour behaviour))
            behaviour.OnPieceEnteredBoard();
    }

    public static void NotifyPieceRetired(int playerId, int pieceId)
    {
        if (TryGet(playerId, pieceId, out CharacterStatusBehaviour behaviour))
            behaviour.OnPieceRetired();
    }

    public static void NotifyMoveCompleted(CharacterMoveRecord record)
    {
        if (TryGet(record.PlayerId, record.PieceId, out CharacterStatusBehaviour behaviour))
            behaviour.OnMoveCompleted(record);

        foreach (CharacterStatusBehaviour candidate in SnapshotBehaviours())
            candidate.OnAnyPieceMoveCompleted(record);
    }

    public static void NotifyCaptureCompleted(CharacterCaptureRequest request)
    {
        if (TryGet(request.AttackerPlayerId, request.AttackerPieceId, out CharacterStatusBehaviour attacker))
            attacker.OnCaptureCompleted(request);
    }

    public static void NotifyOwnerTurnStarted(int playerId)
    {
        foreach (CharacterStatusBehaviour behaviour in SnapshotForPlayer(playerId))
            behaviour.OnOwnerTurnStarted();
    }

    public static void NotifyOwnerTurnEnded(int playerId)
    {
        foreach (CharacterStatusBehaviour behaviour in SnapshotForPlayer(playerId))
            behaviour.OnOwnerTurnEnded();
    }

    public static CharacterActiveResult TryUseActive(CharacterActiveRequest request)
    {
        if (!TryGet(request.PlayerId, request.CasterPieceId, out CharacterStatusBehaviour behaviour))
            return CharacterActiveResult.Failure(
                $"No character skill is registered for Player {request.PlayerId}, Piece {request.CasterPieceId}.");

        return behaviour.TryUseActive(request);
    }

    public static bool IsTargetable(int playerId, int pieceId)
    {
        return !TryGet(playerId, pieceId, out CharacterStatusBehaviour behaviour) || behaviour.IsTargetable;
    }

    internal static void RequestSkillPoint(int playerId, int amount = 1)
    {
        if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        SkillPointRequested?.Invoke(playerId, amount);
    }

    internal static void EnsureManagerBridges(
        TestYutRuleManager yutRules,
        TestTurnManager turns)
    {
        if (yutRules != null && BridgedYutManagers.Add(yutRules))
        {
            Func<YutArena.Common.PlayerSlot, (YutResult, float)[]> previous =
                yutRules.ProbabilityTableProvider;
            yutRules.ProbabilityTableProvider = player =>
            {
                (YutResult, float)[] baseTable = previous != null ? previous(player) : null;
                return ModifyYutProbability((int)player, baseTable);
            };
        }

        if (turns != null && BridgedTurnManagers.Add(turns))
        {
            turns.OnTurnStarted += player => NotifyOwnerTurnStarted((int)player);
            turns.OnTurnEnded += player => NotifyOwnerTurnEnded((int)player);
        }
    }

    private static bool IsOccupiedByAnother(
        int playerId,
        int pieceId,
        CharacterStatusBehaviour behaviour)
    {
        return Behaviours.TryGetValue((playerId, pieceId), out CharacterStatusBehaviour existing) &&
               existing != null && existing != behaviour;
    }

    private static int FindAvailablePieceId(PlayerController owner)
    {
        for (int pieceId = 0; pieceId < owner.RuntimeData.Pieces.Count; pieceId++)
        {
            if (!Behaviours.ContainsKey((owner.PlayerId, pieceId)) ||
                Behaviours[(owner.PlayerId, pieceId)] == null)
                return pieceId;
        }

        throw new InvalidOperationException(
            $"Player {owner.PlayerId} already has a character component for every piece.");
    }

    private static List<CharacterStatusBehaviour> SnapshotForPlayer(int playerId)
    {
        var result = new List<CharacterStatusBehaviour>();
        foreach (KeyValuePair<(int playerId, int pieceId), CharacterStatusBehaviour> entry in Behaviours)
        {
            if (entry.Key.playerId == playerId && entry.Value != null && !result.Contains(entry.Value))
                result.Add(entry.Value);
        }

        return result;
    }

    private static List<CharacterStatusBehaviour> SnapshotBehaviours()
    {
        var result = new List<CharacterStatusBehaviour>();
        foreach (CharacterStatusBehaviour behaviour in Behaviours.Values)
        {
            if (behaviour != null && !result.Contains(behaviour))
                result.Add(behaviour);
        }

        return result;
    }
}
