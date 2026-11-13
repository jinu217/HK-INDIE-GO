#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YutArena.Common;
using YutArena.UI;

namespace YutArena.EditorTools
{
    /// <summary>Queues virtual-device events in the player input update, then checks real action/rebind behavior.</summary>
    public sealed class GameInputVerification : MonoBehaviour
    {
        public const string ReportKey = "YutArena.InputActionVerification";
        private const string RunOnPlayKey = "YutArena.RunInputActionVerification";
        private readonly List<Action> pending = new List<Action>();
        private readonly List<Action> restorePrefs = new List<Action>();
        private readonly List<InputDevice> virtualDevices = new List<InputDevice>();
        private readonly List<UnityEngine.EventSystems.BaseInputModule> modules = new List<UnityEngine.EventSystems.BaseInputModule>();
        private UnityEngine.EventSystems.EventSystem testEventSystem;
        private bool previousNavigation;
        private SettingsDraft original;
        private SettingsPanel panel;
        private Keyboard keyboard, previousKeyboard;
        private Mouse mouse, previousMouse;
        private Gamepad padA, padB, previousGamepad;
        private InputSettings.EditorInputBehaviorInPlayMode oldRouting;
        private InputSettings.BackgroundBehavior oldBackground;
        private bool cleaned, configured;
        private int passed;

        public static string Begin()
        {
            if (!Application.isPlaying)
            {
                SessionState.SetBool(RunOnPlayKey, true);
                SessionState.SetString(ReportKey, "starting Play verification");
                EditorApplication.isPlaying = true;
                return "starting Play verification";
            }
            if (FindFirstObjectByType<GameInputVerification>() != null) throw new InvalidOperationException("이미 입력 검증 중입니다.");
            SessionState.SetString(ReportKey, "running");
            var root = new GameObject("InputActionVerification");
            var runner = root.AddComponent<GameInputVerification>();
            if (runner == null) throw new InvalidOperationException("입력 검증 러너를 만들지 못했습니다.");
            return "started: " + root.GetInstanceID();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginRequestedVerification()
        {
            if (!SessionState.GetBool(RunOnPlayKey, false)) return;
            SessionState.SetBool(RunOnPlayKey, false);
            Begin();
        }
        private void Awake() => DontDestroyOnLoad(gameObject);
        private IEnumerator Start()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "StartScene" || YutArena.Managers.AudioManager.Instance == null)
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                ActiveEditorTracker.sharedTracker.isLocked = false;
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
                yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("StartScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            yield return null;
            var checks = RunChecks();
            while (true)
            {
                bool next = false; Exception failure = null;
                try { next = checks.MoveNext(); } catch (Exception error) { failure = error; }
                if (failure != null || !next)
                {
                    try { (checks as IDisposable)?.Dispose(); Cleanup(); }
                    catch (Exception error) { failure = failure ?? error; }
                    SessionState.SetString(ReportKey, failure == null ? "PASS: " + passed + " input action checks" : "FAIL: " + failure);
                    Destroy(gameObject); yield break;
                }
                yield return checks.Current;
            }
        }
        private IEnumerator RunChecks()
        {
            original = SettingsDraft.Load(); CapturePreferences();
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current; previousGamepad = Gamepad.current;
            oldRouting = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            configured = true;
            testEventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (testEventSystem != null)
            {
                previousNavigation = testEventSystem.sendNavigationEvents;
                testEventSystem.sendNavigationEvents = false;
                testEventSystem.SetSelectedGameObject(null);
            }
            foreach (var module in FindObjectsByType<UnityEngine.EventSystems.BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (module.enabled) { modules.Add(module); module.enabled = false; }
            keyboard = InputSystem.AddDevice<Keyboard>(); virtualDevices.Add(keyboard);
            mouse = InputSystem.AddDevice<Mouse>(); virtualDevices.Add(mouse);
            padA = InputSystem.AddDevice<Gamepad>(); virtualDevices.Add(padA);
            padB = InputSystem.AddDevice<Gamepad>(); virtualDevices.Add(padB);
            InputSystem.onBeforeUpdate += BeforeInput;
            panel = FindFirstObjectByType<SettingsPanel>(); panel.CloseImmediately();
            GameInput.SaveBindings(GameInput.DefaultPaths());
            yield return null;

            Queue(keyboard, new KeyboardState(Key.Space)); yield return null;
            Check(GameInput.Pressed(GameAction.Throw), "default Space triggers Throw action");
            yield return null;
            Check(!GameInput.Pressed(GameAction.Throw), "held button does not repeat WasPressedThisFrame");
            Queue(keyboard, new KeyboardState()); yield return null;
            Queue(keyboard, new KeyboardState(Key.F)); yield return null;
            Check(GameInput.Pressed(GameAction.UseSkill), "default F triggers UseSkill action");
            Queue(keyboard, new KeyboardState()); yield return null;
            Queue(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
            Check(GameInput.Pressed(GameAction.SelectPiece) && GameInput.Pressed(GameAction.SelectDestination) && GameInput.Pressed(GameAction.SelectSkillTarget), "selection actions share their contextual pointer binding");
            Queue(mouse, new MouseState()); yield return null;

            Queue(padA, new GamepadState(GamepadButton.South)); yield return null;
            Check(GameInput.Pressed(GameAction.Throw, padA), "assigned gamepad triggers Throw");
            Check(!GameInput.Pressed(GameAction.Throw, padB), "other gamepad does not receive the press");
            Check(!GameInput.Pressed(GameAction.Throw), "gamepad does not leak into keyboard action group");
            Check(GameInput.UiPressed(UiAction.Submit, padA) && !GameInput.UiPressed(UiAction.Submit, padB), "UI submit is isolated per player device");
            Queue(padA, new GamepadState()); yield return null;
            Queue(padB, new GamepadState(GamepadButton.South)); yield return null;
            Check(GameInput.Pressed(GameAction.Throw, padB) && !GameInput.Pressed(GameAction.Throw, padA), "second player gamepad isolation");
            Queue(padB, new GamepadState()); yield return null;
            Queue(padA, new GamepadState(GamepadButton.DpadRight)); yield return null;
            Check(GameInput.UiPressed(UiAction.Right, padA) && !GameInput.UiPressed(UiAction.Right, padB), "gamepad UI navigation uses actions");
            Queue(padA, new GamepadState()); yield return null;
            InputSystem.RemoveDevice(padB); virtualDevices.Remove(padB);
            padB = InputSystem.AddDevice<Gamepad>(); virtualDevices.Add(padB);
            yield return null;
            Queue(padB, new GamepadState(GamepadButton.South)); yield return null;
            Check(GameInput.Pressed(GameAction.Throw, padB), "hot-plugged gamepad is prepared before first press");
            Queue(padB, new GamepadState()); yield return null;

            SettingsPanel.Show(true); yield return null;
            Queue(keyboard, new KeyboardState(Key.Space)); yield return null;
            Check(!GameInput.Pressed(GameAction.Throw), "settings blocks gameplay action");
            var live = (InputActionAsset)typeof(GameInput).GetField("runtime", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Check(!live.FindActionMap("Gameplay").enabled && live.FindActionMap("UI").enabled, "settings disables Gameplay map and leaves UI enabled");
            Queue(keyboard, new KeyboardState()); yield return null;
            Queue(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
            Check(GameInput.UiPressed(UiAction.Click) && !GameInput.Pressed(GameAction.SelectPiece), "UI input remains available while game input is blocked");
            Queue(mouse, new MouseState()); yield return null;

            panel.SelectTab(SettingsTab.Controls); Invoke("BeginBinding", 0); yield return null;
            Check(Field("rebindOperation") is InputActionRebindingExtensions.RebindingOperation, "settings starts Unity interactive rebinding");
            Queue(padA, new GamepadState(GamepadButton.South)); yield return null;
            Check(Field("rebindOperation") != null && Field("pendingBindingPath") == null, "keyboard binding does not capture gamepad buttons");
            Queue(padA, new GamepadState()); yield return null;
            Queue(keyboard, new KeyboardState(Key.Escape)); yield return null;
            Check(SettingsPanel.IsOpen && Field("modal") == null && panel.Draft.bindings[0] == "<Keyboard>/space", "ESC cancels rebind without closing settings or changing draft");
            Queue(keyboard, new KeyboardState()); yield return null;

            Invoke("BeginBinding", 0); yield return null;
            Queue(keyboard, new KeyboardState(Key.F)); yield return null;
            Queue(keyboard, new KeyboardState()); yield return null;
            Check(Field("rebindOperation") != null && panel.Draft.bindings[0] == "<Keyboard>/space", "duplicate key restarts rebinding without changing draft");
            string beforeRebind = PlayerPrefs.GetString(GameInput.OverridesKey);
            Queue(keyboard, new KeyboardState(Key.G)); yield return null;
            Check(panel.Draft.bindings[0] == "<Keyboard>/space", "captured key waits for release and confirmation");
            Queue(keyboard, new KeyboardState()); yield return null;
            Check(Field("rebindOperation") == null && Field("modal") != null, "valid captured key opens confirmation");
            Click("DialogAction0");
            Check(panel.Draft.bindings[0] == "<Keyboard>/g", "Unity rebind confirmation updates draft");
            Check(SettingsBindings.Load()[0] == "<Keyboard>/space" && PlayerPrefs.GetString(GameInput.OverridesKey) == beforeRebind, "unapplied rebind does not alter saved or live binding");
            panel.SelectTab(SettingsTab.Sound); panel.SelectTab(SettingsTab.Controls);
            Check(panel.Draft.bindings[0] == "<Keyboard>/g", "rebind draft survives tab changes");
            panel.Apply(false);
            Check(SettingsPanel.IsOpen && !panel.HasChanges && SettingsBindings.Load()[0] == "<Keyboard>/g", "Apply saves action override and clears pending changes");
            Check(PlayerPrefs.HasKey(GameInput.OverridesKey), "binding overrides are saved as JSON");
            GameInput.ReloadBindings();
            Check(SettingsBindings.Load()[0] == "<Keyboard>/g", "binding override survives runtime reload");
            panel.CloseImmediately(); yield return null;
            Queue(keyboard, new KeyboardState(Key.Space)); yield return null;
            Check(!GameInput.Pressed(GameAction.Throw), "old key no longer triggers rebound action");
            Queue(keyboard, new KeyboardState()); yield return null;
            Queue(keyboard, new KeyboardState(Key.G)); yield return null;
            Check(GameInput.Pressed(GameAction.Throw), "new key triggers rebound action");
            Queue(keyboard, new KeyboardState()); yield return null;
            Queue(padA, new GamepadState(GamepadButton.South)); yield return null;
            Check(GameInput.Pressed(GameAction.Throw, padA), "keyboard rebind preserves gamepad binding");
            Queue(padA, new GamepadState()); yield return null;

            SettingsPanel.Show(true); panel.SelectTab(SettingsTab.Controls); Invoke("BeginBinding", 1); yield return null;
            var dialog = ((GameObject)Field("modal")).transform.Find("Dialog").GetComponent<RectTransform>();
            Vector2 dialogCenter = RectTransformUtility.WorldToScreenPoint(null, dialog.TransformPoint(dialog.rect.center));
            Queue(mouse, new MouseState { position = dialogCenter }.WithButton(MouseButton.Middle)); yield return null;
            Queue(mouse, new MouseState { position = dialogCenter }); yield return null;
            var mouseStatus = "operation=" + (Field("rebindOperation") != null) + ", pending=" + Field("pendingBindingPath") +
                ", held=" + mouse.middleButton.isPressed + ", message=" + (Field("countdown") as TMPro.TMP_Text)?.text;
            Click("DialogAction0");
            Check(panel.Draft.bindings[1] == "<Mouse>/middleButton", "Unity interactive rebinding captures mouse buttons; actual=" + panel.Draft.bindings[1] + "; " + mouseStatus);
            panel.RequestClose(); Click("DialogAction1");
            Check(SettingsBindings.Load()[1] == "<Mouse>/leftButton", "discard cancels mouse binding draft");

            PlayerPrefs.DeleteKey(GameInput.OverridesKey); PlayerPrefs.SetString("SETTINGS_BINDING_0", "<Keyboard>/h"); GameInput.ReloadBindings();
            Check(SettingsBindings.Load()[0] == "<Keyboard>/h" && PlayerPrefs.HasKey(GameInput.OverridesKey), "legacy saved key migrates to binding override JSON");
            PlayerPrefs.SetString("SETTINGS_BINDING_0", "<Keyboard>/j"); GameInput.ReloadBindings();
            Check(SettingsBindings.Load()[0] == "<Keyboard>/h", "new JSON takes priority over retained legacy values");
            SettingsPanel.Show(true); panel.SelectTab(SettingsTab.Controls); panel.ConfirmDefaults(); Click("DialogAction1");
            Check(panel.HasChanges && panel.Draft.bindings.SequenceEqual(GameInput.DefaultPaths()) && SettingsBindings.Load()[0] == "<Keyboard>/h", "restore defaults remains unapplied");
            panel.Apply(false); GameInput.ReloadBindings();
            Check(SettingsBindings.Load().SequenceEqual(GameInput.DefaultPaths()), "applied defaults remove overrides and do not resurrect legacy keys");
            panel.CloseImmediately(); yield return null;
            SettingsPanel.Show(true); yield return null;
            Queue(keyboard, new KeyboardState(Key.Escape)); yield return null;
            Check(!SettingsPanel.IsOpen, "UI Cancel action closes unchanged settings");
            Queue(keyboard, new KeyboardState()); yield return null;
        }
        private void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            passed++; SessionState.SetString(ReportKey, "running: " + passed + " / " + message);
        }
        private void Queue<TState>(InputDevice device, TState state) where TState : struct, IInputStateTypeInfo
            => pending.Add(() => InputSystem.QueueStateEvent(device, state));
        private void BeforeInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || pending.Count == 0) return;
            var work = pending.ToArray(); pending.Clear(); foreach (var action in work) action();
        }
        private object Field(string name) => typeof(SettingsPanel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
        private void Invoke(string name, params object[] args) => typeof(SettingsPanel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, args);
        private void Click(string name) => panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == name).onClick.Invoke();
        private void CapturePreferences()
        {
            string[] ints = { "PRESENTATION_SPEED", "UI_TEXT_SIZE", "SCREEN_MODE", "RESOLUTION_WIDTH", "RESOLUTION_HEIGHT", "VSYNC_ENABLED", "MAX_FRAME_RATE", "GRAPHICS_QUALITY", "MASTER_MUTED", "BGM_MUTED", "CLICK_MUTED", "BACKGROUND_SOUND_ENABLED" };
            string[] floats = { "BRIGHTNESS", "MASTER_VOLUME", "BGM_VOLUME", "CLICK_VOLUME" };
            foreach (string key in ints)
            {
                bool had = PlayerPrefs.HasKey(key); int value = PlayerPrefs.GetInt(key);
                restorePrefs.Add(() => { if (had) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key); });
            }
            foreach (string key in floats)
            {
                bool had = PlayerPrefs.HasKey(key); float value = PlayerPrefs.GetFloat(key);
                restorePrefs.Add(() => { if (had) PlayerPrefs.SetFloat(key, value); else PlayerPrefs.DeleteKey(key); });
            }
            foreach (string key in Enumerable.Range(0, 8).Select(i => "SETTINGS_BINDING_" + i).Concat(new[] { GameInput.OverridesKey }))
            {
                bool had = PlayerPrefs.HasKey(key); string value = PlayerPrefs.GetString(key);
                restorePrefs.Add(() => { if (had) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); });
            }
        }
        private void Cleanup()
        {
            if (cleaned) return; cleaned = true;
            InputSystem.onBeforeUpdate -= BeforeInput; pending.Clear();
            if (panel != null) panel.CloseImmediately();
            try
            {
                if (Application.isPlaying && original != null && YutArena.Managers.AudioManager.Instance != null &&
                    YutArena.Managers.GeneralSettingsManager.Instance != null && YutArena.Managers.GraphicsSettingsManager.Instance != null) original.Save();
            }
            finally
            {
                foreach (var restore in restorePrefs) restore(); PlayerPrefs.Save();
                foreach (var device in virtualDevices) if (device.added) InputSystem.RemoveDevice(device);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent();
                if (configured)
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = oldRouting;
                    InputSystem.settings.backgroundBehavior = oldBackground;
                }
                foreach (var module in modules) if (module != null) module.enabled = true;
                if (testEventSystem != null) testEventSystem.sendNavigationEvents = previousNavigation;
                GameInput.ReloadBindings();
            }
        }
        private void OnDestroy()
        {
            if (!cleaned) SessionState.SetString(ReportKey, "CANCELLED: verification scene was unloaded");
            Cleanup();
        }
    }
}
#endif
