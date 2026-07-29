using UnityEngine;

namespace YutArena.Managers
{
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("오디오 소스")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("재생할 오디오")]
        [SerializeField] private AudioClip audioClip;

        private static AudioManager instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            AudioSettingsManager.ApplySavedVolume();
            EnsureAudioSource();
            PlayAudio();
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
        }

        private void PlayAudio()
        {
            if (audioClip == null)
            {
                return;
            }

            audioSource.clip = audioClip;

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }
}
