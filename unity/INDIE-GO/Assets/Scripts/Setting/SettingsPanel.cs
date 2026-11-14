using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using YutArena.Managers;
using YutArena.Common;

namespace YutArena.UI
{
    /// <summary>Modal settings. Drafts live independently of tabs and are committed only on Apply.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class SettingsPanel : MonoBehaviour
    {
        private static SettingsPanel instance;
#if UNITY_EDITOR
        public static event Action<GameObject> GeneratedObjectWillBeRemoved;
#endif
        private static int blockedThroughFrame = -1;
        public static bool IsBlockingInput => InGameSettingsPanel.IsBlockingInput || (instance != null && instance.overlay != null && instance.overlay.activeSelf) || Time.frameCount <= blockedThroughFrame;
        public static bool IsOpen => instance != null && instance.overlay != null && instance.overlay.activeSelf;
        private static readonly Color Ink = new Color32(31, 50, 79, 255);
        private static readonly Color Blue = new Color32(35, 78, 137, 255);
        private static readonly Color Cream = new Color32(250, 247, 239, 255);
        private static readonly Color Gold = new Color32(191, 157, 101, 255);
        [SerializeField] private SettingsTheme theme;
        [SerializeField, HideInInspector] private GameObject overlay;
        private GameObject modal, dropdown, launcher;
        private RectTransform surface, body;
        private TMP_Text dirtyLabel, countdown;
        private UnityEngine.UI.Button apply;
        private readonly List<UnityEngine.UI.Button> tabs = new List<UnityEngine.UI.Button>();
        private SettingsDraft saved, draft;
        private SettingsTab currentTab;
        private bool titleContext, closeAfterApply, previewing;
        private FullScreenMode previousMode;
        private Vector2Int previousSize;
        private float deadline;
        private Action escapeAction;
        private int openedFrame, bindingIndex = -1;
        private string candidate;
        private InputActionAsset bindingDraft;
        private InputActionRebindingExtensions.RebindingOperation rebindOperation;
        private string pendingBindingPath;
        private ButtonControl pendingBindingControl;
        private GameObject previousSelection;
        private EventSystem navigationEventSystem;
        private bool previousNavigationEvents;
        public SettingsDraft Draft => draft;
        public SettingsTab CurrentTab => currentTab;
        public bool HasChanges => draft != null && !draft.SameAs(saved);
        public bool IsPreviewing => previewing;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (instance != null && instance != this) { DisposeGenerated(gameObject); return; }
            instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
            // Authored scene layout is visible in edit mode, but starts closed in the game.
            if (overlay != null) overlay.SetActive(false);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (instance != null && instance != this) return;
            if (instance == null) instance = this;
            if (theme == null) theme = Resources.Load<SettingsTheme>("SettingsTheme");
            if (overlay != null && overlay.activeSelf) GameInput.SetGameplayBlocked(true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; blockedThroughFrame = -1; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            EnsureInstance();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            instance.ConfigureLauncher();
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (instance == null) return;
            instance.CloseImmediately();
            instance.ConfigureLauncher();
        }
        private static void EnsureInstance()
        {
            if (instance != null) return;
            instance = FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            if (instance != null) return;
            var root = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            instance = root.AddComponent<SettingsPanel>();
            instance.theme = Resources.Load<SettingsTheme>("SettingsTheme");
            DontDestroyOnLoad(root);
        }
        private void ConfigureLauncher()
        {
            if (launcher != null) DisposeGenerated(launcher);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "InGameScene") return;
            if (FindFirstObjectByType<InGameUIManager>() == null) return;
            launcher = Button(transform, "OpenSettings", "설정", 1770, 20, 125, 48, () => Show(false)).gameObject;
            var rect = launcher.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-25, -20);
        }
        public static void Show(bool fromTitle)
        {
            EnsureInstance();
            if (IsOpen) return;
            if (instance.theme == null || instance.theme.font == null)
            {
                Debug.LogError("SettingsTheme이 없습니다. Tools/Yut Arena/설정 UI 리소스 연결을 실행하세요."); return;
            }
            instance.titleContext = fromTitle;
            GameInput.SetGameplayBlocked(true);
            instance.saved = SettingsDraft.Load(); instance.draft = instance.saved.Copy();
            instance.currentTab = SettingsTab.General;
            instance.previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            instance.BlockBackgroundNavigation();
            instance.Build();
            instance.openedFrame = Time.frameCount;
        }
        public void OpenFromTitle() => Show(true);

        private void BlockBackgroundNavigation()
        {
            navigationEventSystem = EventSystem.current;
            if (navigationEventSystem == null) return;
            previousNavigationEvents = navigationEventSystem.sendNavigationEvents;
            navigationEventSystem.sendNavigationEvents = false;
            navigationEventSystem.SetSelectedGameObject(null);
        }
        private void RestoreBackgroundNavigation()
        {
            if (navigationEventSystem != null) navigationEventSystem.sendNavigationEvents = previousNavigationEvents;
            navigationEventSystem = null;
        }

        // ESC, outside clicks and the window close buttons share the same pending-change flow.
        public void DismissTopLayer()
        {
            if (dropdown != null) CloseDropdown();
            else if (modal != null) escapeAction?.Invoke();
            else RequestClose();
        }
        private void Update()
        {
            if (!IsOpen) return;
            if (previewing)
            {
                if (countdown != null) countdown.text = "새 화면 설정을 적용했습니다.\n" + Mathf.CeilToInt(Mathf.Max(0, deadline - Time.realtimeSinceStartup)) + "초 후 이전 화면 설정으로 돌아갑니다.";
                if (Time.realtimeSinceStartup >= deadline) { RevertDisplay(); return; }
            }
            if (Time.frameCount <= openedFrame) return;
            bool cancel = GameInput.UiPressed(UiAction.Cancel);
            if (rebindOperation != null)
            {
                if (cancel) CloseModal();
                return;
            }
            if (pendingBindingPath != null)
            {
                if (cancel) { CloseModal(); return; }
                // Wait for release so the captured click cannot also confirm the next dialog.
                if (pendingBindingControl != null && pendingBindingControl.device.added && pendingBindingControl.isPressed) return;
                string path = pendingBindingPath; pendingBindingPath = null; pendingBindingControl = null;
                CaptureBinding(path);
                return;
            }
            if (cancel)
            {
                DismissTopLayer();
                return;
            }
        }
        private bool PointerOverModalButton(Vector2 position)
        {
            if (EventSystem.current == null || modal == null) return false;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            foreach (var hit in hits)
                if (hit.gameObject.transform.IsChildOf(modal.transform) && hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null) return true;
            return false;
        }
        private void OnDestroy()
        {
            StopRebinding();
            RestoreBackgroundNavigation();
            if (previewing) GraphicsSettingsManager.PreviewDisplay(previousMode, previousSize);
            if (instance == this) { GameInput.SetGameplayBlocked(false); instance = null; SceneManager.sceneLoaded -= OnSceneLoaded; }
        }

        private void Build()
        {
            if (overlay != null) DisposeGenerated(overlay);
            overlay = Rect(transform, "SettingsOverlay", 0, 0, 1920, 1080).gameObject;
            Stretch(overlay.GetComponent<RectTransform>());
            var blocker = Image(overlay.transform, "InputBlocker", 0, 0, 1920, 1080, new Color(.08f, .14f, .24f, .35f), true);
            Stretch(blocker.rectTransform);
            blocker.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(DismissTopLayer);
            surface = Rect(overlay.transform, "Window", titleContext ? 0 : -220, 0, 1920, 1080);
            surface.anchorMin = surface.anchorMax = surface.pivot = new Vector2(.5f, .5f);
            surface.anchoredPosition = new Vector2(titleContext ? 0 : -220, 0);
            Image(surface, "WindowInputShield", 529, 135, 1302, 810, Color.clear, true);
            Raw(surface, "Frame", theme.titleWindow, 468, 84, 1425, 935);
            Text(surface, "Heading", "설정", 592, 150, 300, 60, 43);
            Button(surface, "Close", "", 1718, 155, 60, 60, RequestClose, clear: true);
            tabs.Clear();
            SettingsTab[] visible = titleContext ? new[] { SettingsTab.General, SettingsTab.Display, SettingsTab.Sound, SettingsTab.Controls } : new[] { SettingsTab.General, SettingsTab.Sound, SettingsTab.Controls };
            float width = 1178f / visible.Length;
            for (int i = 0; i < visible.Length; i++)
            {
                SettingsTab tab = visible[i];
                var button = Button(surface, "Tab" + tab, TabName(tab), 591 + width * i, 248, width - 8, 53, () => SelectTab(tab), clear: true);
                tabs.Add(button);
            }
            Button(surface, "Back", "돌아가기", 591, 860, 150, 47, RequestClose, clear: true);
            Button(surface, "RestoreDefaults", "기본값 복원", 1452, 860, 155, 47, ConfirmDefaults, clear: true);
            apply = Button(surface, "Apply", "적용", 1619, 860, 150, 47, () => Apply(false));
            dirtyLabel = Text(surface, "DirtyStatus", "", 1100, 807, 665, 30, 19, TextAlignmentOptions.Right);
            body = Rect(surface, "Content", 591, 330, 1178, 465);
            SelectTab(currentTab);
        }

#if UNITY_EDITOR
        [ContextMenu("씬 설정창 레이아웃 새로 만들기")]
        public void BuildSceneLayout()
        {
            if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            theme = Resources.Load<SettingsTheme>("SettingsTheme");
            if (theme == null || theme.font == null) throw new InvalidOperationException("SettingsTheme 리소스를 먼저 연결해 주세요.");
            titleContext = true; currentTab = SettingsTab.General;
            saved = SettingsDraft.Load(); draft = saved.Copy();
            if (overlay == null)
            {
                var existing = transform.Find("SettingsOverlay");
                if (existing != null) overlay = existing.gameObject;
            }
            Build();
        }
#endif
        public void SelectTab(SettingsTab tab)
        {
            if (!titleContext && tab == SettingsTab.Display) return;
            CloseDropdown(); currentTab = tab;
            foreach (var button in tabs)
            {
                bool selected = button.name == "Tab" + tab;
                var tabImage = button.GetComponent<UnityEngine.UI.Image>();
                tabImage.sprite = selected ? theme.activeButton : null;
                tabImage.type = UnityEngine.UI.Image.Type.Sliced;
                tabImage.color = selected ? Color.white : Color.clear;
                button.GetComponentInChildren<TMP_Text>().color = selected ? Color.white : Ink;
            }
            ClearChildren(body);
            switch (tab)
            {
                case SettingsTab.General: BuildGeneral(); break;
                case SettingsTab.Display: BuildDisplay(); break;
                case SettingsTab.Sound: BuildSound(); break;
                case SettingsTab.Controls: BuildControls(); break;
            }
            RefreshDirty();
        }
        private static string TabName(SettingsTab tab) => tab == SettingsTab.General ? "일반" : tab == SettingsTab.Display ? "화면" : tab == SettingsTab.Sound ? "사운드" : "조작";
        private void BuildGeneral()
        {
            RowLabel("언어", 14);
            var language = Button(body, "LanguageAppearanceOnly", "한국어", 425, 14, 751, 46, () => {});
            var languageText = language.GetComponentInChildren<TMP_Text>();
            languageText.alignment = TextAlignmentOptions.MidlineLeft;
            languageText.margin = new Vector4(18, 0, 45, 0); languageText.fontStyle = FontStyles.Normal;
            DropdownArrow(language.transform, 751);
            language.interactable = false;
            RowLabel("연출 속도", 85);
            Choice("Speed", new[] { "보통", "빠름" }, draft.speed, 425, 85, 751, i => { draft.speed = i; RefreshDirty(); });
            RowLabel("글자 크기", 171);
            string[] sizes = { "작게", "보통", "크게" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Button(body, "TextSize" + i, sizes[i], 855 + i * 109, 162, 102, 48, () => { draft.textSize = index; SelectTab(currentTab); }, selected: draft.textSize == i);
            }
            Image(body, "PreviewBorder", 2, 269, 1174, 167, Gold);
            Image(body, "PreviewBackground", 3, 270, 1172, 165, new Color(1, 1, 1, .85f));
            Text(body, "PreviewCaption", "미리보기", 43, 303, 300, 35, 19);
            Text(body, "TextSizePreview", "선택한 말의 정보를 확인합니다.", 43, 355, 1080, 55, draft.textSize == 0 ? 22 : draft.textSize == 1 ? 27 : 32).fontStyle = FontStyles.Bold;
        }
        private void BuildDisplay()
        {
            RowLabel("화면 모드", 14);
            int mode = draft.mode == FullScreenMode.ExclusiveFullScreen ? 0 : draft.mode == FullScreenMode.FullScreenWindow ? 1 : 2;
            Choice("ScreenMode", new[] { "전체 화면", "테두리 없는 전체 화면", "창 모드" }, mode, 425, 14, 751, i => { draft.mode = i == 0 ? FullScreenMode.ExclusiveFullScreen : i == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; RefreshDirty(); });
            RowLabel("해상도", 85);
            List<Vector2Int> resolutions = GraphicsSettingsManager.GetSupportedResolutions();
            var current = new Vector2Int(draft.width, draft.height);
            if (!resolutions.Contains(current)) resolutions.Add(current);
            var options = new string[resolutions.Count];
            for (int i = 0; i < options.Length; i++) options[i] = resolutions[i].x + " × " + resolutions[i].y;
            Choice("Resolution", options, resolutions.IndexOf(current), 425, 85, 751, i => { draft.width = resolutions[i].x; draft.height = resolutions[i].y; RefreshDirty(); });
            RowLabel("수직 동기화", 156);
            Toggle("VSync", draft.vSync, 1080, 165, "사용", v => { draft.vSync = v; SelectTab(currentTab); });
            RowLabel("최대 프레임", 227);
            var frame = Choice("FrameRate", new[] { "30", "60", "120", "144", "제한 없음" }, draft.frameRate, 425, 227, 751, i => { draft.frameRate = i; RefreshDirty(); });
            frame.interactable = !draft.vSync;
            RowLabel("그래픽 품질", 298);
            Choice("Quality", new[] { "낮음", "중간", "높음" }, draft.quality, 425, 298, 751, i => { draft.quality = i; RefreshDirty(); });
            RowLabel("밝기", 369);
            Slider("Brightness", draft.brightness, 425, 379, 679, v => { draft.brightness = v; RefreshDirty(); });
        }
        private void BuildSound()
        {
            string[] names = { "전체 음량", "배경 음악", "효과음" };
            float[] values = { draft.master, draft.bgm, draft.effects };
            bool[] mutes = { draft.masterMuted, draft.bgmMuted, draft.effectsMuted };
            for (int i = 0; i < 3; i++)
            {
                int index = i; float y = 30 + i * 96;
                RowLabel(names[i], y);
                Slider("Volume" + i, values[i], 234, y + 10, 710, v => {
                    if (index == 0) draft.master = v; else if (index == 1) draft.bgm = v; else draft.effects = v;
                    RefreshDirty();
                });
                Toggle("Mute" + i, mutes[i], 1064, y + 9, "음소거", v => {
                    if (index == 0) draft.masterMuted = v; else if (index == 1) draft.bgmMuted = v; else draft.effectsMuted = v;
                    RefreshDirty();
                });
            }
            RowLabel("백그라운드 재생", 307);
            Toggle("BackgroundSound", draft.backgroundSound, 1080, 314, "사용", v => { draft.backgroundSound = v; RefreshDirty(); });
        }
        private void BuildControls()
        {
            Text(body, "BindingHint", "기본 입력을 선택하면 단축키를 변경할 수 있습니다.", 2, 0, 1170, 35, 19);
            Image(body, "TableHeading", 2, 39, 1174, 39, new Color32(235, 227, 209, 255));
            Text(body, "ActionHeading", "동작", 20, 39, 390, 39, 18);
            Text(body, "PrimaryHeading", "기본 입력", 442, 39, 305, 39, 18);
            Text(body, "SecondaryHeading", "보조 입력 · 고정", 782, 39, 390, 39, 18);
            for (int i = 0; i < SettingsBindings.Labels.Length; i++)
            {
                int index = i; float y = 82 + i * 45;
                Text(body, "Action" + i, SettingsBindings.Labels[i], 20, y, 413, 37, 21);
                Button(body, "Binding" + i, SettingsBindings.Display(draft.bindings[i]), 442, y, 305, 35, () => BeginBinding(index));
                Text(body, "Secondary" + i, SettingsBindings.Secondary[i], 782, y, 390, 37, 19);
                Image(body, "Rule" + i, 2, y + 41, 1174, 1, new Color32(218, 202, 174, 255));
            }
        }
        private void RowLabel(string label, float y)
        {
            Text(body, label, label, 2, y, 405, 46, 23).fontStyle = FontStyles.Bold;
            Image(body, label + "Rule", 2, y + 58, 1174, 1, new Color32(218, 202, 174, 255));
        }
        private void RefreshDirty()
        {
            if (apply == null) return;
            apply.interactable = HasChanges;
            apply.GetComponent<UnityEngine.UI.Image>().sprite = HasChanges ? theme.activeButton : theme.idleButton;
            apply.GetComponent<UnityEngine.UI.Image>().color = HasChanges ? Color.white : new Color32(231, 225, 211, 255);
            apply.GetComponentInChildren<TMP_Text>().color = HasChanges ? Color.white : new Color32(143, 138, 124, 255);
            dirtyLabel.text = HasChanges ? "변경 사항 있음" : "";
        }

        public void ConfirmDefaults()
        {
            Dialog("현재 탭을 기본값으로 되돌릴까요?", "다른 탭의 변경값은 유지됩니다.",
                new[] { "취소", "되돌리기" }, new Action[] { CloseModal, () => { draft.ResetTab(currentTab); CloseModal(); SelectTab(currentTab); } }, CloseModal);
        }
        public void RequestClose()
        {
            if (!HasChanges) { CloseImmediately(); return; }
            Dialog("변경 사항을 적용할까요?", "저장하지 않은 변경 사항이 있습니다.",
                new[] { "계속 설정", "적용 안 함", "적용 후 닫기" },
                new Action[] { CloseModal, CloseImmediately, () => { CloseModal(); Apply(true); } }, CloseModal);
        }
        public void Apply(bool closeWhenSaved)
        {
            if (!HasChanges) { if (closeWhenSaved) CloseImmediately(); return; }
            closeAfterApply = closeWhenSaved;
            if (!draft.DisplayChanged(saved)) { Commit(); return; }
            previousMode = Screen.fullScreenMode;
            previousSize = new Vector2Int(Screen.width, Screen.height);
            previewing = true;
            GraphicsSettingsManager.PreviewDisplay(draft.mode, new Vector2Int(draft.width, draft.height));
            deadline = Time.realtimeSinceStartup + 10f;
            Dialog("이 화면 설정을 유지할까요?", "새 화면 설정을 적용했습니다.\n10초 후 이전 화면 설정으로 돌아갑니다.",
                new[] { "유지", "되돌리기" }, new Action[] { KeepDisplay, RevertDisplay }, RevertDisplay);
        }
        public void KeepDisplay()
        {
            if (!previewing) return;
            previewing = false; CloseModal(); Commit();
        }
        public void RevertDisplay()
        {
            if (!previewing) return;
            GraphicsSettingsManager.PreviewDisplay(previousMode, previousSize);
            previewing = false; closeAfterApply = false;
            draft.RestoreDisplay(saved); CloseModal(); SelectTab(currentTab);
        }
        private void Commit()
        {
            try
            {
                draft.Save(); saved = draft.Copy(); RefreshDirty();
                if (closeAfterApply) CloseImmediately();
            }
            catch (Exception error)
            {
                closeAfterApply = false;
                Debug.LogException(error);
                Dialog("설정을 저장하지 못했습니다.", "잠시 후 다시 적용해 주세요.", new[] { "계속 설정" }, new Action[] { CloseModal }, CloseModal);
            }
        }
        public void CloseImmediately()
        {
            if (previewing) { GraphicsSettingsManager.PreviewDisplay(previousMode, previousSize); previewing = false; }
            CloseDropdown(); CloseModal();
            if (overlay != null) { DisposeGenerated(overlay); overlay = null; }
            blockedThroughFrame = Time.frameCount;
            draft = saved = null; closeAfterApply = false;
            GameInput.SetGameplayBlocked(false);
            RestoreBackgroundNavigation();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        public static void ConfirmQuit(Action quit)
        {
            EnsureInstance();
            if (IsOpen) return;
            instance.overlay = Rect(instance.transform, "QuitOverlay", 0, 0, 1920, 1080).gameObject;
            GameInput.SetGameplayBlocked(true);
            Stretch(instance.overlay.GetComponent<RectTransform>());
            instance.openedFrame = Time.frameCount;
            instance.BlockBackgroundNavigation();
            instance.Dialog("게임을 종료할까요?", "", new[] { "예", "아니오" }, new Action[] { quit, instance.CloseImmediately }, instance.CloseImmediately);
        }
        private void Dialog(string heading, string description, string[] labels, Action[] actions, Action escape)
        {
            CloseDropdown(); CloseModal();
            modal = Rect(overlay.transform, "Confirmation", 0, 0, 1920, 1080).gameObject;
            Stretch(modal.GetComponent<RectTransform>());
            var modalBlocker = Image(modal.transform, "ModalBlocker", 0, 0, 1920, 1080, new Color(.04f, .09f, .18f, .60f), true);
            Stretch(modalBlocker.rectTransform);
            modalBlocker.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(DismissTopLayer);
            var frame = Rect(modal.transform, "Dialog", 618, 373, 684, 334);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;
            Image(frame, "DialogInputShield", 0, 0, 684, 334, Color.clear, true);
            if (theme.confirmation != null)
            {
                var art = Raw(frame, "Frame", theme.confirmation, 0, 0, 684, 334);
                art.uvRect = new Rect(618f / 1920, 373f / 1080, 684f / 1920, 334f / 1080);
            }
            else Image(frame, "Frame", 0, 0, 684, 334, Cream);
            // Cover the two baked button faces so close-confirmation can fit three choices.
            Image(frame, "ButtonArea", 16, 218, 650, 96, Cream);
            Text(frame, "DialogHeading", heading, 25, 65, 634, 65, 29, TextAlignmentOptions.Center);
            countdown = Text(frame, "DialogDescription", description, 35, 139, 614, 72, 21, TextAlignmentOptions.Center);
            float width = labels.Length == 3 ? 191 : 148;
            float total = labels.Length * width + (labels.Length - 1) * 16;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                Button(frame, "DialogAction" + i, labels[i], (684 - total) / 2 + i * (width + 16), 241, width, 52, () => actions[index](), selected: i == 0);
            }
            escapeAction = escape;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        private void CloseModal()
        {
            StopRebinding(); pendingBindingPath = null; pendingBindingControl = null;
            if (modal != null) { DisposeGenerated(modal); modal = null; }
            bindingIndex = -1; candidate = null; escapeAction = null; countdown = null;
            openedFrame = Time.frameCount;
        }
        private void BeginBinding(int index)
        {
            Dialog("단축키 변경", "새로 사용할 키를 눌러 주세요.\n기존 입력: " + SettingsBindings.Display(draft.bindings[index]),
                new[] { "취소" }, new Action[] { CloseModal }, CloseModal);
            bindingIndex = index;
            StartRebinding(index);
        }
        private void StartRebinding(int index)
        {
            StopRebinding();
            bindingDraft = GameInput.CreateBindingDraft(draft.bindings);
            var action = GameInput.FindGameAction(bindingDraft, (GameAction)index);
            var mousePositions = new Dictionary<Mouse, Vector2>();
            rebindOperation = action.PerformInteractiveRebinding(0)
                .WithExpectedControlType<ButtonControl>()
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithMatchingEventsBeingSuppressed(false)
                .OnComputeScore((control, inputEvent) => {
                    if (control.device is Mouse source)
                    {
                        // Rebinding observes the event before its state is applied to the device.
                        if (!source.position.ReadValueFromEvent(inputEvent, out Vector2 position)) position = source.position.ReadValue();
                        mousePositions[source] = position;
                    }
                    return control.synthetic ? 1f : 2f;
                })
                .OnPotentialMatch(operation => {
                    if (operation.selectedControl.device is Mouse source && operation.selectedControl == source.leftButton &&
                        PointerOverModalButton(mousePositions.TryGetValue(source, out Vector2 position) ? position : source.position.ReadValue()))
                    {
                        operation.RemoveCandidate(operation.selectedControl); return;
                    }
                    operation.Complete();
                })
                .OnComplete(operation => {
                    string path = action.bindings[0].effectivePath;
                    var control = operation.selectedControl as ButtonControl;
                    StopRebinding();
                    pendingBindingPath = path; pendingBindingControl = control;
                    openedFrame = Time.frameCount;
                })
                .OnCancel(operation => CloseModal())
                .Start();
        }
        private void StopRebinding()
        {
            var operation = rebindOperation; rebindOperation = null;
            if (operation != null)
            {
                operation.OnCancel(null).OnComplete(null);
                if (!operation.completed && !operation.canceled) operation.Cancel();
                operation.Dispose();
            }
            GameInput.Release(bindingDraft); bindingDraft = null;
        }
        private void CaptureBinding(string path)
        {
            int index = bindingIndex;
            StopRebinding();
            // Piece, destination and target selection share a pointer button in separate contexts.
            for (int i = 0; i < draft.bindings.Length; i++)
                    if (i != index && draft.bindings[i] == path &&
                        !(IsSelectionBinding(i) && IsSelectionBinding(index)))
                    {
                        countdown.text = "이미 '" + SettingsBindings.Labels[i] + "'에 사용 중입니다.\n다른 키를 눌러 주세요.";
                        StartRebinding(index);
                        return;
                    }
            Dialog("이 단축키로 변경할까요?", SettingsBindings.Display(draft.bindings[index]) + "  →  " + SettingsBindings.Display(path),
                new[] { "확인", "취소" }, new Action[] { () => { draft.bindings[index] = path; CloseModal(); SelectTab(currentTab); }, CloseModal }, CloseModal);
            candidate = path;
        }
        private static bool IsSelectionBinding(int index) => index == 1 || index == 2 || index == 4;

        private UnityEngine.UI.Button Choice(string name, string[] choices, int selected, float x, float y, float width, Action<int> change)
        {
            var button = Button(body, name, "", x, y, width, 46, () => {});
            var label = button.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(18, 0, 45, 0); label.fontStyle = FontStyles.Normal;
            label.text = choices[Mathf.Clamp(selected, 0, choices.Length - 1)];
            DropdownArrow(button.transform, width);
            button.onClick.AddListener(() => OpenDropdown(button, choices, selected, value => {
                selected = value;
                label.text = choices[value];
                change(value);
            }));
            return button;
        }
        private void DropdownArrow(Transform parent, float width)
        {
            var arrow = Image(parent, "Arrow", width - 31, 18, 13, 8, Ink);
            arrow.sprite = theme.dropdownArrow;
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        }
        private void OpenDropdown(UnityEngine.UI.Button owner, string[] choices, int selected, Action<int> change)
        {
            CloseDropdown();
            dropdown = Rect(overlay.transform, "Dropdown", 0, 0, 1920, 1080).gameObject;
            Stretch(dropdown.GetComponent<RectTransform>());
            Stretch(Button(dropdown.transform, "DismissDropdown", "", 0, 0, 1920, 1080, CloseDropdown, clear: true).GetComponent<RectTransform>());
            var ownerRect = owner.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4]; ownerRect.GetWorldCorners(corners);
            Vector3 position = overlay.transform.InverseTransformPoint(corners[0]);
            var overlayRect = overlay.GetComponent<RectTransform>();
            float x = position.x - overlayRect.rect.xMin;
            float top = overlayRect.rect.yMax - position.y + 4;
            float width = ownerRect.rect.width;
            float height = Mathf.Min(choices.Length * 44 + 8, 300);
            top = Mathf.Min(top, overlayRect.rect.height - height - 25);
            var list = Image(dropdown.transform, "Options", x, top, width, height, Cream, true).rectTransform;
            var border = list.gameObject.AddComponent<UnityEngine.UI.Outline>(); border.effectColor = Gold; border.effectDistance = new Vector2(1, -1);
            var scroll = list.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Image(list, "Viewport", 3, 3, width - 6, height - 6, Cream, true).rectTransform;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            var content = Rect(viewport, "Content", 0, 0, width - 6, choices.Length * 44);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            for (int i = 0; i < choices.Length; i++)
            {
                int value = i;
                var option = Button(content, "Option" + i, choices[i], 0, 0, width - 6, 44, () => { CloseDropdown(); change(value); }, selected: i == selected, clear: true);
                option.GetComponent<UnityEngine.UI.Image>().color = i == selected ? Blue : Cream;
                var text = option.GetComponentInChildren<TMP_Text>();
                text.alignment = TextAlignmentOptions.MidlineLeft; text.margin = new Vector4(18, 0, 40, 0); text.fontStyle = FontStyles.Normal;
                if (i == selected)
                {
                    var check = Image(option.transform, "SelectedCheck", width - 39, 16, 16, 12, Color.white);
                    check.sprite = theme.checkmark;
                }
                option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44;
            }
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((selected - 2f) / Mathf.Max(1, choices.Length - 5));
        }
        private void CloseDropdown()
        {
            if (dropdown != null) { DisposeGenerated(dropdown); dropdown = null; }
        }
        private void Slider(string name, float value, float x, float y, float width, Action<float> changed)
        {
            var root = Rect(body, name, x, y - 5, width, 30);
            var track = Image(root, "Track", 0, 12, width, 8, new Color32(221, 212, 191, 255), true);
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var fillArea = Rect(root, "FillArea", 0, 12, width, 8);
            var fill = Image(fillArea, "Fill", 0, 0, width, 8, Blue);
            Stretch(fill.rectTransform);
            var handleArea = Rect(root, "HandleArea", 0, 3, width, 24);
            var handle = Image(handleArea, "Handle", 0, 0, 24, 0, Color.white, true);
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            handle.sprite = theme.sliderHandle;
            handle.preserveAspect = true;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = 1; slider.SetValueWithoutNotify(value);
            var number = Text(body, name + "Value", Mathf.RoundToInt(value * 100).ToString(), x + width + 20, y - 5, 55, 32, 21, TextAlignmentOptions.Center);
            slider.onValueChanged.AddListener(v => { number.text = Mathf.RoundToInt(v * 100).ToString(); changed(v); });
        }
        private void Toggle(string name, bool value, float x, float y, string caption, Action<bool> changed)
        {
            var button = Button(body, name, "", x, y - 8, 44, 38, () => {}, clear: true);
            var face = Image(button.transform, "ToggleFace", 0, 8, 44, 22, Color.white);
            face.sprite = value ? theme.toggleOn : theme.toggleOff;
            Text(body, name + "Caption", caption, x + 55, y - 8, 70, 38, 20);
            button.onClick.AddListener(() => { value = !value; face.sprite = value ? theme.toggleOn : theme.toggleOff; changed(value); });
        }
        private UnityEngine.UI.Button Button(Transform parent, string name, string label, float x, float y, float w, float h, Action action, bool selected = false, bool clear = false)
        {
            var image = Image(parent, name, x, y, w, h, clear ? Color.clear : selected ? Blue : Cream, true);
            if (!clear)
            {
                image.sprite = selected ? theme.activeButton : theme.idleButton;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = Color.white;
                if (image.sprite == null) { var outline = image.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = Gold; outline.effectDistance = new Vector2(1, -1); }
            }
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.9f, .94f, 1); colors.pressedColor = new Color(.7f, .8f, .95f); colors.disabledColor = Color.white; button.colors = colors;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            var text = Text(image.transform, "Label", label, 0, 0, w, h, 22, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold; text.color = selected ? Color.white : Ink;
            return button;
        }
        private TMP_Text Text(Transform parent, string name, string text, float x, float y, float w, float h, float size, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = theme != null ? theme.font : null; label.text = text; label.fontSize = size;
            label.color = Ink; label.alignment = alignment; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static UnityEngine.UI.Image Image(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = raycast; return image;
        }
        private static UnityEngine.UI.RawImage Raw(Transform parent, string name, Texture texture, float x, float y, float w, float h)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var raw = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.texture = texture; raw.raycastTarget = false; return raw;
        }
        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                DisposeGenerated(child);
            }
        }
        internal static void DisposeGenerated(GameObject generated)
        {
#if UNITY_EDITOR
            GeneratedObjectWillBeRemoved?.Invoke(generated);
#endif
            generated.SetActive(false);
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }
    }
}
