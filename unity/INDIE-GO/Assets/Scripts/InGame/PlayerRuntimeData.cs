using System;
using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;

public enum PieceState
{
    Waiting,
    InBoard,
    Goal
}

[Serializable]
public sealed class PlayerRuntimeData
{
    [Serializable]
    public sealed class PieceRuntimeData
    {
        public int PieceId { get; private set; }
        public BoardTileId CurrentTileId { get; private set; }
        public BoardTileId PreviousTileId { get; private set; }
        public PieceState State { get; set; }
        public bool IsFinished => State == PieceState.Goal;

        // -1 means this piece is not stacked with another piece.
        public int StackGroupId { get; private set; }
        public int StackLeaderPieceId { get; private set; }
        public bool IsStacked => StackGroupId >= 0;

        public CcDefine CurrentCc { get; private set; }
        public int RemainingCcTurns { get; private set; }

        internal PieceRuntimeData(int pieceId)
        {
            PieceId = pieceId;
            Reset();
        }

        /// <summary>
        /// Moves this piece to a tile. Goal pieces are allowed to re-enter the
        /// board for future game modes; mode rules decide whether that is legal.
        /// </summary>
        public void MoveTo(BoardTileId nextTileId)
        {
            PreviousTileId = CurrentTileId;
            CurrentTileId = nextTileId;

            if (nextTileId != BoardTileId.None)
                State = PieceState.InBoard;
        }

        public void SetGoal()
        {
            State = PieceState.Goal;
            CurrentTileId = BoardTileId.None;
            PreviousTileId = BoardTileId.None;
            ClearStack();
        }

        /// <summary>
        /// Removes a captured piece from the board and records its capture CC.
        /// TurnManager later consumes Kill/Retire and decides extra throws.
        /// </summary>
        public void SetCaptured(CcDefine captureCc)
        {
            if (captureCc != CcDefine.Kill && captureCc != CcDefine.Retire)
                throw new ArgumentException("Capture CC must be Kill or Retire.", nameof(captureCc));

            State = PieceState.Waiting;
            CurrentTileId = BoardTileId.None;
            PreviousTileId = BoardTileId.None;
            ClearStack();
            SetCc(captureCc);
        }

        public void SetStackGroup(int stackGroupId, int stackLeaderPieceId)
        {
            if (stackGroupId < 0)
                throw new ArgumentOutOfRangeException(nameof(stackGroupId));
            if (stackLeaderPieceId < 0)
                throw new ArgumentOutOfRangeException(nameof(stackLeaderPieceId));

            StackGroupId = stackGroupId;
            StackLeaderPieceId = stackLeaderPieceId;
        }

        public void ClearStack()
        {
            StackGroupId = -1;
            StackLeaderPieceId = -1;
        }

        public void SetCc(CcDefine ccType, int remainingTurns = 0)
        {
            if (remainingTurns < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingTurns));

            CurrentCc = ccType;
            RemainingCcTurns = ccType == CcDefine.None ? 0 : remainingTurns;
        }

        public void ClearCc()
        {
            CurrentCc = CcDefine.None;
            RemainingCcTurns = 0;
        }

        public void Reset()
        {
            CurrentTileId = BoardTileId.None;
            PreviousTileId = BoardTileId.None;
            State = PieceState.Waiting;
            ClearStack();
            ClearCc();
        }
    }

    public int PlayerId { get; private set; }
    public string PlayerName { get; private set; }
    public IReadOnlyList<PieceRuntimeData> Pieces => pieces;

    private readonly List<PieceRuntimeData> pieces = new List<PieceRuntimeData>();
    private int nextStackGroupId;

    public PlayerRuntimeData(int playerId, string playerName, int pieceCount)
    {
        if (playerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(playerId));
        if (pieceCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(pieceCount));

        PlayerId = playerId;
        PlayerName = string.IsNullOrWhiteSpace(playerName) ? $"Player {playerId}" : playerName;

        for (int pieceId = 0; pieceId < pieceCount; pieceId++)
            pieces.Add(new PieceRuntimeData(pieceId));
    }

    public int CreateStackGroupId()
    {
        return nextStackGroupId++;
    }

    public bool TryGetPiece(int pieceId, out PieceRuntimeData piece)
    {
        if (pieceId >= 0 && pieceId < pieces.Count)
        {
            piece = pieces[pieceId];
            return true;
        }

        piece = null;
        return false;
    }

    public void ResetPieces()
    {
        nextStackGroupId = 0;

        foreach (PieceRuntimeData piece in pieces)
            piece.Reset();
    }
}
