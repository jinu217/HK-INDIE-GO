using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YutArena.Common
{
    public enum GameAction { Throw, SelectPiece, SelectDestination, UseSkill, SelectSkillTarget, Cancel, CharacterInfo, OpenChat }
    public enum UiAction { Cancel, Submit, Click, SecondaryClick, Point, Left, Right, Up, Down }

    /// <summary>Action-based input. Runtime clones keep imported assets and per-player devices isolated.</summary>
    public static class GameInput
    {
        public const string ResourceName = "YutArenaInput";
        public const string OverridesKey = "INPUT_BINDING_OVERRIDES_V1";
        public const string KeyboardMouseGroup = "KeyboardMouse";
        public const string GamepadGroup = "Gamepad";
        private static InputActionAsset runtime;
        private static readonly Dictionary<InputDevice, InputActionAsset> deviceActions = new Dictionary<InputDevice, InputActionAsset>();
        private static bool gameplayBlocked, subscribed, quitting;
        private static int blockedThroughFrame = -1;

        public static string[] DefaultPaths()
        {
            var template = Resources.Load<InputActionAsset>(ResourceName);
            if (template != null)
            {
                var paths = new string[8];
                for (int i = 0; i < paths.Length; i++) paths[i] = FindGameAction(template, (GameAction)i).bindings[0].path;
                return paths;
            }
            // Seed values are only used by the Editor installer before the asset exists.
            return SeedDefaultPaths();
        }
        internal static string[] SeedDefaultPaths() => new[] { "<Keyboard>/space", "<Mouse>/leftButton", "<Mouse>/leftButton", "<Keyboard>/f",
            "<Mouse>/leftButton", "<Mouse>/rightButton", "<Keyboard>/f1", "<Keyboard>/enter" };
        public static InputAction FindGameAction(InputActionAsset asset, GameAction action) => asset.FindAction("Gameplay/" + action, true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            DisposeRuntime(); gameplayBlocked = false; quitting = false; blockedThroughFrame = -1;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => EnsureRuntime();

        private static InputActionAsset CloneTemplate()
        {
            var template = Resources.Load<InputActionAsset>(ResourceName);
            if (template == null) throw new InvalidOperationException("YutArenaInput 액션 파일이 없습니다. 입력 리소스를 먼저 연결해 주세요.");
            var clone = UnityEngine.Object.Instantiate(template);
            clone.name = ResourceName + " (Runtime)";
            return clone;
        }
        private static void ApplySaved(InputActionAsset asset)
        {
            if (PlayerPrefs.HasKey(OverridesKey))
            {
                try { asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(OverridesKey)); return; }
                catch (ArgumentException) { Debug.LogWarning("저장된 입력 설정을 읽지 못해 이전 키 설정을 사용합니다."); }
            }
            for (int i = 0; i < 8; i++)
            {
                string key = "SETTINGS_BINDING_" + i;
                if (PlayerPrefs.HasKey(key)) FindGameAction(asset, (GameAction)i).ApplyBindingOverride(0, PlayerPrefs.GetString(key));
            }
        }
        private static void EnsureRuntime()
        {
            if (runtime != null || quitting) return;
            runtime = CloneTemplate(); ApplySaved(runtime);
            runtime.FindActionMap("Gameplay", true).bindingMask = InputBinding.MaskByGroup(KeyboardMouseGroup);
            SetMapState(runtime);
            if (!subscribed)
            {
                InputSystem.onDeviceChange += OnDeviceChange;
                Application.quitting += OnQuitting;
                subscribed = true;
            }
            // Prepare bindings before the first button press, including hot-plugged controllers.
            if (Keyboard.current != null) ForDevice(Keyboard.current);
            if (Mouse.current != null) ForDevice(Mouse.current);
            foreach (Gamepad pad in Gamepad.all) ForDevice(pad);
            if (!PlayerPrefs.HasKey(OverridesKey))
            {
                bool legacy = false;
                for (int i = 0; i < 8; i++) legacy |= PlayerPrefs.HasKey("SETTINGS_BINDING_" + i);
                if (legacy) { PlayerPrefs.SetString(OverridesKey, runtime.SaveBindingOverridesAsJson()); PlayerPrefs.Save(); }
            }
        }
        private static void SetMapState(InputActionAsset asset)
        {
            asset.FindActionMap("UI", true).Enable();
            var gameplay = asset.FindActionMap("Gameplay", true);
            if (gameplayBlocked) gameplay.Disable(); else gameplay.Enable();
        }
        private static InputActionAsset ForDevice(InputDevice device)
        {
            EnsureRuntime();
            if (deviceActions.TryGetValue(device, out var cached)) return cached;
            var clone = CloneTemplate(); ApplySaved(clone);
            clone.devices = new[] { device };
            clone.bindingMask = InputBinding.MaskByGroup(device is Gamepad ? GamepadGroup : KeyboardMouseGroup);
            SetMapState(clone);
            deviceActions.Add(device, clone);
            return clone;
        }
        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
            {
                if (deviceActions.TryGetValue(device, out var cached)) { Release(cached); deviceActions.Remove(device); }
            }
            else if ((device is Gamepad || device is Keyboard || device is Mouse) &&
                     (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)) ForDevice(device);
        }
        public static bool Pressed(GameAction action, Gamepad gamepad = null)
        {
            EnsureRuntime();
            if (runtime == null || gameplayBlocked || Time.frameCount <= blockedThroughFrame) return false;
            return FindGameAction(gamepad != null ? ForDevice(gamepad) : runtime, action).WasPressedThisFrame();
        }
        public static bool UiPressed(UiAction action, InputDevice device = null)
        {
            EnsureRuntime();
            if (runtime == null) return false;
            return (device != null ? ForDevice(device) : runtime).FindAction("UI/" + action, true).WasPressedThisFrame();
        }
        public static Vector2 PointerPosition
        {
            get { EnsureRuntime(); return runtime != null ? runtime.FindAction("UI/Point", true).ReadValue<Vector2>() : Vector2.zero; }
        }
        public static void SetGameplayBlocked(bool blocked)
        {
            if (!Application.isPlaying || quitting) return;
            gameplayBlocked = blocked;
            if (!blocked) blockedThroughFrame = Time.frameCount;
            EnsureRuntime();
            if (runtime == null) return;
            SetMapState(runtime);
            foreach (var asset in deviceActions.Values) SetMapState(asset);
        }

        public static string[] LoadBindingPaths()
        {
            var asset = CloneTemplate();
            try
            {
                ApplySaved(asset);
                var result = new string[8];
                for (int i = 0; i < result.Length; i++) result[i] = FindGameAction(asset, (GameAction)i).bindings[0].effectivePath;
                return result;
            }
            finally { Release(asset); }
        }
        public static InputActionAsset CreateBindingDraft(string[] paths)
        {
            var asset = CloneTemplate();
            try { ApplySaved(asset); ApplyPrimaryPaths(asset, paths); return asset; }
            catch { Release(asset); throw; }
        }
        private static void ApplyPrimaryPaths(InputActionAsset asset, string[] paths)
        {
            if (paths == null || paths.Length != 8) throw new ArgumentException("8개 게임 액션의 키 설정이 필요합니다.");
            for (int i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i])) throw new ArgumentException("비어 있는 입력은 저장할 수 없습니다.");
                var action = FindGameAction(asset, (GameAction)i);
                if (paths[i] == action.bindings[0].path) action.RemoveBindingOverride(0);
                else action.ApplyBindingOverride(0, paths[i]);
            }
        }
        public static void SaveBindings(string[] paths)
        {
            var asset = CreateBindingDraft(paths);
            try { PlayerPrefs.SetString(OverridesKey, asset.SaveBindingOverridesAsJson()); PlayerPrefs.Save(); }
            finally { Release(asset); }
            ReloadBindings();
        }
        public static void ReloadBindings()
        {
            DisposeRuntime();
            if (Application.isPlaying && !quitting) EnsureRuntime();
        }
        private static void OnQuitting() { quitting = true; DisposeRuntime(); }
        private static void DisposeRuntime()
        {
            if (subscribed)
            {
                InputSystem.onDeviceChange -= OnDeviceChange; Application.quitting -= OnQuitting; subscribed = false;
            }
            foreach (var asset in deviceActions.Values) Release(asset);
            deviceActions.Clear();
            Release(runtime); runtime = null;
        }
        public static void Release(InputActionAsset asset)
        {
            if (asset == null) return;
            asset.Disable();
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset); else UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
