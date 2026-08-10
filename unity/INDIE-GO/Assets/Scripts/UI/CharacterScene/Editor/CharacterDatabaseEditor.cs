using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YutArena.UI.CharacterScene.Editor
{
    [CustomEditor(typeof(CharacterDatabase))]
    public class CharacterDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("프로젝트에서 캐릭터 데이터 자동 수집"))
            {
                CollectCharacterData();
            }
        }

        private void CollectCharacterData()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterData");
            List<CharacterData> foundCharacters = new List<CharacterData>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);

                if (data != null)
                {
                    foundCharacters.Add(data);
                }
            }

            foundCharacters.Sort((left, right) =>
            {
                int idComparison = left.char_ID.CompareTo(right.char_ID);
                return idComparison != 0 ? idComparison : string.CompareOrdinal(left.name, right.name);
            });

            serializedObject.Update();
            SerializedProperty charactersProperty = serializedObject.FindProperty("characters");
            charactersProperty.arraySize = foundCharacters.Count;

            for (int i = 0; i < foundCharacters.Count; i++)
            {
                charactersProperty.GetArrayElementAtIndex(i).objectReferenceValue = foundCharacters[i];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            Debug.Log($"캐릭터 데이터 {foundCharacters.Count}개를 ID 순서로 수집했습니다.", target);
        }
    }
}
