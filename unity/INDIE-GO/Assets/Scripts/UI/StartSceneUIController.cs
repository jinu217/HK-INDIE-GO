using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using YutArena.Common;

namespace YutArena.UI
{
    public class StartSceneUIController : MonoBehaviour
    {
        [Header("Main Buttons")]
        [Tooltip("도움말 버튼")]
        [SerializeField] private Button helpButton;
        [Tooltip("설정 버튼")]
        [SerializeField] private Button settingsButton;
        [Tooltip("게임 종료 버튼")]
        [SerializeField] private Button quitButton;

        [Header("Play Mode Buttons")]
        [Tooltip("로컬 플레이 버튼")]
        [SerializeField] private Button localPlayButton;
        [Tooltip("온라인 플레이 버튼")]
        [SerializeField] private Button onlinePlayButton;

        [Header("Online Buttons")]
        [Tooltip("온라인 게임 만들기 버튼")]
        [SerializeField] private Button onlineCreateGameButton;
        [Tooltip("온라인 게임 참가하기 버튼")]
        [SerializeField] private Button onlineJoinGameButton;

        [Header("Panels")]
        [Tooltip("온라인 게임 버튼 묶음")]
        [FormerlySerializedAs("onlineGamePanel")]
        [SerializeField] private GameObject onlineGameButtons;
        [Tooltip("온라인 참가 패널")]
        [SerializeField] private GameObject onlineJoinPanel;
        [Tooltip("도움말 패널")]
        [SerializeField] private GameObject helpPanel;
        [Tooltip("설정 패널")]
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("Scene")]
        [Tooltip("로컬 대기실 씬 이름")]
        [SerializeField] private string localLobbySceneName = "LocalLobbyScene";

        [Header("Debug")]
        [Tooltip("연결 누락 경고")]
        [SerializeField] private bool showMissingReferenceWarnings = true;

        private readonly List<RaycastResult> dismissHits = new List<RaycastResult>();

        private void LateUpdate()
        {
            if (SettingsPanel.IsBlockingInput) return;
            if (gameObject.scene.name != "StartScene") return;
            if (!IsActive(onlineGameButtons) && !IsActive(onlineJoinPanel) &&
                !IsActive(helpPanel)) return;

            if (GameInput.UiPressed(UiAction.Cancel))
            {
                HideAllPanels();
                return;
            }

            if (!GameInput.UiPressed(UiAction.Click) ||
                EventSystem.current == null) return;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = GameInput.PointerPosition
            };
            dismissHits.Clear();
            EventSystem.current.RaycastAll(pointer, dismissHits);
            foreach (RaycastResult hit in dismissHits)
            {
                Transform target = hit.gameObject.transform;
                if (hit.gameObject.GetComponentInParent<Selectable>() != null ||
                    IsInsideOpenPanel(target, onlineGameButtons) ||
                    IsInsideOpenPanel(target, onlineJoinPanel) ||
                    IsInsideOpenPanel(target, helpPanel)) return;
            }
            HideAllPanels();
        }

        private static bool IsInsideOpenPanel(Transform target, GameObject panel)
        {
            return panel != null && panel.activeInHierarchy && target.IsChildOf(panel.transform);
        }

        private void Awake()
        {
            if (settingsPanel == null) settingsPanel = FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            ValidateRequiredReferences();
            BindButtons();
            HideAllPanels();
        }

        private void BindButtons()
        {
            AddClick(helpButton, OpenHelp);
            AddClick(settingsButton, OpenSettings);
            AddClick(quitButton, () => SettingsPanel.ConfirmQuit(QuitGame));

            AddClick(localPlayButton, MoveToLocalLobby);
            AddClick(onlinePlayButton, ShowOnlineGamePanel);

            AddClick(onlineCreateGameButton, ShowOnlineFeaturePending);
            AddClick(onlineJoinGameButton, OpenOnlineJoin);


        }

        private void ValidateRequiredReferences()
        {
            if (!showMissingReferenceWarnings)
            {
                return;
            }

            WarnIfMissing(helpButton, nameof(helpButton));
            WarnIfMissing(settingsButton, nameof(settingsButton));
            WarnIfMissing(quitButton, nameof(quitButton));
            WarnIfMissing(localPlayButton, nameof(localPlayButton));
            WarnIfMissing(onlinePlayButton, nameof(onlinePlayButton));
            WarnIfMissing(onlineCreateGameButton, nameof(onlineCreateGameButton));
            WarnIfMissing(onlineJoinGameButton, nameof(onlineJoinGameButton));
            WarnIfMissing(onlineGameButtons, nameof(onlineGameButtons));
            WarnIfMissing(onlineJoinPanel, nameof(onlineJoinPanel));
            WarnIfMissing(helpPanel, nameof(helpPanel));
            WarnIfMissing(settingsPanel, nameof(settingsPanel));
        }

        private void ShowOnlineGamePanel()
        {
            ClosePopups();
            SetActive(onlineGameButtons, true);
        }

        private void OpenOnlineJoin()
        {
            if (IsActive(onlineJoinPanel))
            {
                return;
            }

            HideAllPanels();
            SetActive(onlineJoinPanel, true);
        }

        private void ShowOnlineFeaturePending()
        {
            Debug.Log("온라인 게임 만들기 기능은 추후 구현 예정입니다.");
        }

        private void MoveToLocalLobby()
        {
            if (string.IsNullOrWhiteSpace(localLobbySceneName))
            {
                Debug.LogWarning("Local lobby scene name is empty.");
                return;
            }

            SceneManager.LoadScene(localLobbySceneName);
        }

        private void OpenHelp()
        {
            if (IsActive(helpPanel))
            {
                return;
            }

            HideAllPanels();
            SetActive(helpPanel, true);
        }

        private void OpenSettings()
        {
            HideAllPanels();
            if (settingsPanel != null) settingsPanel.OpenFromTitle();
            else SettingsPanel.Show(true);
        }

        private void HideAllPanels()
        {
            SetActive(onlineGameButtons, false);
            SetActive(onlineJoinPanel, false);
            SetActive(helpPanel, false);
        }

        private void ClosePopups()
        {
            SetActive(onlineJoinPanel, false);
            SetActive(helpPanel, false);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private void WarnIfMissing(Object target, string fieldName)
        {
            if (target == null)
            {
                Debug.LogWarning($"{nameof(StartSceneUIController)}: {fieldName} 필드가 Inspector에 연결되지 않았습니다.", this);
            }
        }

        private static bool IsActive(GameObject target)
        {
            return target != null && target.activeSelf;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

    }
}


