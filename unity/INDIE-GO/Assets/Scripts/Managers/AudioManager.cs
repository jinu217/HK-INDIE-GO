using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace YutArena.Managers
{
    public sealed class AudioManager : MonoBehaviour
    {
        public const string MasterVolumeKey = "MASTER_VOLUME";
        public const string BgmVolumeKey = "BGM_VOLUME";
        public const string ClickVolumeKey = "CLICK_VOLUME";
        public const string MasterMutedKey = "MASTER_MUTED";
        public const string BgmMutedKey = "BGM_MUTED";
        public const string ClickMutedKey = "CLICK_MUTED";
        private const float DefaultVolume = 1f;

        [Header("오디오 소스")]
        [Tooltip("BGM 재생 전용 AudioSource입니다. 비어 있으면 자동 생성됩니다.")]
        [FormerlySerializedAs("audioSource")]
        [SerializeField] private AudioSource bgmAudioSource;

        [Tooltip("버튼 클릭음 재생 전용 AudioSource입니다. 비어 있으면 자동 생성됩니다.")]
        [SerializeField] private AudioSource clickAudioSource;

        [Tooltip("토글 클릭음 재생 전용 AudioSource입니다. 마스터 볼륨만 적용되며 비어 있으면 자동 생성됩니다.")]
        [SerializeField] private AudioSource toggleAudioSource;

        [Header("초기 BGM (선택 사항)")]
        [Tooltip("첫 씬에 SceneBGM이 없을 때 재생할 BGM입니다. 일반적으로는 SceneBGM에서 지정합니다.")]
        [FormerlySerializedAs("audioClip")]
        [SerializeField] private AudioClip initialBgmClip;

        private static AudioManager instance;

        public static AudioManager Instance => instance;
        public float MasterVolume { get; private set; }
        public float BgmVolume { get; private set; }
        public float ClickVolume { get; private set; }
        public bool IsMasterMuted { get; private set; }
        public bool IsBgmMuted { get; private set; }
        public bool IsClickMuted { get; private set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioSources();
            ApplySavedVolumes();

            if (initialBgmClip != null) PlayBgm(initialBgmClip);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void PlayBgm(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("재생할 BGM AudioClip이 지정되지 않았습니다.", this);
                return;
            }

            if (bgmAudioSource.clip == clip && bgmAudioSource.isPlaying) return;

            bgmAudioSource.Stop();
            bgmAudioSource.clip = clip;
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }

        public void PlayClickSound(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("재생할 버튼 클릭 AudioClip이 지정되지 않았습니다.", this);
                return;
            }

            clickAudioSource.PlayOneShot(clip);
        }

        public void PlayInvalidSound(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("재생할 잘못된 조작 AudioClip이 지정되지 않았습니다.", this);
                return;
            }

            StartCoroutine(PlayInvalidSoundAfterCurrentClick(clip));
        }

        public void PlayToggleSound(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("재생할 토글 AudioClip이 지정되지 않았습니다.", this);
                return;
            }

            toggleAudioSource.PlayOneShot(clip);
        }

        private IEnumerator PlayInvalidSoundAfterCurrentClick(AudioClip clip)
        {
            yield return null;
            clickAudioSource.Stop();
            clickAudioSource.PlayOneShot(clip);
        }

        public void SetBgmVolume(float volume)
        {
            BgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
            ApplyEffectiveVolumes();
            PlayerPrefs.Save();
        }

        public void SetClickVolume(float volume)
        {
            ClickVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(ClickVolumeKey, ClickVolume);
            ApplyEffectiveVolumes();
            PlayerPrefs.Save();
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
            ApplyEffectiveVolumes();
            PlayerPrefs.Save();
        }

        public void SetMasterMuted(bool muted)
        {
            IsMasterMuted = muted;
            SaveMuted(MasterMutedKey, muted);
        }

        public void SetBgmMuted(bool muted)
        {
            IsBgmMuted = muted;
            SaveMuted(BgmMutedKey, muted);
        }

        public void SetClickMuted(bool muted)
        {
            IsClickMuted = muted;
            SaveMuted(ClickMutedKey, muted);
        }

        public static float LoadMasterVolume() => LoadVolume(MasterVolumeKey);
        public static float LoadBgmVolume() => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume));
        public static float LoadClickVolume() => Mathf.Clamp01(PlayerPrefs.GetFloat(ClickVolumeKey, DefaultVolume));
        public static bool LoadMasterMuted() => LoadMuted(MasterMutedKey);
        public static bool LoadBgmMuted() => LoadMuted(BgmMutedKey);
        public static bool LoadClickMuted() => LoadMuted(ClickMutedKey);

        private void ApplySavedVolumes()
        {
            MasterVolume = LoadMasterVolume();
            BgmVolume = LoadBgmVolume();
            ClickVolume = LoadClickVolume();
            IsMasterMuted = LoadMasterMuted();
            IsBgmMuted = LoadBgmMuted();
            IsClickMuted = LoadClickMuted();
            ApplyEffectiveVolumes();
        }

        private void ApplyEffectiveVolumes()
        {
            bgmAudioSource.volume = IsMasterMuted || IsBgmMuted ? 0f : MasterVolume * BgmVolume;
            clickAudioSource.volume = IsMasterMuted || IsClickMuted ? 0f : MasterVolume * ClickVolume;
            toggleAudioSource.volume = IsMasterMuted ? 0f : MasterVolume;
        }

        private void SaveMuted(string key, bool muted)
        {
            PlayerPrefs.SetInt(key, muted ? 1 : 0);
            ApplyEffectiveVolumes();
            PlayerPrefs.Save();
        }

        private static float LoadVolume(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat(key, DefaultVolume));
        private static bool LoadMuted(string key) => PlayerPrefs.GetInt(key, 0) == 1;

        private void EnsureAudioSources()
        {
            if (bgmAudioSource == null) bgmAudioSource = gameObject.AddComponent<AudioSource>();
            if (clickAudioSource == null || clickAudioSource == bgmAudioSource)
                clickAudioSource = gameObject.AddComponent<AudioSource>();
            if (toggleAudioSource == null || toggleAudioSource == bgmAudioSource || toggleAudioSource == clickAudioSource)
                toggleAudioSource = gameObject.AddComponent<AudioSource>();

            bgmAudioSource.playOnAwake = false;
            bgmAudioSource.loop = true;
            clickAudioSource.playOnAwake = false;
            clickAudioSource.loop = false;
            toggleAudioSource.playOnAwake = false;
            toggleAudioSource.loop = false;
        }
    }
}
