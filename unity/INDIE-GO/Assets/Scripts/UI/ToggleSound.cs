using UnityEngine;
using UnityEngine.UI;
using YutArena.Managers;

namespace YutArena.UI
{
    [RequireComponent(typeof(Toggle))]
    public sealed class ToggleSound : MonoBehaviour
    {
        [Header("토글 클릭음")]
        [Tooltip("사용자가 이 토글의 상태를 변경했을 때 재생할 AudioClip입니다.")]
        [SerializeField] private AudioClip toggleClip;

        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
        }

        private void OnEnable()
        {
            toggle.onValueChanged.AddListener(PlayToggleSound);
        }

        private void OnDisable()
        {
            toggle.onValueChanged.RemoveListener(PlayToggleSound);
        }

        private void PlayToggleSound(bool _)
        {
            if (AudioManager.Instance == null)
            {
                Debug.LogError("활성화된 AudioManager가 없어 토글 클릭음을 재생할 수 없습니다.", this);
                return;
            }

            AudioManager.Instance.PlayToggleSound(toggleClip);
        }
    }
}
