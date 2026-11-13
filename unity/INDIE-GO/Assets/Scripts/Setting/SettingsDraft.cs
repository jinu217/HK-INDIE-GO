using System;
using UnityEngine;
using YutArena.Managers;

namespace YutArena.UI
{
    public enum SettingsTab { General, Display, Sound, Controls }

    [Serializable]
    public sealed class SettingsDraft
    {
        public int speed, textSize, frameRate = 1, quality = 2;
        public FullScreenMode mode = FullScreenMode.FullScreenWindow;
        public int width, height;
        public bool vSync = true, masterMuted, bgmMuted, effectsMuted, backgroundSound;
        public float brightness = .5f, master = 1f, bgm = 1f, effects = 1f;
        public string[] bindings = YutArena.Common.GameInput.SeedDefaultPaths();

        public SettingsDraft Copy() => JsonUtility.FromJson<SettingsDraft>(JsonUtility.ToJson(this));
        public bool SameAs(SettingsDraft other) => JsonUtility.ToJson(this) == JsonUtility.ToJson(other);
        public bool DisplayChanged(SettingsDraft other) => mode != other.mode || width != other.width || height != other.height;
        public void RestoreDisplay(SettingsDraft saved)
        {
            mode = saved.mode; width = saved.width; height = saved.height;
        }

        public static SettingsDraft Load()
        {
            Vector2Int resolution = GraphicsSettingsManager.SavedResolution;
            return new SettingsDraft {
                speed = GeneralSettingsManager.PresentationSpeedIndex, textSize = GeneralSettingsManager.TextSizeIndex,
                mode = GraphicsSettingsManager.ScreenMode, width = resolution.x, height = resolution.y,
                vSync = GraphicsSettingsManager.VSyncEnabled, frameRate = GraphicsSettingsManager.FrameRateIndex,
                quality = GraphicsSettingsManager.QualityIndex, brightness = GraphicsSettingsManager.Brightness,
                master = AudioManager.LoadMasterVolume(), bgm = AudioManager.LoadBgmVolume(), effects = AudioManager.LoadClickVolume(),
                masterMuted = AudioManager.LoadMasterMuted(), bgmMuted = AudioManager.LoadBgmMuted(),
                effectsMuted = AudioManager.LoadClickMuted(), backgroundSound = AudioManager.LoadBackgroundSoundEnabled(),
                bindings = SettingsBindings.Load()
            };
        }

        public void ResetTab(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.General: speed = 0; textSize = 1; break;
                case SettingsTab.Display:
                    mode = FullScreenMode.FullScreenWindow;
                    width = Screen.currentResolution.width; height = Screen.currentResolution.height;
                    vSync = true; frameRate = 1; quality = 2; brightness = .5f; break;
                case SettingsTab.Sound:
                    master = bgm = effects = 1f;
                    masterMuted = bgmMuted = effectsMuted = backgroundSound = false; break;
                case SettingsTab.Controls: bindings = SettingsBindings.Defaults(); break;
            }
        }

        public void Save()
        {
            // Only called after the display has been accepted. Editing never writes PlayerPrefs.
            var graphics = GraphicsSettingsManager.Instance;
            if (graphics == null || GeneralSettingsManager.Instance == null || AudioManager.Instance == null)
                throw new InvalidOperationException("설정 관리자를 찾을 수 없습니다.");
            graphics.CommitDisplay(mode, new Vector2Int(width, height));
            graphics.SetVSync(vSync); graphics.SetFrameRateIndex(frameRate);
            graphics.SetQualityIndex(quality); graphics.SetBrightness(brightness);
            GeneralSettingsManager.Instance.SetPresentationSpeed(speed);
            GeneralSettingsManager.Instance.SetTextSize(textSize);
            var audio = AudioManager.Instance;
            audio.SetMasterVolume(master); audio.SetBgmVolume(bgm); audio.SetClickVolume(effects);
            audio.SetMasterMuted(masterMuted); audio.SetBgmMuted(bgmMuted); audio.SetClickMuted(effectsMuted);
            audio.SetBackgroundSoundEnabled(backgroundSound);
            SettingsBindings.Save(bindings);
        }
    }
}
