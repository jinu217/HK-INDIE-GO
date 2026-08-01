using System;
using System.Collections.Generic;
using YutArena.InGame;

/// <summary>
/// 게임 한 판 동안만 존재하는 플레이어의 가변 데이터다.
/// ScriptableObject에 저장하지 않으므로, 플레이가 끝난 값이 원본 설정에 남지 않는다.
/// </summary>
[Serializable]
public sealed class PlayerRuntimeData
{
    [Serializable]
    public sealed class PieceRuntimeData
    {
        public int PieceId { get; private set; }
        public int BoardPosition { get; private set; }
        public bool IsFinished { get; private set; }

        // -1은 다른 말과 업혀 있지 않은 단독 상태를 뜻한다.
        public int StackGroupId { get; private set; }

        /// <summary>현재 이 말에 적용된 CC 또는 특수 처리 상태.</summary>
        public CcDefine CurrentCc { get; private set; }

        /// <summary>
        /// Stun, Silence 등 시간 제한 CC의 남은 턴 수.
        /// 감소 및 만료 처리는 TurnManager가 담당한다.
        /// </summary>
        public int RemainingCcTurns { get; private set; }

        internal PieceRuntimeData(int pieceId)
        {
            PieceId = pieceId;
            Reset();
        }

        public void SetBoardPosition(int boardPosition)
        {
            BoardPosition = boardPosition;
        }

        public void SetFinished(bool isFinished)
        {
            IsFinished = isFinished;
        }

        public void SetStackGroupId(int stackGroupId)
        {
            StackGroupId = stackGroupId;
        }

        public void SetCc(CcDefine ccType, int remainingTurns = 0)
        {
            if (remainingTurns < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingTurns), "CC 남은 턴은 0 이상이어야 합니다.");

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
            BoardPosition = -1; // 출발 전 대기 상태
            IsFinished = false;
            StackGroupId = -1;
            ClearCc();
        }
    }

    public int PlayerId { get; private set; }
    public string PlayerName { get; private set; }
    public IReadOnlyList<PieceRuntimeData> Pieces => pieces;

    private readonly List<PieceRuntimeData> pieces = new List<PieceRuntimeData>();

    public PlayerRuntimeData(int playerId, string playerName, int pieceCount)
    {
        if (playerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(playerId), "플레이어 ID는 1 이상이어야 합니다.");
        if (pieceCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(pieceCount), "말 수는 1 이상이어야 합니다.");

        PlayerId = playerId;
        PlayerName = string.IsNullOrWhiteSpace(playerName) ? $"Player {playerId}" : playerName;

        for (int pieceId = 0; pieceId < pieceCount; pieceId++)
            pieces.Add(new PieceRuntimeData(pieceId));
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
        foreach (PieceRuntimeData piece in pieces)
            piece.Reset();
    }
}
