using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;

/// <summary>
/// Resolves piece movement, stacking, and captures. Positive move counts move
/// forward; negative values are back-do moves.
/// </summary>
public sealed class PieceMovementManager : MonoBehaviour
{
    [SerializeField] private PlayerManager playerManager;

    /// <summary>
    /// 현재 보드 위에 있는 지정 말의 골인까지 남은 칸 수를 반환합니다.
    /// Escape 제한시간 동점 판정에서 사용합니다.
    /// PieceId는 플레이어별로 다시 0부터 시작하므로 playerId와 함께 전달해야 합니다.
    /// </summary>
    public bool TryGetRemainingStepsToGoal(int playerId, int pieceId, out int remainingSteps)
    {
        remainingSteps = -1;

        if (playerManager == null ||
            !playerManager.TryGetPlayer(playerId, out PlayerController player) ||
            !player.TryGetPieceData(pieceId, out PlayerRuntimeData.PieceRuntimeData piece) ||
            piece.State != PieceState.InBoard)
        {
            return false;
        }

        // Escape의 거리는 실제 이동 경로 길이가 아니라 기획에서 정의한 각 칸의 고정 점수입니다.
        // 호출자는 말 ID만 전달하고, 현재 타일 조회와 값 매핑은 여기서 처리합니다.
        switch (piece.CurrentTileId)
        {
            case BoardTileId.None:
            case BoardTileId.Start:
            case BoardTileId.Goal:
                remainingSteps = 0;
                return true;

            case BoardTileId.Outer01: remainingSteps = 10; return true;
            case BoardTileId.Outer02: remainingSteps = 9; return true;
            case BoardTileId.Outer03: remainingSteps = 8; return true;
            case BoardTileId.Outer04: remainingSteps = 7; return true;
            case BoardTileId.Corner01: remainingSteps = 6; return true;

            case BoardTileId.Outer05: remainingSteps = 10; return true;
            case BoardTileId.Outer06: remainingSteps = 9; return true;
            case BoardTileId.Outer07: remainingSteps = 8; return true;
            case BoardTileId.Outer08: remainingSteps = 7; return true;
            case BoardTileId.Corner02: remainingSteps = 6; return true;

            case BoardTileId.Outer09: remainingSteps = 9; return true;
            case BoardTileId.Outer10: remainingSteps = 8; return true;
            case BoardTileId.Outer11: remainingSteps = 7; return true;
            case BoardTileId.Outer12: remainingSteps = 6; return true;
            case BoardTileId.Corner03: remainingSteps = 5; return true;

            case BoardTileId.Outer13: remainingSteps = 4; return true;
            case BoardTileId.Outer14: remainingSteps = 3; return true;
            case BoardTileId.Outer15: remainingSteps = 2; return true;
            case BoardTileId.Outer16: remainingSteps = 1; return true;

            case BoardTileId.Center: remainingSteps = 3; return true;
            case BoardTileId.Inner01: remainingSteps = 5; return true;
            case BoardTileId.Inner02: remainingSteps = 4; return true;
            case BoardTileId.Inner03: remainingSteps = 7; return true;
            case BoardTileId.Inner04: remainingSteps = 6; return true;
            case BoardTileId.Inner05: remainingSteps = 5; return true;
            case BoardTileId.Inner06: remainingSteps = 4; return true;
            case BoardTileId.Inner07: remainingSteps = 2; return true;
            case BoardTileId.Inner08: remainingSteps = 1; return true;
        }

        return false;
    }

    //수정: 일반 이동과 캐릭터 스킬 이동을 구분할 수 있도록 선택 인자를 추가했습니다.
    public bool TryMovePiece(
        int playerId,
        int pieceId,
        int moveCount,
        bool isSkillMove = false)
    {
        if (playerManager == null)
        {
            Debug.LogError("PieceMovementManager requires a PlayerManager reference.", this);
            return false;
        }

        if (moveCount == 0)
        {
            //나중에 낙 처리(movecount를 0으로 주면 낙으로 처리 예정)
            Debug.LogWarning("Move count cannot be zero.", this);
            return false;
        }

        if (!playerManager.TryGetPlayer(playerId, out PlayerController player) ||
            !player.TryGetPieceData(pieceId, out PlayerRuntimeData.PieceRuntimeData selectedPiece))
        {
            Debug.LogError($"Could not find Player {playerId}, Piece {pieceId + 1}.", this);
            return false;
        }

        //수정: 실제 이동 전에 Player 스킬 시스템에서 최종 이동량을 결정합니다.
        bool isFirstBoardMove = selectedPiece.State == PieceState.Waiting;
        moveCount = CharacterSkillRegistry.ModifyMoveCount(
            new CharacterMoveRequest(
                playerId,
                pieceId,
                moveCount,
                isFirstBoardMove,
                isSkillMove));
        if (moveCount == 0)
        {
            Debug.LogWarning("A character skill changed the move count to zero.", this);
            return false;
        }

        List<PlayerRuntimeData.PieceRuntimeData> movingPieces = GetMovingPieces(player, selectedPiece);
        BoardTileId startingTile = selectedPiece.CurrentTileId;
        //수정: 경로 기반 패시브에 전달할 실제 이동 타일을 기록합니다.
        var path = new List<BoardTileId>();
        bool isBackward = moveCount < 0;
        int stepCount = Mathf.Abs(moveCount);

        for (int step = 0; step < stepCount; step++)
        {
            // A piece that has stopped on the shared start tile goals only when
            // it attempts to move past that tile.
            if (!isBackward && selectedPiece.State == PieceState.InBoard &&
                selectedPiece.CurrentTileId == BoardTileId.None)
            {
                SetGroupGoal(movingPieces);
                //수정: 완주로 보드를 벗어난 마지막 경로도 기록합니다.
                path.Add(BoardTileId.None);
                break;
            }

            BoardTileId nextTile = isBackward
                ? GetNextBackwardTile(selectedPiece)
                : GetNextForwardTile(selectedPiece, step == 0);

            foreach (PlayerRuntimeData.PieceRuntimeData movingPiece in movingPieces)
                movingPiece.MoveTo(nextTile);

            //수정: 한 칸씩 이동한 전체 경로를 Player 스킬 시스템에 전달하기 위해 저장합니다.
            path.Add(nextTile);
        }

        //수정: 대기 상태의 말이 처음 보드에 진입한 시점을 알립니다.
        if (isFirstBoardMove && selectedPiece.State == PieceState.InBoard)
            CharacterSkillRegistry.NotifyPieceEnteredBoard(playerId, pieceId);

        // Goal pieces have been removed from the board and cannot interact.
        if (selectedPiece.State == PieceState.Goal)
        {
            //수정: 완주한 이동도 이동 완료 이벤트에서 누락되지 않도록 전달합니다.
            NotifyMoveCompleted(playerId, movingPieces, startingTile, path);
            Debug.Log($"Player {playerId}, Piece {pieceId + 1} reached Goal.", this);
            return true;
        }

        //수정: 잡기 패시브 판단에 공격 말 ID와 업힌 실제 말 수를 함께 전달합니다.
        ResolveCaptures(
            playerId,
            selectedPiece.PieceId,
            movingPieces.Count,
            selectedPiece.CurrentTileId,
            moveCount);
        ResolveStacking(player, movingPieces, selectedPiece.CurrentTileId);
        //수정: 잡기와 업기 처리가 끝난 뒤 최종 이동 결과를 Player 시스템에 알립니다.
        NotifyMoveCompleted(playerId, movingPieces, startingTile, path);

        Debug.Log(
            $"Player {playerId}, Piece {pieceId + 1}: {startingTile} -> " +
            $"{selectedPiece.CurrentTileId} ({moveCount} spaces)",
            this);
        return true;
    }

    //수정: 이동한 모든 말에 출발지, 도착지, 전체 경로를 전달하는 공통 알림입니다.
    private static void NotifyMoveCompleted(
        int playerId,
        IReadOnlyList<PlayerRuntimeData.PieceRuntimeData> movingPieces,
        BoardTileId startingTile,
        IReadOnlyList<BoardTileId> path)
    {
        foreach (PlayerRuntimeData.PieceRuntimeData piece in movingPieces)
        {
            CharacterSkillRegistry.NotifyMoveCompleted(
                new CharacterMoveRecord(
                    playerId,
                    piece.PieceId,
                    startingTile,
                    piece.CurrentTileId,
                    path));
        }
    }

    private static List<PlayerRuntimeData.PieceRuntimeData> GetMovingPieces(
        PlayerController player,
        PlayerRuntimeData.PieceRuntimeData selectedPiece)
    {
        var movingPieces = new List<PlayerRuntimeData.PieceRuntimeData>();

        if (!selectedPiece.IsStacked)
        {
            movingPieces.Add(selectedPiece);
            return movingPieces;
        }

        foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
        {
            if (piece.StackGroupId == selectedPiece.StackGroupId)
                movingPieces.Add(piece);
        }

        return movingPieces;
    }

    private static void SetGroupGoal(List<PlayerRuntimeData.PieceRuntimeData> movingPieces)
    {
        foreach (PlayerRuntimeData.PieceRuntimeData piece in movingPieces)
            piece.SetGoal();
    }

    //수정: 캐릭터 잡기 패시브가 판단할 수 있도록 공격 말 정보를 추가로 받습니다.
    private void ResolveCaptures(
        int movingPlayerId,
        int movingPieceId,
        int movingPieceCount,
        BoardTileId landingTile,
        int moveCount)
    {
        foreach (PlayerController otherPlayer in playerManager.ActivePlayers)
        {
            if (otherPlayer.PlayerId == movingPlayerId)
                continue;

            //수정: 공격 말 수만큼만 퇴장시키는 스킬 결과를 계산합니다.
            int limitedRetiredCount = 0;
            foreach (PlayerRuntimeData.PieceRuntimeData targetPiece in otherPlayer.RuntimeData.Pieces)
            {
                if (targetPiece.State != PieceState.InBoard || targetPiece.CurrentTileId != landingTile)
                    continue;

                //수정: 실제 잡기 전에 Player 스킬 시스템에 잡기 판단을 요청합니다.
                bool wouldGrantExtraThrow = GrantsCaptureExtraThrow(moveCount);
                var request = new CharacterCaptureRequest(
                    movingPlayerId,
                    movingPieceId,
                    otherPlayer.PlayerId,
                    targetPiece.PieceId,
                    movingPieceCount,
                    wouldGrantExtraThrow);
                CharacterCaptureDecision decision =
                    CharacterSkillRegistry.EvaluateIncomingCapture(request);

                //수정: 방어, 분신 소모, 부품 전환 결과면 기본 잡기를 실행하지 않습니다.
                if (decision == CharacterCaptureDecision.Prevent ||
                    decision == CharacterCaptureDecision.ConsumeCloneWithoutBonus ||
                    decision == CharacterCaptureDecision.ConvertToParts)
                {
                    continue;
                }

                //수정: 캐릭터 판단에 따라 기본 잡기 또는 제한 퇴장을 적용합니다.
                CcDefine captureCc;
                if (decision == CharacterCaptureDecision.LimitRetireToAttackingCount)
                {
                    if (limitedRetiredCount >= movingPieceCount)
                        continue;

                    limitedRetiredCount++;
                    captureCc = CcDefine.Retire;
                }
                else
                {
                    captureCc = GetCaptureCc(targetPiece, moveCount);
                }

                targetPiece.SetCaptured(captureCc);
                //수정: 실제 잡기 이후 방어자 퇴장과 공격자 잡기 완료를 알립니다.
                CharacterSkillRegistry.NotifyPieceRetired(
                    otherPlayer.PlayerId,
                    targetPiece.PieceId);
                CharacterSkillRegistry.NotifyCaptureCompleted(request);
            }
        }
    }

    //수정: 잡기 추가 던지기 판정을 요청 정보와 실제 잡기 처리에서 함께 사용합니다.
    private static bool GrantsCaptureExtraThrow(int moveCount)
    {
        return moveCount == -1 || (moveCount >= 1 && moveCount <= 3);
    }

    private static CcDefine GetCaptureCc(PlayerRuntimeData.PieceRuntimeData targetPiece, int moveCount)
    {
        //수정: 중복된 추가 던지기 조건을 공통 함수로 통일했습니다.
        bool grantsExtraThrow = GrantsCaptureExtraThrow(moveCount);

        // A stacked group grants only one extra throw: its carrier is Kill,
        // and all carried pieces are Retire.
        if (grantsExtraThrow && (!targetPiece.IsStacked ||
                                 targetPiece.PieceId == targetPiece.StackLeaderPieceId))
        {
            return CcDefine.Kill;
        }

        return CcDefine.Retire;
    }

    private static void ResolveStacking(
        PlayerController player,
        List<PlayerRuntimeData.PieceRuntimeData> movingPieces,
        BoardTileId landingTile)
    {
        var piecesOnTile = new List<PlayerRuntimeData.PieceRuntimeData>();
        PlayerRuntimeData.PieceRuntimeData stationaryPiece = null;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in player.RuntimeData.Pieces)
        {
            if (piece.State != PieceState.InBoard || piece.CurrentTileId != landingTile)
                continue;

            piecesOnTile.Add(piece);

            if (stationaryPiece == null && !movingPieces.Contains(piece))
                stationaryPiece = piece;
        }

        // No friendly piece was already on the tile, so an existing stack
        // remains unchanged and a lone piece stays unstacked.
        if (stationaryPiece == null)
            return;

        int stackGroupId = stationaryPiece.IsStacked
            ? stationaryPiece.StackGroupId
            : player.RuntimeData.CreateStackGroupId();
        int stackLeaderPieceId = stationaryPiece.IsStacked
            ? stationaryPiece.StackLeaderPieceId
            : stationaryPiece.PieceId;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in piecesOnTile)
            piece.SetStackGroup(stackGroupId, stackLeaderPieceId);
    }

    private static BoardTileId GetNextForwardTile(
        PlayerRuntimeData.PieceRuntimeData piece,
        bool isStartingThisMove)
    {
        BoardTileId current = piece.CurrentTileId;

        if (current == BoardTileId.None)
            return BoardTileId.Outer01;

        if (isStartingThisMove && current == BoardTileId.Corner01)
            return BoardTileId.Inner01;
        if (isStartingThisMove && current == BoardTileId.Corner02)
            return BoardTileId.Inner05;
        if (isStartingThisMove && current == BoardTileId.Center)
            return BoardTileId.Inner07;

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
                return piece.PreviousTileId == BoardTileId.Inner02
                    ? BoardTileId.Inner03
                    : BoardTileId.Inner07;
            default:
                Debug.LogError($"Undefined forward tile: {current}");
                return BoardTileId.None;
        }
    }

    private static BoardTileId GetNextBackwardTile(PlayerRuntimeData.PieceRuntimeData piece)
    {
        switch (piece.CurrentTileId)
        {
            case BoardTileId.None:
                if (piece.PreviousTileId == BoardTileId.Outer01)
                    return BoardTileId.Outer16;
                if (piece.PreviousTileId == BoardTileId.Inner08)
                    return BoardTileId.Inner08;
                if (piece.PreviousTileId == BoardTileId.Outer16)
                    return BoardTileId.Outer16;
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
                return piece.PreviousTileId == BoardTileId.Inner04
                    ? BoardTileId.Inner04
                    : BoardTileId.Outer12;
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
                return piece.PreviousTileId == BoardTileId.Inner03 ||
                       piece.PreviousTileId == BoardTileId.Inner02
                    ? BoardTileId.Inner02
                    : BoardTileId.Inner06;
            default:
                Debug.LogError($"Undefined back-do tile: {piece.CurrentTileId}");
                return BoardTileId.None;
        }
    }
}
