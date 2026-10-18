using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using YutArena.Common;
using UnityEngine.Serialization;

namespace YutArena.UI.CharacterScene
{
    public class GameStartLoadingPanel : MonoBehaviour
    {
        public static bool IsBlockingGameplay { get; private set; }

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private RectTransform playerListRoot;
        [SerializeField] private LoadingPlayerEntryView playerEntryTemplate;
        [SerializeField] private GameModeDatabase gameModeDatabase;
        [SerializeField] private TMP_Text gameModeText;
        [SerializeField] private TMP_Text teamModeText;
        [FormerlySerializedAs("minimumFinishDuration")]
        [SerializeField, Min(0f)] private float minimumDisplayDuration = 3f;
        [SerializeField, Min(0f)] private float completedHoldDuration = 0.15f;

        private bool isLoading;

        private void OnDestroy()
        {
            if (isLoading)
                IsBlockingGameplay = false;
        }

        public void SetPlayers(CharacterData[] characters, int playerCount, PlayerSelectionMarkerView[] markers)
        {
            RefreshGameInformation();
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
                LoadingPlayerEntryView entry = Instantiate(playerEntryTemplate, playerListRoot);
                entry.name = $"Player{i + 1}LoadingInfo";
                entry.gameObject.SetActive(true);
                entry.Refresh(characters[i], markers != null && i < markers.Length ? markers[i] : null);
            }
        }

        private void RefreshGameInformation()
        {
            GameStartSettings settings = GameStartSettingsHolder.Current;
            if (settings == null) return;

            GameModeEntry mode = gameModeDatabase != null
                ? gameModeDatabase.FindByMode(settings.gameMode) : null;
            if (gameModeText != null)
                gameModeText.text = mode != null && !string.IsNullOrWhiteSpace(mode.modeName)
                    ? mode.modeName : settings.gameMode.ToString();
            if (teamModeText != null)
                teamModeText.text = settings.isTeamMode ? "팀전" : "개인전";
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
            IsBlockingGameplay = true;
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            if (panelRoot != null) panelRoot.SetActive(true);
            StartCoroutine(AnimateLoadingText());

            // 패널이 먼저 화면에 그려진 뒤 씬 로딩을 시작한다.
            yield return null;
            float displayStartedAt = Time.realtimeSinceStartup;

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
                yield return null;
            }

            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            float remainingDisplayDuration = Mathf.Max(0f,
                minimumDisplayDuration - (Time.realtimeSinceStartup - displayStartedAt) - completedHoldDuration);
            if (remainingDisplayDuration > 0f)
                yield return new WaitForSecondsRealtime(remainingDisplayDuration);

            if (completedHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(completedHoldDuration);
            if (panelRoot != null) panelRoot.SetActive(false);
            IsBlockingGameplay = false;
            Destroy(gameObject);
        }

        private IEnumerator AnimateLoadingText()
        {
            int dotCount = 1;
            while (isLoading)
            {
                if (loadingText != null)
                    loadingText.text = "로딩중 " + new string('.', dotCount);
                dotCount = dotCount % 3 + 1;
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }
    }
}
