using UnityEngine;

namespace YutArena.Managers
{
    /// <summary>기존 코드 호환용입니다. 새 코드는 AudioManager를 직접 사용하세요.</summary>
    public static class AudioSettingsManager
    {
        public static float MasterVolume => AudioManager.LoadMasterVolume();

        public static void SetMasterVolume(float volume)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMasterVolume(volume);
        }

        public static void ApplySavedVolume()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMasterVolume(MasterVolume);
        }
    }
}
