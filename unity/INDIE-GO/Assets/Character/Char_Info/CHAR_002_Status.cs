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
        if (!talismanAvailable ||
            !TryGetPiece(out PlayerRuntimeData.PieceRuntimeData source))
            return;

        CharacterPieceReference? nearest = CharacterBoardUtility.FindNearestAlly(
            Players,
            PlayerId,
            PieceId,
            source.CurrentTileId,
            Turns != null ? Turns.Settings : null);
        if (!nearest.HasValue || !TryStartPassiveCooldown())
            return;

        PlayerRuntimeData.PieceRuntimeData target = nearest.Value.Piece;
        foreach (PlayerRuntimeData.PieceRuntimeData ally in nearest.Value.Player.RuntimeData.Pieces)
        {
            if (ally.State != PieceState.InBoard ||
                (ally.PieceId != target.PieceId &&
                 (!target.IsStacked || ally.StackGroupId != target.StackGroupId)))
                continue;

            // 한 번 맞을 때가 아니라 대상의 다음 턴이 시작될 때까지 보호합니다.
            CcEffectService.Apply(ally, CcDefine.TalismanProtection, turns: 1,
                sourcePlayerId: PlayerId, sourcePieceId: PieceId);
        }
        talismanAvailable = false;
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_002_Status)} granted protection to " +
            $"Player={nearest.Value.Player.PlayerId}, Piece={nearest.Value.Piece.PieceId}. Owner={PlayerId}, Piece={PieceId}",
            this);
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
        return moveCount * 2;
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

        ObserveTurnManager(Turns);
        DoubleMoves[PlayerId] = new DoubleMoveReservation(Turns.PendingResults);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_002_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success("The next throw result will move twice as far.");
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
