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
        private static readonly Color Ink = new Color32(31, 46, 76, 255);
        private static readonly Color Blue = new Color32(37, 70, 132, 255);
        [SerializeField] private InGameSettingsTheme theme;
        [SerializeField, HideInInspector] private GameObject overlay;
        private GameObject modal;
        private RectTransform window, body;
        private readonly List<UnityEngine.UI.Button> tabButtons = new List<UnityEngine.UI.Button>();
        private UnityEngine.UI.Button optionButton;
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
            if (instance == null) new GameObject("InGameSettingsPanel", typeof(RectTransform)).AddComponent<InGameSettingsPanel>();
        }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            theme = Resources.Load<InGameSettingsTheme>("InGameSettingsTheme");
            ConfigureCanvas();
            if (overlay != null) overlay.SetActive(false);
            // Directly running InGameScene in the Editor must also have a working audio backend.
            if (AudioManager.Instance == null && FindFirstObjectByType<AudioManager>() == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();
        }
        private void ConfigureCanvas()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2100;
            var scaler = gameObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
#if UNITY_EDITOR
        public void BuildSceneLayout()
        {
            if (Application.isPlaying) return;
            theme = Resources.Load<InGameSettingsTheme>("InGameSettingsTheme");
            ConfigureCanvas();
            if (overlay != null) SettingsPanel.DisposeGenerated(overlay);
            bindings = SettingsBindings.Load(); Build(); SelectTab(0);
        }
#endif
        private void Start()
        {
            // Bind the existing HUD button; never create a second settings launcher.
            foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (button.name == "OptionBtn" && button.gameObject.scene == gameObject.scene)
                {
                    optionButton = button;
                    bool connected = false;
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        connected |= button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == nameof(Open);
                    if (!connected) optionButton.onClick.AddListener(Open);
                    break;
                }
            if (optionButton == null) Debug.LogWarning("InGameSettingsPanel: OptionBtn을 찾을 수 없습니다. ESC로 설정을 열 수 있습니다.");
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
            if (theme == null || theme.font == null || theme.layers.Length != 18)
            { Debug.LogError("인게임 설정 리소스가 없습니다. Tools/Yut Arena/인게임 설정 UI 리소스 연결을 실행하세요."); return; }
            InitializeMissingValues();
            bindings = SettingsBindings.Load();
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            eventSystem = EventSystem.current;
            if (eventSystem != null) { navigationEnabled = eventSystem.sendNavigationEvents; eventSystem.sendNavigationEvents = false; eventSystem.SetSelectedGameObject(null); }
            GameInput.SetGameplayBlocked(true);
            if (overlay != null) SettingsPanel.DisposeGenerated(overlay);
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
            overlay = Rect(transform, "InGameSettingsOverlay", 0, 0, 1920, 1080).gameObject;
            Stretch((RectTransform)overlay.transform);
            var scrim = Image(overlay.transform, "GameInputBlocker", 0, 0, 1920, 1080, new Color(.2f, .25f, .4f, .32f), true);
            Stretch(scrim.rectTransform);
            window = Rect(overlay.transform, "Window", 0, 0, 1264, 820);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f);
            window.anchoredPosition = Vector2.zero; window.localScale = Vector3.one * .9f;
            Art(window, "WindowArt", 0, 0, 0, 1264, 820);
            Image(window, "WindowInputShield", 65, 45, 1134, 710, Color.clear, true);
            Text(window, "Title", "인게임 설정", 125, 62, 850, 53, 39);
            var close = Button(window, "Close", "", 1125, 63, 48, 48, Close);
            Art(close.transform, "CloseIcon", 1, 13, 13, 22, 22);
            string[] labels = { "일반", "사운드", "조작" };
            tabButtons.Clear();
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                tabButtons.Add(Button(window, "Tab" + i, labels[i], 125 + i * 338, 145, 335, 54, () => SelectTab(index)));
            }
            body = Rect(window, "Body", 125, 225, 1014, 435);
            Button(window, "Back", "돌아가기", 125, 700, 170, 53, Close, 3);
            Button(window, "Defaults", "기본값 복원", 940, 700, 200, 53, RestoreDefaults, 3);
        }
        public void SelectTab(int index)
        {
            if (modal != null || index < 0 || index > 2) return;
            CancelListening(); tab = index; Clear(body);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                var art = tabButtons[i].GetComponent<UnityEngine.UI.RawImage>();
                art.texture = i == tab ? theme.layers[2] : null; art.color = i == tab ? Color.white : Color.clear;
                tabButtons[i].GetComponentInChildren<TMP_Text>().color = i == tab ? Color.white : Ink;
            }
            if (tab == 0) BuildGeneral(); else if (tab == 1) BuildSound(); else BuildControls();
            openedFrame = Time.frameCount;
        }
        private void BuildGeneral()
        {
            Text(body, "TextSizeCaption", "글자 크기", 0, 25, 300, 48, 29);
            string[] sizes = { "작게", "보통", "크게" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Button(body, "TextSize" + i, sizes[i], 620 + i * 130, 20, 120, 57,
                    () => { GeneralSettingsManager.Instance.SetTextSize(index); SelectTab(0); }, GeneralSettingsManager.TextSizeIndex == i ? 6 : 5);
            }
            Art(body, "PreviewArt", 4, 0, 140, 1014, 250);
            Text(body, "PreviewHeading", "미리보기", 35, 183, 900, 40, 24);
            Text(body, "TextSizePreview", "선택한 말의 정보를 확인합니다.", 35, 257, 945, 75,
                GeneralSettingsManager.TextSizeIndex == 0 ? 27 : GeneralSettingsManager.TextSizeIndex == 1 ? 33 : 39);
        }
        private void BuildSound()
        {
            string[] labels = { "전체 음량", "배경 음악", "효과음" };
            float[] values = { AudioManager.LoadMasterVolume(), AudioManager.LoadBgmVolume(), AudioManager.LoadClickVolume() };
            bool[] muted = { AudioManager.LoadMasterMuted(), AudioManager.LoadBgmMuted(), AudioManager.LoadClickMuted() };
            var commonTheme = Resources.Load<SettingsTheme>("SettingsTheme");
            for (int i = 0; i < 3; i++)
            {
                int index = i; float y = 60 + i * 120;
                Text(body, "VolumeLabel" + i, labels[i], 0, y - 12, 210, 50, 29);
                var number = Text(body, "VolumeNumber" + i, Mathf.RoundToInt(values[i] * 100).ToString(), 790, y - 12, 65, 50, 29, TextAlignmentOptions.Center);
                var sliderRoot = Rect(body, "Volume" + i, 215, y - 18, 550, 60);
                Image(sliderRoot, "PointerArea", 0, 0, 550, 60, Color.clear, true);
                Art(sliderRoot, "Track", 8, 12, 25, 526, 10);
                var fillClip = Rect(sliderRoot, "FillArea", 12, 25, 526, 10);
                var fill = Art(fillClip, "Fill", 9, 0, 0, 526, 10);
                Stretch(fill.rectTransform);
                var handleArea = Rect(sliderRoot, "HandleArea", 12, 30, 526, 0);
                var thumb = Art(handleArea, "Handle", 10, 0, 0, 28, 28);
                thumb.rectTransform.pivot = new Vector2(.5f, .5f);
                var slider = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
                slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
                slider.fillRect = fill.rectTransform; slider.handleRect = thumb.rectTransform;
                slider.targetGraphic = thumb; slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
                slider.SetValueWithoutNotify(Mathf.RoundToInt(values[i] * 100));
                slider.onValueChanged.AddListener(v => { number.text = Mathf.RoundToInt(v).ToString(); SetVolume(index, v); });
                bool isMuted = muted[i];
                var muteButton = Button(body, "Mute" + i, "", 869, y - 12, 145, 50, () => {});
                var face = Image(muteButton.transform, "ToggleFace", 0, 14, 44, 22, Color.white);
                face.sprite = isMuted ? commonTheme.toggleOn : commonTheme.toggleOff;
                Text(muteButton.transform, "Caption", "음소거", 55, 0, 90, 50, 23);
                muteButton.onClick.AddListener(() => {
                    isMuted = !isMuted; SetMuted(index, isMuted);
                    face.sprite = isMuted ? commonTheme.toggleOn : commonTheme.toggleOff;
                });
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
        private void BuildControls()
        {
            hint = Text(body, "ControlsHint", "입력 버튼을 누른 뒤 새 키를 누르세요", 0, -15, 1014, 42, 23, TextAlignmentOptions.Center);
            Art(body, "Table", 11, 0, 40, 1014, 400);
            Text(body, "ActionHeader", "동작", 0, 43, 350, 35, 23, TextAlignmentOptions.Center);
            Text(body, "PrimaryHeader", "기본 입력", 350, 43, 330, 35, 23, TextAlignmentOptions.Center);
            Text(body, "SecondaryHeader", "보조 입력", 680, 43, 334, 35, 23, TextAlignmentOptions.Center);
            for (int i = 0; i < 8; i++)
            {
                int index = i; float y = 82 + 45 * i;
                Text(body, "Action" + i, SettingsBindings.Labels[i], 16, y, 338, 37, 22);
                Button(body, "Binding" + i, SettingsBindings.Display(bindings[i]), 370, y, 290, 37, () => BeginBinding(index), 12, 21);
                Text(body, "Secondary" + i, SettingsBindings.Secondary[i], 700, y, 300, 37, 21, TextAlignmentOptions.Center);
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
            var button = body.Find("Binding" + index).GetComponent<UnityEngine.UI.Button>();
            button.GetComponent<UnityEngine.UI.RawImage>().texture = theme.layers[13];
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
            CancelListening();
            modal = Rect(overlay.transform, "Confirmation", 0, 0, 1920, 1080).gameObject;
            Stretch((RectTransform)modal.transform);
            var blocker = Image(modal.transform, "ModalBlocker", 0, 0, 1920, 1080, new Color(.08f, .12f, .22f, .62f), true);
            Stretch(blocker.rectTransform);
            var frame = Rect(modal.transform, "Dialog", 0, 0, 794, 444);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero;
            Art(frame, "Frame", 15, 0, 0, 794, 444);
            Image(frame, "DialogInputShield", 60, 40, 674, 332, Color.clear, true);
            Text(frame, "Heading", title, 85, 75, 624, 50, 31, TextAlignmentOptions.Center);
            if (before != null)
            {
                Image(frame, "OldKeyBackground", 148, 157, 249, 110, new Color32(247, 244, 234, 255));
                Image(frame, "NewKeyBackground", 397, 157, 249, 110, Ink);
                Text(frame, "OldKeyCaption", "기존 입력", 148, 167, 249, 30, 20, TextAlignmentOptions.Center);
                Text(frame, "NewKeyCaption", "새 입력", 397, 167, 249, 30, 20, TextAlignmentOptions.Center).color = Color.white;
                Text(frame, "OldKey", before, 153, 207, 239, 48, 33, TextAlignmentOptions.Center);
                Text(frame, "NewKey", after, 402, 207, 239, 48, 33, TextAlignmentOptions.Center).color = Color.white;
                Text(frame, "Arrow", "→", 377, 193, 40, 45, 25, TextAlignmentOptions.Center).color = new Color32(190, 160, 108, 255);
            }
            Button(frame, "Cancel", "취소", 225, 309, 148, 53, () => { CloseModal(); SelectTab(tab); }, 16, 24);
            Button(frame, "Confirm", "확인", 421, 309, 148, 53, () => accept(), 17, 24).GetComponentInChildren<TMP_Text>().color = Color.white;
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
                listeningLabel.text = SettingsBindings.Display(bindings[listening]); listeningLabel.color = Ink;
                listeningLabel.GetComponentInParent<UnityEngine.UI.RawImage>().texture = theme.layers[12];
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
            if (overlay != null) { SettingsPanel.DisposeGenerated(overlay); overlay = null; }
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
        private UnityEngine.UI.Button Button(Transform parent, string name, string label, float x, float y, float width, float height, Action click, int layer = -1, float size = 28)
        {
            var raw = Art(parent, name, layer, x, y, width, height); raw.raycastTarget = true;
            if (layer < 0) raw.color = Color.clear;
            var button = raw.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = raw;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(() => click());
            var text = Text(raw.transform, "Label", label, 5, 0, width - 10, height, size, TextAlignmentOptions.Center);
            if (layer == 6) text.color = Color.white;
            return button;
        }
        private TMP_Text Text(Transform parent, string name, string content, float x, float y, float width, float height, float size, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var label = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = theme.font; label.text = content; label.fontSize = size; label.color = Ink;
            label.alignment = align; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
        private UnityEngine.UI.RawImage Art(Transform parent, string name, int layer, float x, float y, float width, float height)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = layer >= 0 ? theme.layers[layer] : null; image.raycastTarget = false; return image;
        }
        private static UnityEngine.UI.Image Image(Transform parent, string name, float x, float y, float width, float height, Color color, bool raycast = false)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = raycast; return image;
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) SettingsPanel.DisposeGenerated(parent.GetChild(i).gameObject);
        }
    }
}
