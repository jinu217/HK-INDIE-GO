#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YutArena.Common;
using YutArena.Managers;
using YutArena.UI;

namespace YutArena.EditorTools
{
    /// <summary>Play-mode integration checks against the actual in-game UI and native InputAction events.</summary>
    public sealed class InGameSettingsVerification : MonoBehaviour
    {
        public const string ReportKey = "YutArena.InGameSettingsVerification";
        private readonly List<Action> restore = new List<Action>();
        private readonly List<Action> pending = new List<Action>();
        private readonly List<BaseInputModule> modules = new List<BaseInputModule>();
        private InGameSettingsPanel panel;
        private Keyboard keyboard, oldKeyboard;
        private Gamepad pad, oldPad;
        private InputSettings.EditorInputBehaviorInPlayMode oldRouting;
        private InputSettings.BackgroundBehavior oldBackground;
        private bool cleaned, configured;
        private int passed;
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("InGameScene Play 모드에서 실행해 주세요.");
            if (FindFirstObjectByType<InGameSettingsVerification>() != null) throw new InvalidOperationException("검증 중입니다.");
            SessionState.SetString(ReportKey, "running");
            new GameObject("InGameSettingsVerification").AddComponent<InGameSettingsVerification>();
        }
        private IEnumerator Start()
        {
            var checks = Checks();
            while (true)
            {
                bool next = false; Exception failure = null;
                try { next = checks.MoveNext(); } catch (Exception error) { failure = error; }
                if (!next || failure != null)
                {
                    try { (checks as IDisposable)?.Dispose(); Cleanup(); } catch (Exception error) { failure = failure ?? error; }
                    SessionState.SetString(ReportKey, failure == null ? "PASS: " + passed + " in-game settings checks" : "FAIL after " + passed + ": " + failure);
                    Destroy(gameObject); yield break;
                }
                yield return checks.Current;
            }
        }
        private IEnumerator Checks()
        {
            panel = FindFirstObjectByType<InGameSettingsPanel>();
            Capture(); panel.Close(); yield return null;
            oldKeyboard = Keyboard.current; oldPad = Gamepad.current;
            oldRouting = InputSystem.settings.editorInputBehaviorInPlayMode; oldBackground = InputSystem.settings.backgroundBehavior; configured = true;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (var module in FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None))
                if (module.enabled) { modules.Add(module); module.enabled = false; }
            keyboard = InputSystem.AddDevice<Keyboard>("InGameSettingsTestKeyboard");
            pad = InputSystem.AddDevice<Gamepad>("InGameSettingsTestPad");
            InputSystem.onBeforeUpdate += BeforeInput;
            Queue(new KeyboardState()); yield return null;
            var option = FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b => b.name == "OptionBtn");
            option.onClick.Invoke(); yield return null;
            Check(InGameSettingsPanel.IsOpen && SettingsPanel.IsBlockingInput, "OptionBtn opens separate blocking in-game panel");
            Check(!SettingsPanel.IsOpen && panel.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("Tab")) == 3, "title popup remains closed; exactly three tabs");
            float time = Time.time; int frame = Time.frameCount;
            var turn = FindFirstObjectByType<TestTurnManager>();
            var remainingField = typeof(TestTurnManager).GetField("turnTimerRemainingSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
            float remaining = (float)remainingField.GetValue(turn);
            yield return new WaitForSecondsRealtime(.2f);
            Check(Time.timeScale == 1 && Time.time > time && Time.frameCount > frame, "game time advances while settings is open");
            Check((float)remainingField.GetValue(turn) < remaining, "actual turn countdown continues behind popup");
            Queue(new KeyboardState(Key.Space, Key.F)); QueuePad(new GamepadState(GamepadButton.South)); yield return null;
            Check(!GameInput.Pressed(GameAction.Throw) && !GameInput.Pressed(GameAction.UseSkill) && !GameInput.Pressed(GameAction.Throw, pad), "keyboard and gamepad gameplay actions are blocked");
            Check(!LocalSelectionInput.SharedInputAvailable && LocalSelectionInput.SharedDeviceAvailable, "popup blocks game input without pretending devices disconnected");
            Queue(new KeyboardState()); QueuePad(new GamepadState()); yield return null;
            Click("TextSize2"); yield return null;
            Check(GeneralSettingsManager.TextSizeIndex == 2 && Label("TextSizePreview").fontSize == 39, "large text selection updates preview and saves immediately");
            panel.RestoreDefaults(); yield return null;
            Check(GeneralSettingsManager.TextSizeIndex == 1 && Field("modal") == null && Label("TextSizePreview").fontSize == 33, "general defaults restore normal preview without modal");
            panel.SelectTab(1); yield return null;
            var sliders = panel.GetComponentsInChildren<UnityEngine.UI.Slider>();
            Check(sliders.Length == 3 && panel.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("Mute")) == 3, "sound view contains three sliders and matching mute controls");
            sliders[0].value = 80; sliders[1].value = 60; sliders[2].value = 75;
            Check(Label("VolumeNumber0").text == "80" && Mathf.Approximately(AudioManager.LoadMasterVolume(), .8f), "slider number and saved volume update together");
            Check(Mathf.Approximately(sliders[0].fillRect.anchorMax.x, .8f) && Mathf.Approximately(sliders[0].handleRect.anchorMin.x, .8f), "slider fill and handle match numeric value");
            // Start from a known state, then exercise each independent toggle and persistence.
            panel.RestoreDefaults(); yield return null;
            Click("Mute0"); Click("Mute1"); Click("Mute2");
            Check(AudioManager.LoadMasterMuted() && AudioManager.LoadBgmMuted() && AudioManager.LoadClickMuted(), "all mute controls save immediately to shared audio settings");
            panel.GetComponentsInChildren<UnityEngine.UI.Slider>().First(s=>s.name=="Volume0").value=70;
            Check(AudioManager.LoadMasterMuted() && Mathf.Approximately(AudioManager.LoadMasterVolume(),.7f), "adjusting a muted slider preserves its mute state and stored volume");
            panel.SelectTab(0); panel.SelectTab(1); Click("Mute1");
            Check(AudioManager.LoadMasterMuted() && !AudioManager.LoadBgmMuted() && AudioManager.LoadClickMuted(), "mute state survives tab changes and channels toggle independently");
            panel.RestoreDefaults(); yield return null;
            Check(panel.GetComponentsInChildren<UnityEngine.UI.Slider>().All(s=>s.value==50) && Field("modal") == null, "sound defaults immediately restore 50/50/50");
            Check(!AudioManager.LoadMasterMuted() && !AudioManager.LoadBgmMuted() && !AudioManager.LoadClickMuted(), "sound defaults also turn off all mute controls");
            panel.SelectTab(0); panel.SelectTab(1);
            Check(panel.GetComponentsInChildren<UnityEngine.UI.Slider>().All(s=>s.value==50), "tab switches retain saved sound changes");
            panel.SelectTab(2); yield return null;
            Check(panel.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("Binding")) == 8, "controls show eight selectable primary inputs");
            SettingsBindings.Save(SettingsBindings.Defaults()); panel.Close(); yield return null; panel.Open(); panel.SelectTab(2); yield return null;
            panel.BeginBinding(0); yield return null;
            Check(Label("Binding0", true).text == "입력 대기" && Field("modal") == null, "rebind waits inline without a dialog");
            Queue(new KeyboardState(Key.Escape)); yield return null;
            Check(InGameSettingsPanel.IsOpen && (int)Field("listening") == -1 && Field("modal") == null, "ESC cancels waiting without closing settings");
            Queue(new KeyboardState()); yield return null;
            panel.BeginBinding(0); yield return null;
            Queue(new KeyboardState(Key.G)); yield return null;
            Check(Field("modal") == null, "captured key release is required before confirmation");
            Queue(new KeyboardState()); yield return null; yield return null;
            Check(Field("modal") != null && Label("OldKey").text == "Space" && Label("NewKey").text == "G", "native rebinding opens existing-to-new comparison");
            Check(SettingsBindings.Load()[0] == "<Keyboard>/space", "candidate is not saved before confirmation");
            Queue(new KeyboardState(Key.Escape)); yield return null;
            Check(Field("modal") == null && InGameSettingsPanel.IsOpen && SettingsBindings.Load()[0] == "<Keyboard>/space", "ESC cancels comparison and preserves previous key");
            Queue(new KeyboardState()); yield return null;
            panel.BeginBinding(0); yield return null;
            Queue(new KeyboardState(Key.G)); yield return null; Queue(new KeyboardState()); yield return null; yield return null;
            Click("Confirm"); yield return null;
            Check(SettingsBindings.Load()[0] == "<Keyboard>/g" && Label("Binding0",true).text == "G", "confirmation saves rebind and updates its cell");
            panel.Close(); yield return null; panel.Open(); panel.SelectTab(2); yield return null;
            Check(Label("Binding0",true).text == "G", "rebind persists across closing and reopening");
            panel.RestoreDefaults(); yield return null;
            Click("Cancel"); yield return null;
            Check(SettingsBindings.Load()[0] == "<Keyboard>/g" && Field("modal") == null, "cancel controls defaults preserves current binding");
            panel.RestoreDefaults(); yield return null;
            Click("Confirm"); yield return null;
            Check(SettingsBindings.Load()[0] == "<Keyboard>/space" && Label("Binding0",true).text == "Space", "confirm controls defaults restores primary action bindings");
            Queue(new KeyboardState(Key.Escape)); yield return null;
            Check(!InGameSettingsPanel.IsOpen, "ESC closes popup directly without unsaved confirmation");
            Queue(new KeyboardState()); yield return null;
            Check(!SettingsPanel.IsBlockingInput && LocalSelectionInput.SharedInputAvailable, "closing restores gameplay availability");
            Queue(new KeyboardState(Key.Escape)); yield return null;
            Check(InGameSettingsPanel.IsOpen, "ESC opens in-game settings from match");
            Queue(new KeyboardState()); yield return null;
            Click("Back"); yield return null;
            Check(!InGameSettingsPanel.IsOpen, "Back closes settings");
            panel.Open(); yield return null; Click("Close"); yield return null;
            Check(!InGameSettingsPanel.IsOpen, "X closes settings");
        }
        private void Check(bool value, string description)
        {
            if (!value) throw new Exception(description);
            passed++; SessionState.SetString(ReportKey, "running: " + passed + " " + description);
        }
        private object Field(string name) => typeof(InGameSettingsPanel).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
        private TMPro.TMP_Text Label(string name, bool child = false)
        {
            if (child) return panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name==name).GetComponentInChildren<TMPro.TMP_Text>();
            return panel.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name==name);
        }
        private void Click(string name) => panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name==name).onClick.Invoke();
        private void Queue(KeyboardState state) => pending.Add(()=>InputSystem.QueueStateEvent(keyboard,state));
        private void QueuePad(GamepadState state) => pending.Add(()=>InputSystem.QueueStateEvent(pad,state));
        private void BeforeInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            var work = pending.ToArray(); pending.Clear(); foreach (var action in work) action();
        }
        private void Capture()
        {
            foreach (string key in new[] { "UI_TEXT_SIZE", "MASTER_MUTED", "BGM_MUTED", "CLICK_MUTED" })
            {
                bool had=PlayerPrefs.HasKey(key); int value=PlayerPrefs.GetInt(key);
                restore.Add(()=>{if(had) PlayerPrefs.SetInt(key,value); else PlayerPrefs.DeleteKey(key);});
            }
            foreach (string key in new[] { AudioManager.MasterVolumeKey, AudioManager.BgmVolumeKey, AudioManager.ClickVolumeKey })
            {
                bool had=PlayerPrefs.HasKey(key); float value=PlayerPrefs.GetFloat(key);
                restore.Add(()=>{if(had) PlayerPrefs.SetFloat(key,value); else PlayerPrefs.DeleteKey(key);});
            }
            bool hadOverrides=PlayerPrefs.HasKey(GameInput.OverridesKey); string overrides=PlayerPrefs.GetString(GameInput.OverridesKey);
            restore.Add(()=>{if(hadOverrides) PlayerPrefs.SetString(GameInput.OverridesKey,overrides); else PlayerPrefs.DeleteKey(GameInput.OverridesKey);});
        }
        private void Cleanup()
        {
            if (cleaned) return; cleaned=true;
            InputSystem.onBeforeUpdate-=BeforeInput; pending.Clear();
            if(panel!=null) panel.Close();
            foreach(var action in restore) action(); PlayerPrefs.Save();
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if(pad!=null && pad.added) InputSystem.RemoveDevice(pad);
            if(oldKeyboard!=null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            if(oldPad!=null && oldPad.added) oldPad.MakeCurrent();
            if(configured) {InputSystem.settings.editorInputBehaviorInPlayMode=oldRouting;InputSystem.settings.backgroundBehavior=oldBackground;}
            foreach(var module in modules) if(module!=null) module.enabled=true;
            GeneralSettingsManager.Instance?.RefreshTextSizes();
            if(AudioManager.Instance!=null) AudioManager.Instance.SendMessage("ApplySavedVolumes");
            GameInput.ReloadBindings();
        }
        private void OnDestroy() { if(!cleaned) Cleanup(); }
    }
}
#endif
