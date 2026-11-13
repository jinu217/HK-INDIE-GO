#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using YutArena.Common;

namespace YutArena.EditorTools
{
    public static class GameInputAssetInstaller
    {
        public const string AssetPath = "Assets/Scripts/Resources/YutArenaInput.inputactions";

        [MenuItem("Tools/Yut Arena/입력 액션 리소스 연결")]
        public static void Install()
        {
            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath) != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/Resources")) AssetDatabase.CreateFolder("Assets/Scripts", "Resources");
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = GameInput.ResourceName;
            var gameplay = asset.AddActionMap("Gameplay");
            string[] defaults = GameInput.DefaultPaths();
            for (int i = 0; i < defaults.Length; i++)
                gameplay.AddAction(((GameAction)i).ToString(), InputActionType.Button, defaults[i], groups: GameInput.KeyboardMouseGroup, expectedControlLayout: "Button");
            gameplay.FindAction("Throw").AddBinding("<Gamepad>/buttonSouth", groups: GameInput.GamepadGroup);
            gameplay.FindAction("Cancel").AddBinding("<Keyboard>/escape", groups: GameInput.KeyboardMouseGroup);

            var ui = asset.AddActionMap("UI");
            AddButton(ui, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            AddButton(ui, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth").AddBinding("<Keyboard>/numpadEnter", groups: GameInput.KeyboardMouseGroup);
            AddButton(ui, "Click", "<Mouse>/leftButton");
            AddButton(ui, "SecondaryClick", "<Mouse>/rightButton");
            ui.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", groups: GameInput.KeyboardMouseGroup, expectedControlLayout: "Vector2");
            string[] names = { "Left", "Right", "Up", "Down" };
            string[] letters = { "a", "d", "w", "s" };
            string[] directions = { "left", "right", "up", "down" };
            for (int i = 0; i < names.Length; i++)
            {
                var action = AddButton(ui, names[i], "<Keyboard>/" + directions[i] + "Arrow", "<Gamepad>/dpad/" + directions[i]);
                action.AddBinding("<Keyboard>/" + letters[i], groups: GameInput.KeyboardMouseGroup);
                action.AddBinding("<Gamepad>/leftStick/" + directions[i], groups: GameInput.GamepadGroup);
            }
            asset.AddControlScheme(GameInput.KeyboardMouseGroup).WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
            asset.AddControlScheme(GameInput.GamepadGroup).WithRequiredDevice("<Gamepad>");
            File.WriteAllText(AssetPath, asset.ToJson());
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
        }
        private static InputAction AddButton(InputActionMap map, string name, string keyboardMouse, string gamepad = null)
        {
            var action = map.AddAction(name, InputActionType.Button, keyboardMouse, groups: GameInput.KeyboardMouseGroup, expectedControlLayout: "Button");
            if (gamepad != null) action.AddBinding(gamepad, groups: GameInput.GamepadGroup);
            return action;
        }
    }
}
#endif
