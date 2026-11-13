#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YutArena.UI;

namespace YutArena.EditorTools
{
    /// <summary>Play-mode regression checks; restores all preferences, including originally absent keys.</summary>
    public static class SettingsUIVerification
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play 모드에서 실행해야 합니다.");
            var original = SettingsDraft.Load();
            bool hadOverrides = PlayerPrefs.HasKey(YutArena.Common.GameInput.OverridesKey);
            string originalOverrides = PlayerPrefs.GetString(YutArena.Common.GameInput.OverridesKey);
            string[] ints = { "PRESENTATION_SPEED", "UI_TEXT_SIZE", "SCREEN_MODE", "RESOLUTION_WIDTH", "RESOLUTION_HEIGHT", "VSYNC_ENABLED", "MAX_FRAME_RATE", "GRAPHICS_QUALITY", "MASTER_MUTED", "BGM_MUTED", "CLICK_MUTED", "BACKGROUND_SOUND_ENABLED" };
            string[] floats = { "BRIGHTNESS", "MASTER_VOLUME", "BGM_VOLUME", "CLICK_VOLUME" };
            var absent = new List<string>();
            foreach (string key in ints.Concat(floats).Concat(Enumerable.Range(0, 8).Select(i => "SETTINGS_BINDING_" + i)))
                if (!PlayerPrefs.HasKey(key)) absent.Add(key);
            int passed = 0;
            Action<bool, string> check = (condition, message) => { if (!condition) throw new Exception("FAIL: " + message); passed++; };
            var panel = UnityEngine.Object.FindFirstObjectByType<SettingsPanel>();
            try
            {
                panel.CloseImmediately(); SettingsPanel.Show(true);
                panel.SelectTab(SettingsTab.Display);
                int modeIndex = original.mode == FullScreenMode.Windowed ? 1 : 2;
                string modeCaption = modeIndex == 1 ? "테두리 없는 전체 화면" : "창 모드";
                CheckChoice(panel, "ScreenMode", modeIndex, modeCaption, check);
                check(panel.Draft.mode == (modeIndex == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed), "screen choice updates draft");
                Click(panel, "Resolution");
                string resolutionCaption = Buttons(panel).First(b => b.name == "Option0").GetComponentInChildren<TMPro.TMP_Text>().text;
                Click(panel, "Option0");
                CheckChoice(panel, "Resolution", 0, resolutionCaption, check);
                check(resolutionCaption == panel.Draft.width + " × " + panel.Draft.height, "resolution choice updates draft");
                check(SettingsDraft.Load().SameAs(original), "dropdown choices remain unapplied");
                Click(panel, "InputBlocker");
                check(SettingsPanel.IsOpen && Field("modal").GetValue(panel) != null, "outside dirty settings opens close confirmation");
                Click(panel, "ModalBlocker");
                check(SettingsPanel.IsOpen && panel.HasChanges && Field("modal").GetValue(panel) == null, "outside confirmation continues settings");
                Click(panel, "InputBlocker"); Click(panel, "DialogAction1");
                check(!SettingsPanel.IsOpen && SettingsDraft.Load().SameAs(original), "outside discard preserves saved values");
                SettingsPanel.Show(true);
                // Raycast geometry is checked on the following rendered frame via the CLI.
                Click(panel, "InputBlocker");
                check(!SettingsPanel.IsOpen, "outside clean window closes");
                panel.CloseImmediately(); SettingsPanel.Show(true);
                check(!panel.HasChanges, "open loads saved values");
                check(SettingsPanel.IsBlockingInput && !YutArena.Common.LocalSelectionInput.SharedInputAvailable, "modal blocks gameplay input");
                check(!Buttons(panel).First(b => b.name == "Apply").interactable, "apply starts disabled");
                int editedSpeed = (original.speed + 1) % 2;
                panel.Draft.speed = editedSpeed; panel.SelectTab(SettingsTab.Sound);
                panel.Draft.master = .37f; panel.SelectTab(SettingsTab.General);
                check(panel.Draft.speed == editedSpeed && Mathf.Approximately(panel.Draft.master, .37f), "tab switching retains drafts");
                check(SettingsDraft.Load().SameAs(original), "editing does not save");
                panel.SelectTab(SettingsTab.Sound); panel.ConfirmDefaults();
                Escape(panel);
                check(Mathf.Approximately(panel.Draft.master, .37f), "defaults cancel preserves draft");
                panel.ConfirmDefaults(); Click(panel, "DialogAction1");
                check(panel.Draft.master == 1 && panel.Draft.speed == editedSpeed, "defaults affect only current tab");
                check(panel.HasChanges && SettingsDraft.Load().SameAs(original), "defaults wait for apply");
                panel.RequestClose(); Escape(panel);
                check(SettingsPanel.IsOpen && panel.HasChanges, "close escape continues editing");
                panel.RequestClose(); Click(panel, "DialogAction1");
                check(!SettingsPanel.IsOpen && SettingsDraft.Load().SameAs(original), "discard closes without saving");

                SettingsPanel.Show(true); panel.Draft.speed = editedSpeed;
                panel.Apply(false);
                check(SettingsPanel.IsOpen && !panel.HasChanges && SettingsDraft.Load().speed == editedSpeed, "normal apply saves and keeps open");
                panel.Draft.master = .37f;
                panel.Draft.mode = panel.Draft.mode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                panel.Apply(true);
                check(panel.IsPreviewing && SettingsPanel.IsOpen, "display apply previews before close");
                check(SettingsDraft.Load().mode != panel.Draft.mode, "preview is not persisted");
                Escape(panel);
                check(!panel.IsPreviewing && SettingsPanel.IsOpen && panel.Draft.master == .37f, "preview escape preserves other draft and remains open");
                check(panel.Draft.mode == SettingsDraft.Load().mode, "preview restores display draft");
                panel.Draft.mode = panel.Draft.mode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                panel.Apply(false);
                Field("deadline").SetValue(panel, -1f); Invoke(panel, "Update");
                check(!panel.IsPreviewing && SettingsPanel.IsOpen && panel.Draft.master == .37f, "display timeout restores and retains other edits");
                panel.SelectTab(SettingsTab.Controls);
                Invoke(panel, "BeginBinding", 0); Invoke(panel, "CaptureBinding", "<Keyboard>/q");
                Escape(panel);
                check(panel.Draft.bindings[0] == original.bindings[0], "binding cancel retains old key");
                Invoke(panel, "BeginBinding", 0); Invoke(panel, "CaptureBinding", "<Keyboard>/q"); Click(panel, "DialogAction0");
                check(panel.Draft.bindings[0] == "<Keyboard>/q", "binding confirmation updates draft");
                panel.RequestClose(); Click(panel, "DialogAction2");
                check(!SettingsPanel.IsOpen && SettingsDraft.Load().master == .37f, "apply-and-close commits and closes");

                SettingsPanel.Show(true);
                panel.Draft.mode = panel.Draft.mode == FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                panel.Apply(true); panel.KeepDisplay();
                check(!SettingsPanel.IsOpen, "display keep completes pending close");
                SettingsPanel.Show(false);
                check(Buttons(panel).Count(b => b.name.StartsWith("Tab")) == 3, "ingame has three tabs");
                panel.SelectTab(SettingsTab.Display);
                check(panel.CurrentTab == SettingsTab.General, "display unavailable ingame");
                check(!Buttons(panel).First(b => b.name == "LanguageAppearanceOnly").interactable, "language is appearance only");
                panel.CloseImmediately();
                bool quit = false; SettingsPanel.ConfirmQuit(() => quit = true); Escape(panel);
                check(!SettingsPanel.IsOpen && !quit, "quit escape cancels");
                SettingsPanel.ConfirmQuit(() => quit = true); Click(panel, "DialogAction0");
                check(quit, "quit yes invokes exit action");
                return "PASS: " + passed + " settings flow checks";
            }
            finally
            {
                panel.CloseImmediately(); original.Save();
                foreach (string key in absent) PlayerPrefs.DeleteKey(key);
                if (hadOverrides) PlayerPrefs.SetString(YutArena.Common.GameInput.OverridesKey, originalOverrides);
                else PlayerPrefs.DeleteKey(YutArena.Common.GameInput.OverridesKey);
                PlayerPrefs.Save();
                SettingsPanel.Show(true);
            }
        }
        private static BindingFlags Flags => BindingFlags.Instance | BindingFlags.NonPublic;
        private static FieldInfo Field(string name) => typeof(SettingsPanel).GetField(name, Flags);
        private static void Invoke(SettingsPanel panel, string name, params object[] args) => typeof(SettingsPanel).GetMethod(name, Flags).Invoke(panel, args);
        private static void Escape(SettingsPanel panel) => ((Action)Field("escapeAction").GetValue(panel))();
        private static UnityEngine.UI.Button[] Buttons(SettingsPanel panel) => panel.GetComponentsInChildren<UnityEngine.UI.Button>(false);
        private static void Click(SettingsPanel panel, string name) => Buttons(panel).First(b => b.name == name).onClick.Invoke();
        private static void CheckChoice(SettingsPanel panel, string name, int value, string caption, Action<bool, string> check)
        {
            Click(panel, name); Click(panel, "Option" + value);
            var owner = Buttons(panel).First(b => b.name == name);
            check(owner.GetComponentInChildren<TMPro.TMP_Text>().text == caption, name + " caption matches selection");
            owner.onClick.Invoke();
            var selected = owner.GetComponentInParent<SettingsPanel>().GetComponentsInChildren<UnityEngine.UI.Image>().First(i => i.name == "SelectedCheck");
            check(selected.transform.parent.name == "Option" + value, name + " reopened dropdown marks current choice");
            Click(panel, "DismissDropdown");
            check(SettingsPanel.IsOpen && Field("dropdown").GetValue(panel) == null, name + " outside dropdown closes list only");
        }
        private static GameObject RaycastAt(SettingsPanel panel, float x, float y)
        {
            Canvas.ForceUpdateCanvases();
            var overlay = ((GameObject)Field("overlay").GetValue(panel)).GetComponent<RectTransform>();
            Vector3 world = overlay.TransformPoint(new Vector3(overlay.rect.xMin + x, overlay.rect.yMax - y));
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, world)
            };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0) throw new Exception("No UI hit at " + x + ", " + y);
            return hits[0].gameObject;
        }
        private static void ClickAt(SettingsPanel panel, float x, float y)
        {
            var target = RaycastAt(panel, x, y);
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(target,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
    }
}
#endif
