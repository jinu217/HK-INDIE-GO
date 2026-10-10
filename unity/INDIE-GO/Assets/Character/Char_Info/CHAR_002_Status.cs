using System.Collections.Generic;
using YutArena.Common;
using YutArena.InGame;
using YutArena.Managers;
using YutArena.Managers.GameProgress;

public sealed class CHAR_002_Status : CharacterStatusBehaviour
{
    private sealed class DoubleMoveReservation
    {
        public readonly HashSet<YutThrowData> KnownResults;
        public YutThrowData TargetResult;
        public bool AwaitingThrow = true;
        public bool ApplyOnNextMove;

        public DoubleMoveReservation(IReadOnlyList<YutThrowData> pendingResults)
        {
            KnownResults = new HashSet<YutThrowData>(pendingResults);
        }
    }

    private static readonly Dictionary<int, DoubleMoveReservation> DoubleMoves =
        new Dictionary<int, DoubleMoveReservation>();
    private static TestTurnManager observedTurns;
    private bool talismanAvailable = true;

    // 축지법은 시전자 말이 아닌 다음에 던진 윷 결과 한 개에 적용됩니다.
    public override bool RequiresCasterPieceSelection => false;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        if (observedTurns != null)
            observedTurns.OnPendingResultsChanged -= ObservePendingResults;
        observedTurns = null;
        DoubleMoves.Clear();
    }

    public override bool CanSelectAsActiveCaster(
        PlayerRuntimeData.PieceRuntimeData piece)
    {
        return CcEffectService.CanUseSkill(piece);
    }

    public override void OnPieceEnteredBoard()
    {
        if (!talismanAvailable || !IsPassiveReady ||
            !TryGetPiece(out PlayerRuntimeData.PieceRuntimeData source) ||
            source.State != PieceState.InBoard || source.CurrentTileId == BoardTileId.None)
            return;

        CharacterPieceReference? nearest = CharacterBoardUtility.FindNearestAlly(
            Players,
            PlayerId,
            PieceId,
            source.CurrentTileId,
            Turns != null ? Turns.Settings : null);
        if (!nearest.HasValue)
            return;

        PlayerRuntimeData.PieceRuntimeData target = nearest.Value.Piece;
        //수정: 선택한 아군 한 말에만 부여하며 자신이나 업힌 말로 확산하지 않습니다.
        // 한 번 맞을 때가 아니라 대상의 다음 턴이 시작될 때까지 보호합니다.
        if (!ApplyPassiveEffect(target: target))
            return;

        TryStartPassiveCooldown();
        talismanAvailable = false;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_002_Status)} granted protection to " +
            $"Player={nearest.Value.Player.PlayerId}, Piece={nearest.Value.Piece.PieceId}. Owner={PlayerId}, Piece={PieceId}",
            this);
    }

    protected override bool CanReceivePassiveEffect(PlayerRuntimeData.PieceRuntimeData target, int index)
    {
        var targetOwner = CcEffectService.FindOwner(target);
        return targetOwner != null && CharacterBoardUtility.AreAllies(PlayerId, targetOwner.PlayerId,
            Turns != null ? Turns.Settings : null) &&
            !(targetOwner.PlayerId == PlayerId && target.PieceId == PieceId) &&
            target.State == PieceState.InBoard && target.CurrentTileId != BoardTileId.None &&
            !target.Cc.Has(CcDefine.Robot_part) &&
            (!target.IsStacked || target.StackLeaderPieceId == target.PieceId);
    }

    public override void OnPieceRetired()
    {
        talismanAvailable = true;
        ResetPassiveCooldown();
    }

    public override int ModifyMoveCount(CharacterMoveRequest request)
    {
        int moveCount = base.ModifyMoveCount(request);
        if (observedTurns != Turns)
        {
            DoubleMoves.Remove(request.PlayerId);
            return moveCount;
        }
        if (request.IsActiveSkillMove ||
            !DoubleMoves.TryGetValue(request.PlayerId, out DoubleMoveReservation reservation) ||
            !reservation.ApplyOnNextMove)
            return moveCount;

        DoubleMoves.Remove(request.PlayerId);
        if (moveCount == 0 || !ApplyActiveEffect()) return moveCount;
        return base.ModifyMoveCount(request);
    }

    public override void OnOwnerTurnEnded()
    {
        DoubleMoves.Remove(PlayerId);
    }

    protected override bool CanUseActiveDuringPhase(TurnPhase phase)
    {
        return phase == TurnPhase.WaitThrow;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (Turns == null)
            return CharacterActiveResult.Failure("Turn manager is not available.");
        if (ActiveEffect() == null || !ActiveEffect().IsValid)
            return CharacterActiveResult.Failure("Next-throw movement effect settings are missing.");

        ObserveTurnManager(Turns);
        DoubleMoves[PlayerId] = new DoubleMoveReservation(Turns.PendingResults);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_002_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success($"The next throw result's move count will be multiplied by {ActiveEffect().amount}.");
    }

    private static void ObserveTurnManager(TestTurnManager turns)
    {
        if (observedTurns == turns) return;
        if (observedTurns != null)
            observedTurns.OnPendingResultsChanged -= ObservePendingResults;
        DoubleMoves.Clear();
        observedTurns = turns;
        observedTurns.OnPendingResultsChanged += ObservePendingResults;
    }

    private static void ObservePendingResults(List<YutThrowData> results)
    {
        foreach (KeyValuePair<int, DoubleMoveReservation> entry in DoubleMoves)
        {
            DoubleMoveReservation reservation = entry.Value;
            if (reservation.AwaitingThrow)
            {
                foreach (YutThrowData result in results)
                {
                    if ((int)result.player != entry.Key ||
                        reservation.KnownResults.Contains(result)) continue;
                    reservation.TargetResult = result;
                    reservation.AwaitingThrow = false;
                    break;
                }
            }
            else if (reservation.TargetResult != null &&
                     !results.Contains(reservation.TargetResult))
            {
                // 턴 매니저가 결과를 제거한 직후, 해당 이동을 요청합니다.
                reservation.TargetResult = null;
                reservation.ApplyOnNextMove = true;
            }

            reservation.KnownResults.Clear();
            foreach (YutThrowData result in results)
                reservation.KnownResults.Add(result);
        }
    }
}
