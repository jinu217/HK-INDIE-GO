using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YutArena.UI.CharacterScene
{
    public class GameStartLoadingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform playerListRoot;
        [SerializeField] private PlayerCharacterPanelView playerEntryTemplate;
        [SerializeField, Min(0f)] private float minimumFinishDuration = 1f;
        [SerializeField, Min(0f)] private float completedHoldDuration = 0.15f;

        private bool isLoading;

        public void SetPlayers(CharacterData[] characters, int playerCount)
        {
            if (playerListRoot == null || playerEntryTemplate == null) return;

            for (int i = playerListRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = playerListRoot.GetChild(i);
                if (child != playerEntryTemplate.transform)
                    Destroy(child.gameObject);
            }

            int count = Mathf.Min(playerCount, characters != null ? characters.Length : 0);
            for (int i = 0; i < count; i++)
            {
                PlayerCharacterPanelView entry = Instantiate(playerEntryTemplate, playerListRoot);
                entry.name = $"Player{i + 1}LoadingInfo";
                entry.gameObject.SetActive(true);
                entry.Refresh(i, characters[i], true);
            }
        }

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        public void LoadScene(string sceneName)
        {
            if (isLoading || string.IsNullOrWhiteSpace(sceneName)) return;
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            isLoading = true;
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            SetProgress(0f);
            if (panelRoot != null) panelRoot.SetActive(true);

            // 패널이 먼저 화면에 그려진 뒤 씬 로딩을 시작한다.
            yield return null;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError($"씬을 불러올 수 없습니다: {sceneName}", this);
                Destroy(gameObject);
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                SetProgress(Mathf.Lerp(0f, 0.7f, Mathf.Clamp01(operation.progress / 0.9f)));
                yield return null;
            }

            SetProgress(0.7f);
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            float elapsed = 0f;
            while (elapsed < minimumFinishDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetProgress(Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(elapsed / minimumFinishDuration)));
                yield return null;
            }

            SetProgress(1f);
            if (completedHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(completedHoldDuration);
            Destroy(gameObject);
        }

        private void SetProgress(float progress)
        {
            if (progressFill != null)
                progressFill.fillAmount = Mathf.Clamp01(progress);
        }
    }
}
