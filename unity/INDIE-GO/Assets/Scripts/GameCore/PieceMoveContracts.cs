using System;
using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

namespace YutArena.GameCore
{
    public readonly struct PieceMoveCommand
    {
        public PieceMoveCommand(int playerId, int pieceId, int moveCount, bool isActiveSkillMove = false)
        {
            PlayerId = playerId;
            PieceId = pieceId;
            MoveCount = moveCount;
            IsActiveSkillMove = isActiveSkillMove;
        }

        public int PlayerId { get; }
        public int PieceId { get; }
        public int MoveCount { get; }
        public bool IsActiveSkillMove { get; }
    }

    public readonly struct PieceCaptureResult
    {
        public PieceCaptureResult(
            int targetPlayerId,
            int targetPieceId,
            CharacterCaptureDecision decision,
            CcDefine appliedCc,
            bool grantsExtraThrow)
        {
            TargetPlayerId = targetPlayerId;
            TargetPieceId = targetPieceId;
            Decision = decision;
            AppliedCc = appliedCc;
            GrantsExtraThrow = grantsExtraThrow;
        }

        public int TargetPlayerId { get; }
        public int TargetPieceId { get; }
        public CharacterCaptureDecision Decision { get; }
        public CcDefine AppliedCc { get; }
        public bool GrantsExtraThrow { get; }
        public bool WasRemoved => AppliedCc == CcDefine.Kill || AppliedCc == CcDefine.Retire;
    }

    public sealed class PieceMoveResult
    {
        private PieceMoveResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Error { get; }
        public int PlayerId { get; internal set; }
        public int PieceId { get; internal set; }
        public int RequestedMoveCount { get; internal set; }
        public int AppliedMoveCount { get; internal set; }
        public BoardTileId From { get; internal set; }
        public BoardTileId To { get; internal set; }
        public bool EnteredBoard { get; internal set; }
        public int FinishedPieceCount { get; internal set; }
        public IReadOnlyList<BoardTileId> Path => path;
        public IReadOnlyList<PieceCaptureResult> Captures => captures;
        public bool GrantsCaptureExtraThrow => captures.Exists(c => c.GrantsExtraThrow);

        private readonly List<BoardTileId> path = new List<BoardTileId>();
        private readonly List<PieceCaptureResult> captures = new List<PieceCaptureResult>();

        internal void AddPath(BoardTileId tile) => path.Add(tile);
        internal void AddCapture(PieceCaptureResult result) => captures.Add(result);

        public static PieceMoveResult Success(PieceMoveCommand command)
        {
            return new PieceMoveResult(true, string.Empty)
            {
                PlayerId = command.PlayerId,
                PieceId = command.PieceId,
                RequestedMoveCount = command.MoveCount,
                AppliedMoveCount = command.MoveCount
            };
        }

        public static PieceMoveResult Failure(PieceMoveCommand command, string error)
        {
            if (string.IsNullOrWhiteSpace(error)) throw new ArgumentException("An error is required.", nameof(error));
            return new PieceMoveResult(false, error)
            {
                PlayerId = command.PlayerId,
                PieceId = command.PieceId,
                RequestedMoveCount = command.MoveCount,
                AppliedMoveCount = command.MoveCount
            };
        }
    }
}
