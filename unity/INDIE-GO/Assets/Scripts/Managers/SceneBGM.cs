using UnityEngine;

namespace YutArena.Managers
{
    public sealed class SceneBGM : MonoBehaviour
    {
        [Header("씬 BGM")]
        [Tooltip("이 씬에 입장했을 때 자동으로 반복 재생할 BGM입니다.")]
        [SerializeField] private AudioClip bgmClip;

        private void Start()
        {
            if (bgmClip == null)
            {
                Debug.LogWarning($"{gameObject.scene.name} 씬의 BGM AudioClip이 지정되지 않았습니다.", this);
                return;
            }

            if (AudioManager.Instance == null)
            {
                Debug.LogError("활성화된 AudioManager가 없습니다. 시작 씬에 AudioManager를 배치해 주세요.", this);
                return;
            }

            AudioManager.Instance.PlayBgm(bgmClip);
        }
    }
}
