using UnityEngine;

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

    [Header("# Skill Status")]
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
        HasActiveStatus || !string.IsNullOrWhiteSpace(active_Name);
}
