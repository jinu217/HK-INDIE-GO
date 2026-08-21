using UnityEngine;
using UnityEngine.UI;
using YutArena.Managers;

namespace YutArena.UI
{
    public sealed class AudioSettingsUI : MonoBehaviour
    {
        [Header("볼륨 슬라이더")]
        [Tooltip("BGM 전체 볼륨 Slider입니다. 범위는 코드에서 0~1로 설정됩니다.")]
        [SerializeField] private Slider bgmVolumeSlider;

        [Tooltip("모든 버튼 클릭음의 전체 볼륨 Slider입니다. 범위는 코드에서 0~1로 설정됩니다.")]
        [SerializeField] private Slider clickVolumeSlider;

        private void Awake()
        {
            ConfigureSlider(bgmVolumeSlider, AudioManager.LoadBgmVolume());
            ConfigureSlider(clickVolumeSlider, AudioManager.LoadClickVolume());
        }

        private void OnEnable()
        {
            if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
            if (clickVolumeSlider != null) clickVolumeSlider.onValueChanged.AddListener(SetClickVolume);
        }

        private void OnDisable()
        {
            if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.RemoveListener(SetBgmVolume);
            if (clickVolumeSlider != null) clickVolumeSlider.onValueChanged.RemoveListener(SetClickVolume);
        }

        public void SetBgmVolume(float volume)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetBgmVolume(volume);
        }

        public void SetClickVolume(float volume)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetClickVolume(volume);
        }

        private static void ConfigureSlider(Slider slider, float savedValue)
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(savedValue);
        }
    }
}
