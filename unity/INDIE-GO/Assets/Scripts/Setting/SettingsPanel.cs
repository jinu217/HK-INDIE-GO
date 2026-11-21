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
        [SerializeField] private SettingsTheme theme;
        [SerializeField, HideInInspector] private GameObject overlay;
        [SerializeField] private SettingsPanelView titleView;
        [SerializeField] private SettingsPanelView menuView;
        private SettingsPanelView activeView;
        private GameObject modal, dropdown;
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
            if (titleView != null) titleView.gameObject.SetActive(false);
            if (menuView != null) menuView.gameObject.SetActive(false);
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
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (instance == null) return;
            instance.CloseImmediately();
        }
        private static void EnsureInstance()
        {
            if (instance != null) return;
            instance = FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            if (instance != null) return;
            var prefab = Resources.Load<GameObject>("UI/Settings/SettingsPanelRoot");
            if (prefab == null) throw new InvalidOperationException("SettingsPanelRoot prefab is missing.");
            instance = Instantiate(prefab).GetComponent<SettingsPanel>();
            DontDestroyOnLoad(instance.gameObject);
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

        private SettingsPanelView EnsureView(bool fromTitle)
        {
            var view = fromTitle ? titleView : menuView;
            if (view == null)
            {
                var prefab = Resources.Load<SettingsPanelView>(fromTitle ? "UI/Settings/TitleSettingsView" : "UI/Settings/MenuSettingsView");
                if (prefab == null) throw new InvalidOperationException("Authored settings view is missing.");
                view = Instantiate(prefab, transform, false);
                view.name = fromTitle ? "TitleSettingsView" : "MenuSettingsView";
                if (fromTitle) titleView = view; else menuView = view;
            }
            return view;
        }
        private void Build()
        {
            if (titleView != null) titleView.gameObject.SetActive(false);
            if (menuView != null) menuView.gameObject.SetActive(false);
            activeView = EnsureView(titleContext);
            overlay = activeView.gameObject; overlay.SetActive(true);
            surface = activeView.window; surface.gameObject.SetActive(true);
            SettingsPanelView.Click(activeView.Control<UnityEngine.UI.Button>("InputBlocker"), DismissTopLayer);
            SettingsPanelView.Click(activeView.Control<UnityEngine.UI.Button>("Close"), RequestClose);
            SettingsPanelView.Click(activeView.Control<UnityEngine.UI.Button>("Back"), RequestClose);
            SettingsPanelView.Click(activeView.Control<UnityEngine.UI.Button>("RestoreDefaults"), ConfirmDefaults);
            apply = activeView.Control<UnityEngine.UI.Button>("Apply");
            SettingsPanelView.Click(apply, () => Apply(false));
            dirtyLabel = activeView.Control<TMP_Text>("DirtyStatus");
            tabs.Clear(); tabs.AddRange(activeView.tabs);
            var visible = titleContext ? new[] { SettingsTab.General, SettingsTab.Display, SettingsTab.Sound, SettingsTab.Controls } : new[] { SettingsTab.General, SettingsTab.Sound, SettingsTab.Controls };
            for (int i = 0; i < visible.Length; i++)
            {
                var selected = visible[i]; SettingsPanelView.Click(tabs[i], () => SelectTab(selected));
            }
            SelectTab(currentTab);
        }
#if UNITY_EDITOR
        public void BuildSceneLayout()
        {
            if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            theme = Resources.Load<SettingsTheme>("SettingsTheme");
            titleContext = true; currentTab = SettingsTab.General;
            saved = SettingsDraft.Load(); draft = saved.Copy(); Build();
        }
#endif
        public void SelectTab(SettingsTab tab)
        {
            if (!titleContext && tab == SettingsTab.Display) return;
            CloseDropdown(); currentTab = tab;
            int page = titleContext ? (int)tab : tab == SettingsTab.General ? 0 : tab == SettingsTab.Sound ? 1 : 2;
            body = activeView.ShowPage(page);
            for (int i = 0; i < tabs.Count; i++)
            {
                var button = tabs[i];
                bool selected = i == page;
                var image = button.GetComponent<UnityEngine.UI.Image>();
                image.sprite = selected ? theme.activeButton : null;
                image.color = selected ? Color.white : Color.clear;
                button.GetComponentInChildren<TMP_Text>().color = selected ? theme.selectedTextColor : theme.textColor;
            }
            switch (tab)
            {
                case SettingsTab.General: BindGeneral(); break;
                case SettingsTab.Display: BindDisplay(); break;
                case SettingsTab.Sound: BindSound(); break;
                case SettingsTab.Controls: BindControls(); break;
            }
            RefreshDirty();
        }
        private T Control<T>(string name) where T : Component => SettingsPanelView.Control<T>(body, name);
        private void Click(string name, Action action) => SettingsPanelView.Click(Control<UnityEngine.UI.Button>(name), action);
        private void BindGeneral()
        {
            Control<UnityEngine.UI.Button>("LanguageAppearanceOnly").interactable = false;
            Choice("Speed", new[] { "보통", "빠름" }, draft.speed, i => { draft.speed = i; RefreshDirty(); });
            for (int i = 0; i < 3; i++)
            {
                int index = i; var button = Control<UnityEngine.UI.Button>("TextSize" + i);
                SelectButton(button, draft.textSize == i);
                SettingsPanelView.Click(button, () => { draft.textSize = index; SelectTab(currentTab); });
            }
            Control<TMP_Text>("TextSizePreview").fontSize = theme.previewTextSizes[Mathf.Clamp(draft.textSize, 0, 2)];
        }
        private void BindDisplay()
        {
            int mode = draft.mode == FullScreenMode.ExclusiveFullScreen ? 0 : draft.mode == FullScreenMode.FullScreenWindow ? 1 : 2;
            Choice("ScreenMode", new[] { "전체 화면", "테두리 없는 전체 화면", "창 모드" }, mode, i => { draft.mode = i == 0 ? FullScreenMode.ExclusiveFullScreen : i == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; RefreshDirty(); });
            var resolutions = GraphicsSettingsManager.GetSupportedResolutions(); var current = new Vector2Int(draft.width, draft.height);
            if (!resolutions.Contains(current)) resolutions.Add(current);
            var options = new string[resolutions.Count]; for (int i = 0; i < options.Length; i++) options[i] = resolutions[i].x + " × " + resolutions[i].y;
            Choice("Resolution", options, resolutions.IndexOf(current), i => { draft.width = resolutions[i].x; draft.height = resolutions[i].y; RefreshDirty(); });
            Toggle("VSync", draft.vSync, v => { draft.vSync = v; SelectTab(currentTab); });
            Choice("FrameRate", new[] { "30", "60", "120", "144", "제한 없음" }, draft.frameRate, i => { draft.frameRate = i; RefreshDirty(); }).interactable = !draft.vSync;
            Choice("Quality", new[] { "낮음", "중간", "높음" }, draft.quality, i => { draft.quality = i; RefreshDirty(); });
            Slider("Brightness", draft.brightness, v => { draft.brightness = v; RefreshDirty(); });
        }
        private void BindSound()
        {
            float[] values = { draft.master, draft.bgm, draft.effects }; bool[] mutes = { draft.masterMuted, draft.bgmMuted, draft.effectsMuted };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Slider("Volume" + i, values[i], v => { if (index == 0) draft.master = v; else if (index == 1) draft.bgm = v; else draft.effects = v; RefreshDirty(); });
                Toggle("Mute" + i, mutes[i], v => { if (index == 0) draft.masterMuted = v; else if (index == 1) draft.bgmMuted = v; else draft.effectsMuted = v; RefreshDirty(); });
            }
            Toggle("BackgroundSound", draft.backgroundSound, v => { draft.backgroundSound = v; RefreshDirty(); });
        }
        private void BindControls()
        {
            for (int i = 0; i < SettingsBindings.Labels.Length; i++)
            {
                int index = i; var button = Control<UnityEngine.UI.Button>("Binding" + i);
                button.GetComponentInChildren<TMP_Text>().text = SettingsBindings.Display(draft.bindings[i]);
                SettingsPanelView.Click(button, () => BeginBinding(index));
            }
        }
        private void SelectButton(UnityEngine.UI.Button button, bool selected)
        {
            button.GetComponent<UnityEngine.UI.Image>().sprite = selected ? theme.activeButton : theme.idleButton;
            button.GetComponentInChildren<TMP_Text>().color = selected ? theme.selectedTextColor : theme.textColor;
        }
        private void RefreshDirty()
        {
            if (apply == null) return;
            apply.interactable = HasChanges;
            apply.GetComponent<UnityEngine.UI.Image>().sprite = HasChanges ? theme.activeButton : theme.idleButton;
            apply.GetComponent<UnityEngine.UI.Image>().color = HasChanges ? Color.white : theme.disabledButtonTint;
            apply.GetComponentInChildren<TMP_Text>().color = HasChanges ? Color.white : theme.disabledTextColor;
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
            if (overlay != null) { overlay.SetActive(false); overlay = null; }
            blockedThroughFrame = Time.frameCount;
            draft = saved = null; closeAfterApply = false;
            GameInput.SetGameplayBlocked(false);
            RestoreBackgroundNavigation();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        public static void ConfirmQuit(Action quit)
        {
            EnsureInstance(); if (IsOpen) return;
            instance.activeView = instance.EnsureView(true);
            instance.overlay = instance.activeView.gameObject; instance.overlay.SetActive(true);
            instance.activeView.window.gameObject.SetActive(false);
            GameInput.SetGameplayBlocked(true); instance.openedFrame = Time.frameCount;
            instance.BlockBackgroundNavigation();
            instance.Dialog("게임을 종료할까요?", "", new[] { "예", "아니오" }, new Action[] { quit, instance.CloseImmediately }, instance.CloseImmediately);
        }
        private void Dialog(string heading, string description, string[] labels, Action[] actions, Action escape)
        {
            CloseDropdown(); CloseModal();
            modal = Instantiate(activeView.confirmationTemplates[labels.Length - 1], overlay.transform, false);
            modal.name = "Confirmation"; modal.SetActive(true);
            SettingsPanelView.Control<TMP_Text>(modal.transform, "DialogHeading").text = heading;
            countdown = SettingsPanelView.Control<TMP_Text>(modal.transform, "DialogDescription"); countdown.text = description;
            SettingsPanelView.Click(SettingsPanelView.Control<UnityEngine.UI.Button>(modal.transform, "ModalBlocker"), DismissTopLayer);
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i; var button = SettingsPanelView.Control<UnityEngine.UI.Button>(modal.transform, "DialogAction" + i);
                button.GetComponentInChildren<TMP_Text>().text = labels[i]; SettingsPanelView.Click(button, () => actions[index]());
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

        private UnityEngine.UI.Button Choice(string name, string[] choices, int selected, Action<int> change)
        {
            var button = Control<UnityEngine.UI.Button>(name); var label = button.GetComponentInChildren<TMP_Text>();
            label.text = choices[Mathf.Clamp(selected, 0, choices.Length - 1)];
            SettingsPanelView.Click(button, () => OpenDropdown(button, choices, selected, value => { selected = value; label.text = choices[value]; change(value); }));
            return button;
        }
        private void OpenDropdown(UnityEngine.UI.Button owner, string[] choices, int selected, Action<int> change)
        {
            CloseDropdown(); dropdown = Instantiate(activeView.dropdownTemplate, overlay.transform, false); dropdown.name = "Dropdown"; dropdown.SetActive(true);
            SettingsPanelView.Click(SettingsPanelView.Control<UnityEngine.UI.Button>(dropdown.transform, "DismissDropdown"), CloseDropdown);
            var ownerRect = owner.GetComponent<RectTransform>(); var corners = new Vector3[4]; ownerRect.GetWorldCorners(corners);
            Vector3 position = overlay.transform.InverseTransformPoint(corners[0]); var root = (RectTransform)overlay.transform;
            float height = Mathf.Min(choices.Length * 44 + 8, 300); var list = SettingsPanelView.Control<UnityEngine.UI.ScrollRect>(dropdown.transform, "Options");
            var rect = (RectTransform)list.transform; rect.anchoredPosition = new Vector2(position.x - root.rect.xMin, -Mathf.Min(root.rect.yMax - position.y + 4, root.rect.height - height - 25));
            rect.sizeDelta = new Vector2(ownerRect.rect.width, height);
            list.viewport.sizeDelta = new Vector2(ownerRect.rect.width - 6, height - 6);
            list.content.sizeDelta = new Vector2(ownerRect.rect.width - 6, choices.Length * 44);
            var template = list.content.GetChild(0).gameObject; template.SetActive(false);
            for (int i = 0; i < choices.Length; i++)
            {
                int value = i; var option = Instantiate(template, list.content, false); option.name = "Option" + i; option.SetActive(true);
                var button = option.GetComponent<UnityEngine.UI.Button>();
                var image = option.GetComponent<UnityEngine.UI.Image>(); image.color = i == selected ? theme.selectedBackgroundColor : theme.dropdownBackgroundColor;
                var text = option.GetComponentInChildren<TMP_Text>(); text.text = choices[i]; text.color = i == selected ? theme.selectedTextColor : theme.textColor;
                SettingsPanelView.Control<UnityEngine.UI.Image>(option.transform, "SelectedCheck").gameObject.SetActive(i == selected);
                SettingsPanelView.Click(button, () => { CloseDropdown(); change(value); });
            }
            Canvas.ForceUpdateCanvases(); list.verticalNormalizedPosition = 1f - Mathf.Clamp01((selected - 2f) / Mathf.Max(1, choices.Length - 5));
        }
        private void CloseDropdown()
        {
            if (dropdown != null) { DisposeGenerated(dropdown); dropdown = null; }
        }
        private void Slider(string name, float value, Action<float> changed)
        {
            var slider = Control<UnityEngine.UI.Slider>(name); var number = Control<TMP_Text>(name + "Value");
            slider.onValueChanged.RemoveAllListeners(); slider.SetValueWithoutNotify(value); number.text = Mathf.RoundToInt(value * 100).ToString();
            slider.onValueChanged.AddListener(v => { number.text = Mathf.RoundToInt(v * 100).ToString(); changed(v); });
        }
        private void Toggle(string name, bool value, Action<bool> changed)
        {
            var button = Control<UnityEngine.UI.Button>(name); var face = SettingsPanelView.Control<UnityEngine.UI.Image>(button.transform, "ToggleFace");
            face.sprite = value ? theme.toggleOn : theme.toggleOff;
            SettingsPanelView.Click(button, () => { value = !value; face.sprite = value ? theme.toggleOn : theme.toggleOff; changed(value); });
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
