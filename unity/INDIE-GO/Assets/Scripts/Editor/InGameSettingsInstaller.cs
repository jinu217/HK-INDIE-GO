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
            Texture2D Load(string prefix)
            {
                string file = paths.FirstOrDefault(p => System.IO.Path.GetFileName(p).StartsWith(prefix));
                if (file == null) throw new System.InvalidOperationException("인게임 설정 이미지 누락: " + prefix);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            }
            theme.windowBackground = Load("01_");
            theme.closeIcon = Load("02_");
            theme.selectedTab = Load("03_");
            theme.footerButton = Load("04_");
            theme.generalPreview = Load("05_");
            theme.sizeIdle = Load("06_");
            theme.sizeSelected = Load("07_");
            theme.soundDividers = Load("08_");
            theme.sliderEmpty = Load("09_");
            theme.sliderFilled = Load("10_");
            theme.sliderThumb = Load("11_");
            theme.controlsTable = Load("12_");
            theme.bindingIdle = Load("13_");
            theme.bindingListening = Load("14_");
            theme.confirmationScrim = Load("15_");
            theme.confirmationFrame = Load("16_");
            theme.confirmationCancel = Load("17_");
            theme.confirmationAccept = Load("18_");
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
                var prefab = Resources.Load<GameObject>("UI/Settings/InGameSettingsPanelRoot");
                if (prefab == null) throw new System.InvalidOperationException("InGameSettingsPanelRoot prefab is missing.");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(root, "Place InGameSettingsPanel");
                panel = root.GetComponent<InGameSettingsPanel>();
            }
            panel.BuildSceneLayout();
            var button = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == "OptionBtn" && b.gameObject.scene == scene);
            if (button == null) throw new System.InvalidOperationException("OptionBtn이 없습니다.");
            var panelFields = new SerializedObject(panel);
            panelFields.FindProperty("optionButton").objectReferenceValue = button;
            panelFields.ApplyModifiedProperties();
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
