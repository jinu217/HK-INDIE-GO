#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using YutArena.UI;

namespace YutArena.EditorTools
{
    [InitializeOnLoad]
    public static class InGameSettingsInstaller
    {
        private const string Path = "Assets/Scripts/Resources/InGameSettingsTheme.asset";
        private const string Art = "Assets/Sprites/설정창_인게임_UI/좀 더 세부 파일 모음";
        static InGameSettingsInstaller() { EditorApplication.delayCall += EnsureInstalled; }
        private static void EnsureInstalled()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.LoadAssetAtPath<InGameSettingsTheme>(Path) == null) Install();
        }
        [MenuItem("Tools/Yut Arena/인게임 설정 UI 리소스 연결")]
        public static void Install()
        {
            var theme = AssetDatabase.LoadAssetAtPath<InGameSettingsTheme>(Path);
            if (theme == null) { theme = ScriptableObject.CreateInstance<InGameSettingsTheme>(); AssetDatabase.CreateAsset(theme, Path); }
            theme.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NEXON Lv2 Gothic SDF.asset");
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { Art }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            theme.layers = new Texture2D[18];
            for (int i = 0; i < 18; i++)
            {
                string prefix = (i + 1).ToString("00") + "_";
                string file = paths.FirstOrDefault(p => System.IO.Path.GetFileName(p).StartsWith(prefix));
                if (file == null) throw new System.InvalidOperationException("인게임 설정 이미지 누락: " + prefix);
                theme.layers[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            }
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
        }

        [MenuItem("Tools/Yut Arena/InGameScene에 설정창 배치")]
        public static void InstallSceneLayout()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Play 모드를 종료해 주세요.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "InGameScene") throw new System.InvalidOperationException("InGameScene에서 실행해 주세요.");
            Install();
            var panel = Object.FindFirstObjectByType<InGameSettingsPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                var root = new GameObject("InGameSettingsPanel", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(root, "Place in-game settings");
                panel = root.AddComponent<InGameSettingsPanel>();
            }
            panel.BuildSceneLayout();
            var button = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == "OptionBtn" && b.gameObject.scene == scene);
            if (button == null) throw new System.InvalidOperationException("OptionBtn이 없습니다.");
            bool connected = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                connected |= button.onClick.GetPersistentTarget(i) == panel && button.onClick.GetPersistentMethodName(i) == nameof(InGameSettingsPanel.Open);
            if (!connected)
            {
                Undo.RecordObject(button, "Connect in-game settings");
                UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, panel.Open);
                EditorUtility.SetDirty(button);
            }
            EditorUtility.SetDirty(panel);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = panel.gameObject;
        }
    }
}
#endif
