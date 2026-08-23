using System;
using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

/// <summary>
/// 잡기 판정 전에 캐릭터가 인게임 시스템에 반환하는 결정입니다.
/// PieceMovementManager는 실제 상태를 바꾸기 전에 이 값을 확인해야 합니다.
/// </summary>
public enum CharacterCaptureDecision
{
    Proceed = 0,
    Prevent,
    LimitRetireToAttackingCount,
    ConsumeCloneWithoutBonus,
    ConvertToParts
}

public readonly struct CharacterMoveRequest
{
    public CharacterMoveRequest(
        int playerId,
        int pieceId,
        int moveCount,
        bool isFirstBoardMove,
        bool isActiveSkillMove = false)
    {
        PlayerId = playerId;
        PieceId = pieceId;
        MoveCount = moveCount;
        IsFirstBoardMove = isFirstBoardMove;
        IsActiveSkillMove = isActiveSkillMove;
    }

    public int PlayerId { get; }
    public int PieceId { get; }
    public int MoveCount { get; }
    public bool IsFirstBoardMove { get; }
    public bool IsActiveSkillMove { get; }
}

public readonly struct CharacterCaptureRequest
{
    public CharacterCaptureRequest(
        int attackerPlayerId,
        int attackerPieceId,
        int targetPlayerId,
        int targetPieceId,
        int attackingPieceCount,
        bool wouldGrantExtraThrow)
    {
        AttackerPlayerId = attackerPlayerId;
        AttackerPieceId = attackerPieceId;
        TargetPlayerId = targetPlayerId;
        TargetPieceId = targetPieceId;
        AttackingPieceCount = Math.Max(1, attackingPieceCount);
        WouldGrantExtraThrow = wouldGrantExtraThrow;
    }

    public int AttackerPlayerId { get; }
    public int AttackerPieceId { get; }
    public int TargetPlayerId { get; }
    public int TargetPieceId { get; }
    public int AttackingPieceCount { get; }
    public bool WouldGrantExtraThrow { get; }
}

/// <summary>
/// 액티브 버튼이 나중에 전달할 요청 데이터입니다.
/// SP와 쿨타임 검증은 요청을 만들기 전에 플레이어 시스템에서 수행해야 합니다.
/// </summary>
public readonly struct CharacterActiveRequest
{
    public CharacterActiveRequest(
        int playerId,
        int casterPieceId,
        int targetPlayerId = -1,
        int targetPieceId = -1,
        YutResult selectedYutResult = YutResult.None)
    {
        PlayerId = playerId;
        CasterPieceId = casterPieceId;
        TargetPlayerId = targetPlayerId;
        TargetPieceId = targetPieceId;
        SelectedYutResult = selectedYutResult;
    }

    public int PlayerId { get; }
    public int CasterPieceId { get; }
    public int TargetPlayerId { get; }
    public int TargetPieceId { get; }
    public YutResult SelectedYutResult { get; }
    public bool HasTarget => TargetPlayerId > 0 && TargetPieceId >= 0;
}

public readonly struct CharacterActiveResult
{
    private CharacterActiveResult(bool succeeded, string message, bool suppressExtraThrow)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
        SuppressExtraThrow = suppressExtraThrow;
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public bool SuppressExtraThrow { get; }

    public static CharacterActiveResult Success(
        string message = "",
        bool suppressExtraThrow = false)
    {
        return new CharacterActiveResult(true, message, suppressExtraThrow);
    }

    public static CharacterActiveResult Failure(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A failure result requires a message.", nameof(message));

        return new CharacterActiveResult(false, message, false);
    }
}

public readonly struct CharacterPieceReference
{
    public CharacterPieceReference(
        PlayerController player,
        PlayerRuntimeData.PieceRuntimeData piece)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Piece = piece ?? throw new ArgumentNullException(nameof(piece));
    }

    public PlayerController Player { get; }
    public PlayerRuntimeData.PieceRuntimeData Piece { get; }
}

public readonly struct CharacterMoveRecord
{
    public CharacterMoveRecord(
        int playerId,
        int pieceId,
        BoardTileId from,
        BoardTileId to,
        IReadOnlyList<BoardTileId> path,
        bool ignoresInstalledItems = false)
    {
        PlayerId = playerId;
        PieceId = pieceId;
        From = from;
        To = to;
        Path = path ?? Array.Empty<BoardTileId>();
        IgnoresInstalledItems = ignoresInstalledItems;
    }

    public int PlayerId { get; }
    public int PieceId { get; }
    public BoardTileId From { get; }
    public BoardTileId To { get; }
    public IReadOnlyList<BoardTileId> Path { get; }
    public bool IgnoresInstalledItems { get; }
}
