using UnityEngine;

/// <summary>
/// 캐릭터 스킬이 인게임 시스템에 요청하는 효과의 종류입니다.
/// 실제 SP, 쿨타임, 대상, 지속 턴 같은 런타임 값은 플레이어 시스템이 관리합니다.
/// 새 효과가 필요할 때 기존 값을 재사용하지 말고 새 항목을 추가해야 합니다.
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
    public string passive_Name;
    [TextArea]
    public string passive_Desc;
    
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
    public GameObject visualModelPrefab;

    public bool HasPassiveStatus => passive_Status != CharacterSkillStatus.None;
    public bool HasActiveStatus => active_Status != CharacterSkillStatus.None;
    public bool HasActiveSkill =>
        HasActiveStatus || !string.IsNullOrWhiteSpace(active_Name);
}
