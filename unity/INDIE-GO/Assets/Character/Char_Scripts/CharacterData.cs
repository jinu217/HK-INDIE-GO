using System;
using UnityEngine;
using YutArena.InGame;

// 테이블 효과 ID와 수치를 분리합니다. 발동/대상 선택/실행 순서는 Status에 남습니다.
[Serializable]
public sealed class CharacterEffectDefinition
{
    public CcDefine id;
    [Min(0), Tooltip("대상 소유자 턴 시작 감소 횟수. 0=명시적 해제까지, 1=다음 턴 시작까지, 2=한 턴 행동 제한.")]
    public int ownerTurnTicks;
    [Tooltip("SP/분신/이동 칸 수/배율 등 효과의 수치. 이동은 음수도 허용합니다.")]
    public int amount = 1;
    [Min(-1), Tooltip("protect/Charm 방어 횟수. -1=기간 동안 무제한, 양수=횟수 제한.")]
    public int charges = -1;
    public bool ignoresInstalledItems;
    public bool isSimpleMove = true;

    public bool IsValid => id != CcDefine.None && Enum.IsDefined(typeof(CcDefine), id) &&
        ownerTurnTicks >= 0 && (id == CcDefine.Move_plus ? amount != 0 : amount > 0) &&
        (charges == -1 || charges > 0);
}


/// <summary>
/// 기존 SO/UI의 스킬 분류 메타데이터입니다(직렬화 값 호환 유지).
/// 실제 효과 실행과 말 상태는 CcDefine / CcEffectService / PieceRuntimeData.Cc를 사용합니다.
/// </summary>
public enum CharacterSkillStatus
{
    None = 0,
    Get_point,
    Get_turn,
    Move_1,
    Transformation,
    Shield,
    Hide,
    No_back,
    Kill_atk,
    Catch_atk,
    Move_end,
    Binding
}

[CreateAssetMenu(fileName = "Character", menuName = "ScriptableObject/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("# Main Info")]
    public int char_ID;
    public string char_Name;
    [TextArea]
    public string char_Desc;
    
    public Sprite char_Icon;
    [Header("# Skills")]
    [Tooltip("인게임 UI에 표시할 패시브 스킬 이미지")]
    public Sprite passive_Icon;
    public string passive_Name;
    [TextArea]
    public string passive_Desc;
    
    [Tooltip("인게임 액티브 스킬 버튼에 표시할 이미지")]
    public Sprite active_Icon;
    public string active_Name;
    [TextArea]
    public string active_Desc;

    [Header("# Active Runtime")]
    [Min(0)]
    [Tooltip("0 means that the active skill has no turn cooldown.")]
    public int active_CooldownTurns;
    [Min(0)]
    [Tooltip("Skill points consumed only after the active skill succeeds.")]
    public int active_SkillPointCost;

    [Header("# Passive Runtime")]
    [Min(0)]
    [Tooltip("0 means that the passive uses only its character-specific trigger condition.")]
    public int passive_CooldownTurns;

    [Header("# Effect IDs (Runtime)")]
    [Tooltip("Status가 지정한 순서의 패시브 효과. 빈 목록은 별도의 발동/방어 규칙만 있는 패시브입니다.")]
    public CharacterEffectDefinition[] passive_Effects = Array.Empty<CharacterEffectDefinition>();
    public CharacterEffectDefinition[] active_Effects = Array.Empty<CharacterEffectDefinition>();
    [Tooltip("패시브가 막는 공통 상태 ID 목록. 잡기/Retire는 넣지 않으면 면역 대상이 아닙니다.")]
    public CcDefine[] passive_Immunities = Array.Empty<CcDefine>();
    [Min(0), Tooltip("거리 제한이 있는 액티브의 검색 거리/이동 거리.")]
    public int active_Range;

    // 기존 UI와 SO 직렬화 호환용 분류. 효과 실행에는 아래 값이 아니라 Effect IDs를 사용합니다.
    [Header("# Skill Status (Legacy UI)")]
    [Tooltip("지정되지 않은 패시브는 None으로 둡니다.")]
    public CharacterSkillStatus passive_Status = CharacterSkillStatus.None;
    [Tooltip("지정되지 않은 액티브는 None으로 둡니다.")]
    public CharacterSkillStatus active_Status = CharacterSkillStatus.None;

    [Header("# Modelling")]
    [Tooltip("이 챔피언을 선택한 플레이어의 모든 말에 생성할 직업 말 프리팹입니다.")]
    public GameObject piecePrefab;
    public GameObject visualModelPrefab;

    public bool HasPassiveStatus => passive_Status != CharacterSkillStatus.None;
    public bool HasActiveStatus => active_Status != CharacterSkillStatus.None;
    public bool HasActiveSkill =>
        HasActiveStatus || (active_Effects != null && active_Effects.Length > 0) ||
        !string.IsNullOrWhiteSpace(active_Name);
}
