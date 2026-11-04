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
        private readonly PlayerRuntimeData owner;
        private StackState stack;
        public int OwnerPlayerId => owner.PlayerId;
        public int PieceId { get; private set; }
        public BoardTileId CurrentTileId { get; private set; }
        public BoardTileId PreviousTileId { get; private set; }
        public PieceState State { get; set; }
        public bool IsFinished => State == PieceState.Goal;

        // -1 means this piece is not stacked with another piece.
        public int StackGroupId => stack != null ? stack.Id : -1;
        public int StackLeaderPieceId => stack != null ? stack.Leader.PieceId : -1;
        public int StackLeaderPlayerId => stack != null ? stack.Leader.OwnerPlayerId : -1;
        public bool IsStacked => stack != null;
        public bool IsStackLeader => stack == null || ReferenceEquals(stack.Leader, this);
        internal PieceRuntimeData Leader => stack != null && stack.Leader.State == PieceState.InBoard &&
            ReferenceEquals(stack.Leader.stack, stack) ? stack.Leader : this;

        public bool IsInSameStack(PieceRuntimeData other) =>
            stack != null && other != null && ReferenceEquals(stack, other.stack);

        internal List<PieceRuntimeData> GetStackMembers()
        {
            if (stack == null) return new List<PieceRuntimeData> { this };
            var members = new List<PieceRuntimeData>();
            foreach (var member in stack.Members)
                if (member == this || member.State == PieceState.InBoard) members.Add(member);
            return members;
        }

        //수정: 모든 효과는 이 말의 CC 목록에 저장하며 Character의 효과 처리기만 변경합니다.
        [UnityEngine.SerializeField] private PieceCcCollection cc = new PieceCcCollection();
        public PieceCcCollection Cc => cc;
        public CcDefine CurrentCc => cc.Primary;
        public int RemainingCcTurns => cc.Get(CurrentCc)?.RemainingOwnerTurns ?? 0;

        internal PieceRuntimeData(int pieceId, PlayerRuntimeData owner)
        {
            this.owner = owner;
            PieceId = pieceId;
            cc.Bind(owner, this);
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
            ClearCc();
        }

        /// <summary>
        /// Removes a captured piece from the board and records its capture CC.
        /// TurnManager later consumes Kill/Retire and decides extra throws.
        /// </summary>
        public void SetCaptured(CcDefine captureCc)
        {
            if (captureCc != CcDefine.Kill && captureCc != CcDefine.Retire)
                throw new ArgumentException("Capture CC must be Kill or Retire.", nameof(captureCc));

            CcEffectService.Apply(this, captureCc);
        }

        internal void ApplyCapturedPosition()
        {
            State = PieceState.Waiting;
            CurrentTileId = BoardTileId.None;
            PreviousTileId = BoardTileId.None;
            ClearStack();
        }

        public void SetStackGroup(int stackGroupId, int stackLeaderPieceId)
        {
            if (!owner.TryGetPiece(stackLeaderPieceId, out var leader))
                throw new ArgumentOutOfRangeException(nameof(stackLeaderPieceId));
            SetStackGroup(stackGroupId, leader);
        }

        /// <summary>말 번호가 같아도 소유자가 다른 팀원 말을 정확히 구분합니다.</summary>
        public void SetStackGroup(int stackGroupId, PieceRuntimeData leader)
        {
            if (stackGroupId < 0)
                throw new ArgumentOutOfRangeException(nameof(stackGroupId));
            if (leader == null) throw new ArgumentNullException(nameof(leader));
            var key = (stackGroupId, leader.PieceId);
            if (!leader.owner.stacks.TryGetValue(key, out var group))
            {
                group = new StackState(stackGroupId, leader);
                leader.owner.stacks.Add(key, group);
            }
            if (ReferenceEquals(stack, group)) return;
            ClearStack();
            stack = group;
            stack.Members.Add(this);
        }

        public void ClearStack()
        {
            if (stack == null) return;
            var previous = stack;
            stack = null;
            previous.Members.Remove(this);
            if (previous.Members.Count == 0)
                previous.Leader.owner.stacks.Remove((previous.Id, previous.Leader.PieceId));
        }

        public void SetCc(CcDefine ccType, int remainingTurns = 0)
        {
            CcEffectService.Apply(this, ccType, remainingTurns);
        }

        public void ClearCc()
        {
            CcEffectService.Clear(this);
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
    private readonly Dictionary<(int group, int leader), StackState> stacks =
        new Dictionary<(int, int), StackState>();
    private sealed class StackState
    {
        public readonly int Id;
        public readonly PieceRuntimeData Leader;
        public readonly List<PieceRuntimeData> Members = new List<PieceRuntimeData>();
        public StackState(int id, PieceRuntimeData leader) { Id = id; Leader = leader; }
    }
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
            pieces.Add(new PieceRuntimeData(pieceId, this));
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
