using UnityEngine;

namespace YutArena.Managers
{
    public static class AudioSettingsManager
    {
        private const string MasterVolumeKey = "MasterVolume";
        private const float DefaultMasterVolume = 1f;

        public static float MasterVolume
        {
            get { return PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ApplySavedVolume();
        }

        public static void SetMasterVolume(float volume)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            AudioListener.volume = clampedVolume;
            PlayerPrefs.SetFloat(MasterVolumeKey, clampedVolume);
            PlayerPrefs.Save();
        }

        public static void ApplySavedVolume()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
