using System.Collections;
using UnityEngine;
using YutArena.Common;
using YutArena.Managers;

namespace YutArena.Test
{
    /// <summary>
    /// TestTurnManager의 실제 턴 단계에 맞춰 윷 던지기와 추가 던지기 확정 효과음을 재생한다.
    /// </summary>
    public sealed class YutThrowSound : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TestTurnManager turnManager;

        [Header("Clips")]
        [SerializeField] private AudioClip throwClip;
        [SerializeField] private AudioClip extraThrowConfirmedClip;

        [Header("Timing")]
        [Tooltip("윷 던지기 연출이 끝난 뒤 확정음을 재생하기 위한 대기 시간")]
        [SerializeField, Min(0f)] private float extraThrowSoundDelay = 0.85f;

        private TurnPhase previousPhase = TurnPhase.None;
        private Coroutine extraSoundCoroutine;

        private void Awake()
        {
            if (turnManager == null)
            {
                turnManager = FindFirstObjectByType<TestTurnManager>();
            }
        }

        private void OnEnable()
        {
            if (turnManager == null)
            {
                return;
            }

            previousPhase = turnManager.CurrentTurn.currentPhase;
            turnManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        }

        private void OnDisable()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            }

            if (extraSoundCoroutine != null)
            {
                StopCoroutine(extraSoundCoroutine);
                extraSoundCoroutine = null;
            }
        }

        private void HandleTurnPhaseChanged(TurnContext turn)
        {
            if (turn == null)
            {
                return;
            }

            TurnPhase currentPhase = turn.currentPhase;

            if (currentPhase == TurnPhase.SaveThrowResult)
            {
                PlayOneShot(throwClip);
            }

            bool extraThrowConfirmed =
                currentPhase == TurnPhase.WaitThrow
                && (previousPhase == TurnPhase.CheckExtraThrow
                    || previousPhase == TurnPhase.CheckBonusThrow);

            if (extraThrowConfirmed)
            {
                if (extraSoundCoroutine != null)
                {
                    StopCoroutine(extraSoundCoroutine);
                }

                extraSoundCoroutine = StartCoroutine(PlayExtraSoundAfterDelay());
            }

            previousPhase = currentPhase;
        }

        private IEnumerator PlayExtraSoundAfterDelay()
        {
            yield return new WaitForSeconds(extraThrowSoundDelay);
            PlayOneShot(extraThrowConfirmedClip);
            extraSoundCoroutine = null;
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (AudioManager.Instance == null)
            {
                Debug.LogError("활성화된 AudioManager가 없어 윷 효과음을 재생할 수 없습니다.", this);
                return;
            }

            AudioManager.Instance.PlayClickSound(clip);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            extraThrowSoundDelay = Mathf.Max(0f, extraThrowSoundDelay);
        }
#endif
    }
}
