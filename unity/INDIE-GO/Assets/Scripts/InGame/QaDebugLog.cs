using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using YutArena.InGame;

// QA events only. Calls and interpolated arguments are omitted in release builds.
public static class QaDebugLog
{
    internal static Action<string> Receiver;
    public static bool Enabled => Receiver != null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Receiver = null;

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Event(string message, UnityEngine.Object context = null)
    {
        if (Enabled) Receiver.Invoke($"[이벤트][{Time.unscaledTime:F2}s] {message}");
    }
    public static string Piece(int playerId, int pieceId)
    {
        if (playerId <= 0) return "시스템";
        if (pieceId < 0) return $"P{playerId}(플레이어 전체)";
        string name = CharacterSkillRegistry.TryGet(playerId, pieceId, out var skill) && skill.Data != null
            ? skill.Data.char_Name + "/" + skill.GetType().Name.Replace("_Status", "") : "캐릭터 미등록";
        return $"P{playerId}/말{pieceId + 1}(id={pieceId}, {name})";
    }
    public static string Stack(int playerId, PlayerRuntimeData.PieceRuntimeData piece)
    {
        if (!Enabled || piece == null) return "-";
        var text = new StringBuilder(Piece(playerId, piece.PieceId));
        var owner = CcEffectService.FindOwner(piece);
        if (owner != null && piece.IsStacked)
            foreach (var member in owner.RuntimeData.Pieces)
                if (member != piece && member.State == PieceState.InBoard && member.StackGroupId == piece.StackGroupId)
                    text.Append(" + ").Append(Piece(playerId, member.PieceId));
        return text.ToString();
    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Skill(string kind, int sourcePlayer, int sourcePiece, int targetPlayer,
        int targetPiece, string detail)
    {
        if (!Enabled) return;
        string skillName = "-";
        if (CharacterSkillRegistry.TryGet(sourcePlayer, sourcePiece, out var skill) && skill.Data != null)
            skillName = kind.StartsWith("패시브", StringComparison.Ordinal) ? skill.Data.passive_Name : skill.Data.active_Name;
        Event($"[{kind}] {Piece(sourcePlayer, sourcePiece)} -> {Piece(targetPlayer, targetPiece)} | 스킬={skillName}, {detail}");
    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Capture(CharacterCaptureRequest request, CharacterCaptureDecision decision, string targets, bool captured)
    {
        if (Enabled) Event($"[잡기] {Piece(request.AttackerPlayerId, request.AttackerPieceId)} -> {targets} | " +
            $"결과={(captured ? "잡힘" : "잡히지 않음")}, 방어판정={decision}, 공격 말 수={request.AttackingPieceCount}");
    }
}
