#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using YutArena.UI;

namespace YutArena.EditorTools
{
    public static class SettingsUIInstaller
    {
        private const string Root = "Assets/Sprites/설정창_타이틀_UI/";
        private const string Path = "Assets/Scripts/Resources/SettingsTheme.asset";

        [MenuItem("Tools/Yut Arena/설정 UI 리소스 연결")]
        public static void Install()
        {
            GameInputAssetInstaller.Install();
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/Resources")) AssetDatabase.CreateFolder("Assets/Scripts", "Resources");
            var theme = AssetDatabase.LoadAssetAtPath<SettingsTheme>(Path);
            if (theme == null) { theme = ScriptableObject.CreateInstance<SettingsTheme>(); AssetDatabase.CreateAsset(theme, Path); }
            theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NEXON Lv2 Gothic SDF.asset");
            theme.titleWindow = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "공통_설정창과_하단버튼.png");
            theme.gameWindow = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/설정창_인게임_UI/좀 더 세부 파일 모음/01_window.png");
            theme.confirmation = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "공통_취소확인_팝업.png");
            theme.sliderHandle = Sprite("공통_슬라이더_손잡이.png");
            theme.toggleOn = Sprite("공통_토글_켜짐.png"); theme.toggleOff = Sprite("공통_토글_꺼짐.png");
            theme.dropdownArrow = Sprite("세부 이미지/03_선택칸_위쪽화살표.png");
            theme.checkmark = Sprite("세부 이미지/08_선택항목_체크표시.png");
            theme.activeButton = Sprite("공통_적용버튼_활성.png");
            if (theme.idleButton == null)
            {
                theme.idleButton = UnityEngine.Sprite.Create(theme.titleWindow, new Rect(123, theme.titleWindow.height - 822, 150, 47), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
                theme.idleButton.name = "SettingsIdleButton";
                AssetDatabase.AddObjectToAsset(theme.idleButton, theme);
            }
            if (theme.font == null || theme.titleWindow == null || theme.sliderHandle == null || theme.toggleOn == null || theme.toggleOff == null)
                throw new System.InvalidOperationException("설정 UI 리소스 중 누락된 항목이 있습니다.");
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
            Debug.Log("SettingsPanel: NEXON Lv2 Gothic SDF 및 설정 UI 리소스 연결 완료.");
        }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name).OfType<Sprite>().FirstOrDefault();

        [MenuItem("Tools/Yut Arena/StartScene에 설정창 배치")]
        public static void InstallSceneLayout()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Play 모드를 종료해 주세요.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "StartScene") throw new System.InvalidOperationException("StartScene에서 실행해 주세요.");
            Install();
            var panel = Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                var root = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(root, "Place SettingsPanel");
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                panel = root.AddComponent<SettingsPanel>();
            }
            panel.BuildSceneLayout();
            var controller = Object.FindFirstObjectByType<YutArena.UI.StartSceneUIController>();
            if (controller != null)
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("settingsPanel").objectReferenceValue = panel;
                serialized.ApplyModifiedProperties();
                foreach (string field in new[] { "helpPanel", "onlineGameButtons", "onlineJoinPanel" })
                {
                    var previousPopup = serialized.FindProperty(field)?.objectReferenceValue as GameObject;
                    if (previousPopup == null || !previousPopup.activeSelf) continue;
                    Undo.RecordObject(previousPopup, "Hide legacy title popup");
                    previousPopup.SetActive(false);
                }
            }
            EditorUtility.SetDirty(panel);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = panel.gameObject;
            Debug.Log("StartScene 루트의 SettingsPanel에 편집 모드 레이아웃을 저장했습니다.");
        }
    }
}
#endif
