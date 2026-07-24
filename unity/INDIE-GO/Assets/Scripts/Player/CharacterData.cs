using UnityEngine;
[CreateAssetMenu(fileName = "Character", menuName = "ScriptableObject/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("# Main Info")]
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
    
    [Header("# Costs")]
    public int cooldown_Turn;
    public int cost_Sp;
    [Header("# Modelling")]
    public GameObject visualModelPrefab;
}
