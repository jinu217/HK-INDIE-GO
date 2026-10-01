using System.Collections.Generic;
using UnityEngine;
using YutArena.InGame;

public sealed class CHAR_018_Status : CharacterStatusBehaviour
{
    public override bool RequiresTargetPieceSelection => true;


    public override void OnPieceEnteredBoard()
    {
        List<CharacterPieceReference> enemies = CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId);
        enemies.RemoveAll(reference =>
            !CharacterSkillRegistry.IsTargetable(
                reference.Player.PlayerId,
                reference.Piece.PieceId));

        if (enemies.Count == 0)
        {
            ClearMark();
            return;
        }
        if (!TryStartPassiveCooldown()) return;

        CharacterPieceReference selected = enemies[Random.Range(0, enemies.Count)];
        ClearMark();
        CcEffectService.Apply(selected.Piece, CcDefine.Mark,
            sourcePlayerId: PlayerId, sourcePieceId: PieceId);
        Debug.Log(
            $"[CharacterSkill][Passive] {nameof(CHAR_018_Status)} marked " +
            $"Player={selected.Player.PlayerId}, Piece={selected.Piece.PieceId}. " +
            $"Owner={PlayerId}, Piece={PieceId}",
            this);
    }

    public override void OnPieceRetired()
    {
        ClearMark();
        ResetPassiveCooldown();
    }

    protected override CharacterActiveResult ExecuteActive(
        CharacterActiveRequest request,
        PlayerRuntimeData.PieceRuntimeData caster)
    {
        if (caster.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Assassination requires a piece on the board.");

        CharacterPieceReference target;
        if (request.HasTarget)
        {
            if (!TryGetPiece(request.TargetPlayerId, request.TargetPieceId, out target))
                return CharacterActiveResult.Failure("The selected target does not exist.");
        }
        else if (!TryFindAutomaticTarget(out target))
        {
            return CharacterActiveResult.Failure("There is no target on the board.");
        }

        if (target.Player.PlayerId == PlayerId || target.Piece.State != PieceState.InBoard)
            return CharacterActiveResult.Failure("Assassination can target only an enemy on the board.");
        if (!CharacterSkillRegistry.IsTargetable(target.Player.PlayerId, target.Piece.PieceId))
            return CharacterActiveResult.Failure("The selected enemy cannot currently be targeted.");

        CharacterBoardUtility.MoveStackAlongPath(
            Owner,
            caster,
            new[] { target.Piece.CurrentTileId });
        bool captured = CharacterBoardUtility.TryCapture(
            PlayerId,
            PieceId,
            target,
            GetStackPieceCount(caster),
            true,
            out CharacterCaptureDecision decision);

        Debug.Log(
            $"[CharacterSkill][Active] {nameof(CHAR_018_Status)} activated against " +
            $"Player={target.Player.PlayerId}, Piece={target.Piece.PieceId}. " +
            $"Owner={PlayerId}, Piece={PieceId}",
            this);
        return CharacterActiveResult.Success(
            captured
                ? "Moved to and captured the selected enemy."
                : $"Moved to the selected enemy, but capture resolved with {decision}.");
    }

    private bool TryFindAutomaticTarget(out CharacterPieceReference target)
    {
        foreach (var enemy in CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
        {
            if (!CharacterSkillRegistry.IsTargetable(enemy.Player.PlayerId, enemy.Piece.PieceId)) continue;
            foreach (var effect in enemy.Piece.Cc.Effects)
                if (effect.Type == CcDefine.Mark && effect.SourcePlayerId == PlayerId &&
                    effect.SourcePieceId == PieceId) { target = enemy; return true; }
        }

        foreach (CharacterPieceReference enemy in
                 CharacterBoardUtility.GetEnemiesOnBoard(Players, PlayerId))
        {
            if (!CharacterSkillRegistry.IsTargetable(
                    enemy.Player.PlayerId,
                    enemy.Piece.PieceId))
                continue;

            target = enemy;
            return true;
        }

        target = default;
        return false;
    }

    private void ClearMark()
    {
        CcEffectService.ClearMarksFrom(PlayerId, PieceId);
    }
}
