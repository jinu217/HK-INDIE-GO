using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.UI
{
    /// <summary>Match-only settings. Confirmed changes are saved immediately; the match clock keeps running.</summary>
    [DefaultExecutionOrder(-950)]
    public sealed class InGameSettingsPanel : MonoBehaviour
    {
        private static InGameSettingsPanel instance;
        private static int blockedThroughFrame = -1;
        public static bool IsOpen => instance != null && instance.overlay != null && instance.overlay.activeSelf;
        public static bool IsBlockingInput => IsOpen || Time.frameCount <= blockedThroughFrame;
        [SerializeField] private InGameSettingsTheme theme;
        [SerializeField, HideInInspector] private GameObject overlay;
        private GameObject modal;
        [SerializeField] private SettingsPanelView view;
        private RectTransform window, body;
        private readonly List<UnityEngine.UI.Button> tabButtons = new List<UnityEngine.UI.Button>();
        [SerializeField] private UnityEngine.UI.Button optionButton;
        private TMP_Text hint;
        private int tab, openedFrame, listening = -1;
        private string[] bindings;
        private InputActionAsset bindingDraft;
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private string pendingPath;
        private ButtonControl pendingControl;
        private TMP_Text listeningLabel;
        private GameObject previousSelection;
        private EventSystem eventSystem;
        private bool navigationEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; blockedThroughFrame = -1; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            EnsureForScene();
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForScene();
        private static void EnsureForScene()
        {
            if (SceneManager.GetActiveScene().name != "InGameScene" || instance != null) return;
            instance = FindFirstObjectByType<InGameSettingsPanel>(FindObjectsInactive.Include);
            if (instance == null)
            {
                var prefab = Resources.Load<GameObject>("UI/Settings/InGameSettingsPanelRoot");
                if (prefab == null) throw new InvalidOperationException("InGameSettingsPanelRoot prefab is missing.");
                instance = Instantiate(prefab).GetComponent<InGameSettingsPanel>();
            }
        }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            theme = Resources.Load<InGameSettingsTheme>("InGameSettingsTheme");
            if (overlay != null) overlay.SetActive(false);
            // Directly running InGameScene in the Editor must also have a working audio backend.
            if (AudioManager.Instance == null && FindFirstObjectByType<AudioManager>() == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();
        }
#if UNITY_EDITOR
        public void BuildSceneLayout()
        {
            if (Application.isPlaying) return;
            theme = Resources.Load<InGameSettingsTheme>("InGameSettingsTheme");
            bindings = SettingsBindings.Load(); Build(); SelectTab(0);
        }
#endif
        private void Start()
        {
            if (optionButton == null)
            {
                Debug.LogWarning("InGameSettingsPanel: 설정 열기 버튼이 연결되지 않았습니다.", this);
                return;
            }
            bool connected = false;
            for (int i = 0; i < optionButton.onClick.GetPersistentEventCount(); i++)
                connected |= optionButton.onClick.GetPersistentTarget(i) == this && optionButton.onClick.GetPersistentMethodName(i) == nameof(Open);
            if (!connected) optionButton.onClick.AddListener(Open);
        }

        private void Update()
        {
            if (Time.frameCount <= openedFrame || Time.frameCount <= blockedThroughFrame) return;
            if (!IsOpen)
            {
                if (!SettingsPanel.IsOpen && GameInput.UiPressed(UiAction.Cancel)) Open();
                return;
            }
            if (pendingPath != null && (pendingControl == null || !pendingControl.isPressed))
            {
                string path = pendingPath; pendingPath = null; pendingControl = null;
                ShowBindingConfirmation(path); return;
            }
            if (GameInput.UiPressed(UiAction.Cancel)) DismissTop();
        }
        public void Open()
        {
            if (IsOpen || SettingsPanel.IsOpen) return;
            if (theme == null || theme.font == null || theme.selectedTab == null || theme.bindingIdle == null || theme.bindingListening == null)
            { Debug.LogError("인게임 설정 리소스가 없습니다. Tools/Yut Arena/인게임 설정 UI 리소스 연결을 실행하세요."); return; }
            InitializeMissingValues();
            bindings = SettingsBindings.Load();
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            eventSystem = EventSystem.current;
            if (eventSystem != null) { navigationEnabled = eventSystem.sendNavigationEvents; eventSystem.sendNavigationEvents = false; eventSystem.SetSelectedGameObject(null); }
            GameInput.SetGameplayBlocked(true);
            Build(); SelectTab(0); openedFrame = Time.frameCount;
        }
        private static void InitializeMissingValues()
        {
            if (!PlayerPrefs.HasKey("UI_TEXT_SIZE")) GeneralSettingsManager.Instance?.SetTextSize(1);
            if (!PlayerPrefs.HasKey(AudioManager.MasterVolumeKey)) SetVolume(0, 50);
            if (!PlayerPrefs.HasKey(AudioManager.BgmVolumeKey)) SetVolume(1, 50);
            if (!PlayerPrefs.HasKey(AudioManager.ClickVolumeKey)) SetVolume(2, 50);
        }
        private void Build()
        {
            if (view == null)
            {
                var prefab = Resources.Load<SettingsPanelView>("UI/Settings/InGameSettingsView");
                if (prefab == null) throw new InvalidOperationException("InGameSettingsView prefab is missing.");
                view = Instantiate(prefab, transform, false); view.name = "InGameSettingsView";
            }
            overlay = view.gameObject; overlay.SetActive(true); window = view.window;
            SettingsPanelView.Click(view.Control<UnityEngine.UI.Button>("Close"), Close);
            SettingsPanelView.Click(view.Control<UnityEngine.UI.Button>("Back"), Close);
            SettingsPanelView.Click(view.Control<UnityEngine.UI.Button>("Defaults"), RestoreDefaults);
            tabButtons.Clear(); tabButtons.AddRange(view.tabs);
            for (int i = 0; i < tabButtons.Count; i++) { int index = i; SettingsPanelView.Click(tabButtons[i], () => SelectTab(index)); }
        }
        public void SelectTab(int index)
        {
            if (modal != null || index < 0 || index > 2) return;
            CancelListening(); tab = index; body = view.ShowPage(index);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                var art = tabButtons[i].GetComponent<UnityEngine.UI.RawImage>();
                art.texture = i == tab ? theme.selectedTab : null; art.color = i == tab ? Color.white : Color.clear;
                tabButtons[i].GetComponentInChildren<TMP_Text>().color = i == tab ? theme.selectedTextColor : theme.textColor;
            }
            if (tab == 0) BindGeneral(); else if (tab == 1) BindSound(); else BindControls();
            openedFrame = Time.frameCount;
        }
        private T Control<T>(string name) where T : Component => SettingsPanelView.Control<T>(body, name);
        private void BindGeneral()
        {
            for (int i = 0; i < 3; i++)
            {
                int index = i; var button = Control<UnityEngine.UI.Button>("TextSize" + i); bool selected = GeneralSettingsManager.TextSizeIndex == i;
                button.GetComponent<UnityEngine.UI.RawImage>().texture = (selected ? theme.sizeSelected : theme.sizeIdle);
                button.GetComponentInChildren<TMP_Text>().color = selected ? theme.selectedTextColor : theme.textColor;
                SettingsPanelView.Click(button, () => { GeneralSettingsManager.Instance.SetTextSize(index); SelectTab(0); });
            }
            Control<TMP_Text>("TextSizePreview").fontSize = theme.previewTextSizes[Mathf.Clamp(GeneralSettingsManager.TextSizeIndex, 0, 2)];
        }
        private void BindSound()
        {
            float[] values = { AudioManager.LoadMasterVolume(), AudioManager.LoadBgmVolume(), AudioManager.LoadClickVolume() };
            bool[] muted = { AudioManager.LoadMasterMuted(), AudioManager.LoadBgmMuted(), AudioManager.LoadClickMuted() };
            var commonTheme = Resources.Load<SettingsTheme>("SettingsTheme");
            for (int i = 0; i < 3; i++)
            {
                int index = i; var number = Control<TMP_Text>("VolumeNumber" + i); var slider = Control<UnityEngine.UI.Slider>("Volume" + i);
                slider.onValueChanged.RemoveAllListeners(); slider.SetValueWithoutNotify(Mathf.RoundToInt(values[i] * 100)); number.text = Mathf.RoundToInt(values[i] * 100).ToString();
                slider.onValueChanged.AddListener(v => { number.text = Mathf.RoundToInt(v).ToString(); SetVolume(index, v); });
                bool isMuted = muted[i]; var button = Control<UnityEngine.UI.Button>("Mute" + i); var face = SettingsPanelView.Control<UnityEngine.UI.Image>(button.transform, "ToggleFace");
                face.sprite = isMuted ? commonTheme.toggleOn : commonTheme.toggleOff;
                SettingsPanelView.Click(button, () => { isMuted = !isMuted; SetMuted(index, isMuted); face.sprite = isMuted ? commonTheme.toggleOn : commonTheme.toggleOff; });
            }
        }
        private static void SetVolume(int channel, float value)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            if (channel == 0) audio.SetMasterVolume(value / 100f);
            else if (channel == 1) audio.SetBgmVolume(value / 100f);
            else audio.SetClickVolume(value / 100f);
        }
        private static void SetMuted(int channel, bool muted)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            if (channel == 0) audio.SetMasterMuted(muted);
            else if (channel == 1) audio.SetBgmMuted(muted);
            else audio.SetClickMuted(muted);
        }
        private void BindControls()
        {
            hint = Control<TMP_Text>("ControlsHint"); hint.text = "입력 버튼을 누른 뒤 새 키를 누르세요";
            for (int i = 0; i < 8; i++)
            {
                int index = i; var button = Control<UnityEngine.UI.Button>("Binding" + i);
                button.GetComponent<UnityEngine.UI.RawImage>().texture = theme.bindingIdle;
                button.GetComponentInChildren<TMP_Text>().text = SettingsBindings.Display(bindings[i]);
                SettingsPanelView.Click(button, () => BeginBinding(index));
            }
        }
        public void RestoreDefaults()
        {
            CancelListening();
            if (tab == 0) { GeneralSettingsManager.Instance.SetTextSize(1); SelectTab(0); }
            else if (tab == 1) { for (int i = 0; i < 3; i++) { SetVolume(i, 50); SetMuted(i, false); } SelectTab(1); }
            else ShowDialog("기본값으로 되돌릴까요?", () => {
                bindings = SettingsBindings.Defaults(); SettingsBindings.Save(bindings); CloseModal(); SelectTab(2);
            });
        }
        public void BeginBinding(int index)
        {
            if (modal != null || index < 0 || index >= 8) return;
            CancelListening(); listening = index;
            var button = Control<UnityEngine.UI.Button>("Binding" + index);
            button.GetComponent<UnityEngine.UI.RawImage>().texture = theme.bindingListening;
            listeningLabel = button.GetComponentInChildren<TMP_Text>(); listeningLabel.text = "입력 대기"; listeningLabel.color = Color.white;
            hint.text = "새 키를 누르세요 · ESC로 취소";
            bindingDraft = GameInput.CreateBindingDraft(bindings);
            var action = GameInput.FindGameAction(bindingDraft, (GameAction)index);
            var positions = new Dictionary<Mouse, Vector2>();
            rebind = action.PerformInteractiveRebinding(0).WithExpectedControlType<ButtonControl>()
                .WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape").WithMatchingEventsBeingSuppressed(false)
                .OnComputeScore((control, inputEvent) => {
                    if (control.device is Mouse source)
                    {
                        if (!source.position.ReadValueFromEvent(inputEvent, out Vector2 position)) position = source.position.ReadValue();
                        positions[source] = position;
                    }
                    return control.synthetic ? 1 : 2;
                })
                .OnPotentialMatch(operation => {
                    if (operation.selectedControl.device is Mouse source && operation.selectedControl == source.leftButton &&
                        PointerOverButton(positions.TryGetValue(source, out Vector2 position) ? position : source.position.ReadValue()))
                    { operation.RemoveCandidate(operation.selectedControl); return; }
                    operation.Complete();
                })
                .OnComplete(operation => {
                    pendingPath = action.bindings[0].effectivePath; pendingControl = operation.selectedControl as ButtonControl;
                    StopRebinding(); openedFrame = Time.frameCount;
                })
                .OnCancel(operation => CancelListening()).Start();
            openedFrame = Time.frameCount;
        }
        private bool PointerOverButton(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null;
        }
        private void ShowBindingConfirmation(string path)
        {
            int index = listening;
            if (index < 0) return;
            for (int i = 0; i < bindings.Length; i++)
                if (i != index && bindings[i] == path && !(SelectionBinding(i) && SelectionBinding(index)))
                {
                    BeginBinding(index); hint.text = "이미 '" + SettingsBindings.Labels[i] + "'에 사용 중입니다. 다른 키를 누르세요."; return;
                }
            string before = SettingsBindings.Display(bindings[index]);
            ShowDialog(SettingsBindings.Labels[index] + " 단축키 변경", () => {
                bindings[index] = path; SettingsBindings.Save(bindings); CloseModal(); SelectTab(2);
            }, before, SettingsBindings.Display(path));
        }
        private static bool SelectionBinding(int index) => index == 1 || index == 2 || index == 4;
        private void ShowDialog(string title, Action accept, string before = null, string after = null)
        {
            CancelListening(); CloseModal();
            modal = Instantiate(view.confirmationTemplates[before == null ? 0 : 1], overlay.transform, false); modal.name = "Confirmation"; modal.SetActive(true);
            SettingsPanelView.Control<TMP_Text>(modal.transform, "Heading").text = title;
            if (before != null)
            {
                SettingsPanelView.Control<TMP_Text>(modal.transform, "OldKey").text = before;
                SettingsPanelView.Control<TMP_Text>(modal.transform, "NewKey").text = after;
            }
            SettingsPanelView.Click(SettingsPanelView.Control<UnityEngine.UI.Button>(modal.transform, "Cancel"), () => { CloseModal(); SelectTab(tab); });
            SettingsPanelView.Click(SettingsPanelView.Control<UnityEngine.UI.Button>(modal.transform, "Confirm"), accept);
            openedFrame = Time.frameCount;
        }
        public void DismissTop()
        {
            if (modal != null) { CloseModal(); SelectTab(tab); }
            else if (listening >= 0 || pendingPath != null) CancelListening();
            else Close();
            openedFrame = Time.frameCount;
        }
        private void StopRebinding()
        {
            var operation = rebind; rebind = null;
            if (operation != null)
            {
                operation.OnCancel(null).OnComplete(null);
                if (!operation.completed && !operation.canceled) operation.Cancel();
                operation.Dispose();
            }
            GameInput.Release(bindingDraft); bindingDraft = null;
        }
        private void CancelListening()
        {
            StopRebinding(); pendingPath = null; pendingControl = null;
            if (listeningLabel != null && listening >= 0 && bindings != null)
            {
                listeningLabel.text = SettingsBindings.Display(bindings[listening]); listeningLabel.color = theme.textColor;
                listeningLabel.GetComponentInParent<UnityEngine.UI.RawImage>().texture = theme.bindingIdle;
            }
            listening = -1; listeningLabel = null;
            if (hint != null) hint.text = "입력 버튼을 누른 뒤 새 키를 누르세요";
            openedFrame = Time.frameCount;
        }
        private void CloseModal()
        {
            if (modal != null) { SettingsPanel.DisposeGenerated(modal); modal = null; }
            openedFrame = Time.frameCount;
        }
        public void Close()
        {
            CancelListening(); CloseModal();
            if (overlay != null) overlay.SetActive(false);
            blockedThroughFrame = Time.frameCount;
            GameInput.SetGameplayBlocked(SettingsPanel.IsOpen);
            if (eventSystem != null) { eventSystem.sendNavigationEvents = navigationEnabled; eventSystem.SetSelectedGameObject(previousSelection); }
            eventSystem = null; previousSelection = null;
        }
        private void OnDestroy()
        {
            if (optionButton != null) optionButton.onClick.RemoveListener(Open);
            if (instance != this) return;
            Close(); instance = null;
        }
    }
}
