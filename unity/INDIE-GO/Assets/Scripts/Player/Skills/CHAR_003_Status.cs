using YutArena.InGame;

public sealed class CHAR_003_Status : CharacterStatusBehaviour
{
    private int cloneCount;

    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (cloneCount > 0)
        {
            cloneCount--;
            return CharacterCaptureDecision.ConsumeCloneWithoutBonus;
        }

        // 업힌 실제 말이 공격측 말 수보다 많으면 공격측 말 수만큼만 Retire해야 합니다.
        // 현재 PieceMovementManager는 스택 전체를 한 번에 처리하므로 호출측에서
        // request.AttackingPieceCount만큼 대상을 제한해 적용해야 합니다.
        return request.AttackingPieceCount > 0
            ? CharacterCaptureDecision.LimitRetireToAttackingCount
            : CharacterCaptureDecision.Proceed;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        cloneCount++;
        return CharacterActiveResult.Success("A non-scoring clone was stacked on the caster.");
    }
}
