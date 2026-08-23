using YutArena.InGame;
using System.Collections.Generic;
using UnityEngine;

public sealed class CHAR_003_Status : CharacterStatusBehaviour
{
    private int cloneCount;
    private int cloneStackGroupId = -1;
    private readonly List<GameObject> cloneVisuals = new List<GameObject>();

    public override CharacterCaptureDecision EvaluateIncomingCapture(CharacterCaptureRequest request)
    {
        if (cloneCount > 0)
        {
            cloneCount--;
            RemoveLastCloneVisual();
            if (TryGetPiece(out PlayerRuntimeData.PieceRuntimeData caster))
                ClearVirtualCloneStackIfEmpty(caster);

            Debug.Log(
                $"[CharacterSkill][ActiveEffect] {nameof(CHAR_003_Status)} clone was captured. " +
                $"Player={PlayerId}, Piece={PieceId}, Clones={cloneCount}",
                this);
            return CharacterCaptureDecision.ConsumeCloneWithoutBonus;
        }

        // 업힌 실제 말이 공격측 말 수보다 많으면 공격측 말 수만큼만 Retire해야 합니다.
        // 현재 PieceMovementManager는 스택 전체를 한 번에 처리하므로 호출측에서
        // request.AttackingPieceCount만큼 대상을 제한해 적용해야 합니다.
        if (request.AttackingPieceCount > 0 && TryStartPassiveCooldown())
        {
            UnityEngine.Debug.Log(
                $"[CharacterSkill][Passive] {nameof(CHAR_003_Status)} limited retired pieces " +
                $"to {request.AttackingPieceCount}. Player={PlayerId}, Piece={PieceId}",
                this);
            return CharacterCaptureDecision.LimitRetireToAttackingCount;
        }

        return CharacterCaptureDecision.Proceed;
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Clone Technique requires a piece on the board.");

        EnsureCloneStack(caster);
        cloneCount++;
        CreateCloneVisual(cloneCount);
        UnityEngine.Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_003_Status)} activated. " +
            $"Player={PlayerId}, Piece={PieceId}, Clones={cloneCount}",
            this);
        return CharacterActiveResult.Success("A non-scoring clone was stacked on the caster.");
    }

    public override void OnPieceRetired()
    {
        ClearAllClones();
        ResetPassiveCooldown();
    }

    protected override void OnDisable()
    {
        ClearAllClones();
        base.OnDisable();
    }

    private void EnsureCloneStack(PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.IsStacked)
        {
            cloneStackGroupId = caster.StackGroupId;
            return;
        }

        cloneStackGroupId = Owner.RuntimeData.CreateStackGroupId();
        caster.SetStackGroup(cloneStackGroupId, caster.PieceId);
    }

    private void ClearVirtualCloneStackIfEmpty(PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (cloneCount > 0 || cloneStackGroupId < 0 ||
            caster.StackGroupId != cloneStackGroupId)
            return;

        foreach (PlayerRuntimeData.PieceRuntimeData piece in Owner.RuntimeData.Pieces)
        {
            if (piece.PieceId != caster.PieceId &&
                piece.StackGroupId == cloneStackGroupId)
            {
                cloneStackGroupId = -1;
                return;
            }
        }

        caster.ClearStack();
        cloneStackGroupId = -1;
    }

    private void CreateCloneVisual(int visualIndex)
    {
        SpriteRenderer source = GetComponentInChildren<SpriteRenderer>(true);
        if (source == null) return;

        var cloneObject = new GameObject($"SkillClone_{visualIndex}");
        cloneObject.transform.SetParent(transform, false);
        cloneObject.transform.localPosition = new Vector3(
            0.24f + ((visualIndex - 1) % 2) * 0.16f,
            0.2f + ((visualIndex - 1) / 2) * 0.14f,
            0f);
        cloneObject.transform.localScale = Vector3.one * 0.65f;

        SpriteRenderer cloneRenderer = cloneObject.AddComponent<SpriteRenderer>();
        cloneRenderer.sprite = source.sprite;
        cloneRenderer.color = new Color(
            source.color.r,
            source.color.g,
            source.color.b,
            Mathf.Min(source.color.a, 0.62f));
        cloneRenderer.sortingLayerID = source.sortingLayerID;
        cloneRenderer.sortingOrder = source.sortingOrder + visualIndex;
        cloneRenderer.flipX = source.flipX;
        cloneRenderer.flipY = source.flipY;
        cloneVisuals.Add(cloneObject);
    }

    private void RemoveLastCloneVisual()
    {
        if (cloneVisuals.Count == 0) return;

        int lastIndex = cloneVisuals.Count - 1;
        GameObject cloneObject = cloneVisuals[lastIndex];
        cloneVisuals.RemoveAt(lastIndex);
        if (cloneObject != null)
            Destroy(cloneObject);
    }

    private void ClearAllClones()
    {
        cloneCount = 0;
        while (cloneVisuals.Count > 0)
            RemoveLastCloneVisual();

        if (TryGetPiece(out PlayerRuntimeData.PieceRuntimeData caster))
            ClearVirtualCloneStackIfEmpty(caster);
        else
            cloneStackGroupId = -1;
    }
}
