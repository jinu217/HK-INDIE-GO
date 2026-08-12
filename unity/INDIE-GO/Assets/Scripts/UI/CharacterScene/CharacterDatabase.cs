using System.Collections.Generic;
using UnityEngine;

namespace YutArena.UI.CharacterScene
{
    [CreateAssetMenu(
        fileName = "CharacterDatabase",
        menuName = "Character/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [Tooltip("캐릭터 데이터 목록")]
        [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

        public IReadOnlyList<CharacterData> Characters => characters;

        public CharacterData FindById(int characterId)
        {
            return characters.Find(data => data != null && data.char_ID == characterId);
        }
    }
}
