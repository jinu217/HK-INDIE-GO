using UnityEngine;
using UnityEngine.UI;
using YutArena.Managers;

namespace YutArena.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class ButtonSound : MonoBehaviour
    {
        [Header("버튼 클릭음")]
        [Tooltip("이 버튼을 클릭했을 때 재생할 AudioClip입니다. 버튼마다 다르게 지정할 수 있습니다.")]
        [SerializeField] private AudioClip clickClip;

        [Tooltip("이 버튼의 실행 조건을 충족하지 못했을 때 재생할 AudioClip입니다.")]
        [SerializeField] private AudioClip invalidClickClip;

        private Button button;

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable() => button.onClick.AddListener(PlayClickSound);

        private void OnDisable() => button.onClick.RemoveListener(PlayClickSound);

        private void PlayClickSound()
        {
            if (AudioManager.Instance == null)
            {
                Debug.LogError("활성화된 AudioManager가 없어 클릭음을 재생할 수 없습니다.", this);
                return;
            }

            AudioManager.Instance.PlayClickSound(clickClip);
        }

        public void PlayInvalidClickSound()
        {
            if (AudioManager.Instance == null)
            {
                Debug.LogError("활성화된 AudioManager가 없어 잘못된 클릭음을 재생할 수 없습니다.", this);
                return;
            }

            AudioManager.Instance.PlayInvalidSound(invalidClickClip);
        }
    }
}
