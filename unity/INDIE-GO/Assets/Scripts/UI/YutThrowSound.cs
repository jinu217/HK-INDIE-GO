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
            // 같은 프레임의 턴 이벤트 구독자가 모두 처리된 뒤 연출 상태 확인
            yield return null;
            // 윷별 착지 시간이 달라도 마지막 윷의 반동 종료 후 확정음 재생
            YutThrowManager throwManager = FindFirstObjectByType<YutThrowManager>();
            while (throwManager != null && throwManager.IsThrowing) yield return null;
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

            AudioManager.Instance.PlayEffectSound(clip);
        }


    }
}
