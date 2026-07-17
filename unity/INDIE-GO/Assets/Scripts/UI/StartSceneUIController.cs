using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YutArena.UI
{
    public class StartSceneUIController : MonoBehaviour
    {
        [Header("Main Buttons")]
        [Tooltip("게임 시작 버튼입니다. 누르면 다른 패널을 끄고 로컬/온라인 플레이 선택 버튼을 보여줍니다.")]
        [SerializeField] private Button startGameButton;
        [Tooltip("도움말 버튼입니다. 누르면 다른 패널을 끄고 윷놀이 규칙, 캐릭터, 맵, 모드 설명 창을 엽니다.")]
        [SerializeField] private Button helpButton;
        [Tooltip("설정 버튼입니다. 누르면 다른 패널을 끄고 음향과 화면 설정 창을 엽니다.")]
        [SerializeField] private Button settingsButton;
        [Tooltip("게임 종료 버튼입니다. 빌드에서는 게임을 종료하고, 에디터에서는 플레이 모드를 종료합니다.")]
        [SerializeField] private Button quitButton;

        [Header("Play Mode Buttons")]
        [Tooltip("로컬 플레이 버튼입니다. 누르면 팝업 창을 끄고 로컬 게임 만들기/참가하기 버튼을 보여줍니다.")]
        [SerializeField] private Button localPlayButton;
        [Tooltip("온라인 플레이 버튼입니다. 누르면 팝업 창을 끄고 온라인 게임 만들기/참가하기 버튼을 보여줍니다.")]
        [SerializeField] private Button onlinePlayButton;

        [Header("Local Buttons")]
        [Tooltip("로컬 게임 만들기 버튼입니다. 누르면 로컬 대기실 씬으로 이동합니다.")]
        [SerializeField] private Button localCreateGameButton;
        [Tooltip("로컬 게임 참가하기 버튼입니다. 누르면 다른 패널을 끄고 참가 가능한 대기실 목록 UI 창을 엽니다. 실제 참가 기능은 추후 구현 예정입니다.")]
        [SerializeField] private Button localJoinGameButton;

        [Header("Online Buttons")]
        [Tooltip("온라인 게임 만들기 버튼입니다. 실제 기능은 추후 구현 예정입니다.")]
        [SerializeField] private Button onlineCreateGameButton;
        [Tooltip("온라인 게임 참가하기 버튼입니다. 실제 기능은 추후 구현 예정입니다.")]
        [SerializeField] private Button onlineJoinGameButton;

        [Header("Panels")]
        [Tooltip("게임 시작 버튼을 눌렀을 때 나타나는 로컬/온라인 플레이 선택 패널입니다.")]
        [SerializeField] private GameObject playModePanel;
        [Tooltip("로컬 플레이 버튼을 눌렀을 때 나타나는 로컬 게임 만들기/참가하기 패널입니다.")]
        [SerializeField] private GameObject localGamePanel;
        [Tooltip("온라인 플레이 버튼을 눌렀을 때 나타나는 온라인 게임 만들기/참가하기 패널입니다.")]
        [SerializeField] private GameObject onlineGamePanel;
        [Tooltip("도움말 창 패널입니다. 윷놀이 규칙, 캐릭터 설명, 맵 설명, 모드 설명 UI를 넣습니다.")]
        [SerializeField] private GameObject helpPanel;
        [Tooltip("설정 창 패널입니다. 음향, 해상도, 전체화면 설정 UI를 넣습니다.")]
        [SerializeField] private GameObject settingsPanel;
        [Tooltip("로컬 참가 창 패널입니다. 참가 가능한 대기실 목록 UI를 넣습니다. 목록 표시와 참가 기능은 추후 구현 예정입니다.")]
        [SerializeField] private GameObject localJoinPanel;

        [Header("Settings")]
        [Tooltip("전체 음량을 조절하는 슬라이더입니다. 값은 AudioListener.volume에 적용됩니다.")]
        [SerializeField] private Slider masterVolumeSlider;
        [Tooltip("창 크기를 선택하는 드롭다운입니다. 기본 해상도는 1280x720, 1600x900, 1920x1080입니다.")]
        [SerializeField] private Dropdown resolutionDropdown;
        [Tooltip("전체화면 여부를 조절하는 토글입니다.")]
        [SerializeField] private Toggle fullScreenToggle;

        [Header("Scene")]
        [Tooltip("로컬 게임 만들기 버튼을 눌렀을 때 이동할 대기실 씬 이름입니다. Build Settings에 등록되어 있어야 합니다.")]
        [SerializeField] private string localLobbySceneName = "LobbyScene";

        [Header("Debug")]
        [Tooltip("필수 버튼이나 패널이 Inspector에 연결되지 않았을 때 Console에 경고를 출력할지 여부입니다.")]
        [SerializeField] private bool showMissingReferenceWarnings = true;

        private readonly ResolutionOption[] resolutionOptions =
        {
            new ResolutionOption(1280, 720),
            new ResolutionOption(1600, 900),
            new ResolutionOption(1920, 1080)
        };

        private void Awake()
        {
            ValidateRequiredReferences();
            BindButtons();
            SetupSettingsControls();
            HideAllPanels();
        }

        private void BindButtons()
        {
            AddClick(startGameButton, ShowPlayModePanel);
            AddClick(helpButton, OpenHelp);
            AddClick(settingsButton, OpenSettings);
            AddClick(quitButton, QuitGame);

            AddClick(localPlayButton, ShowLocalGamePanel);
            AddClick(onlinePlayButton, ShowOnlineGamePanel);

            AddClick(localCreateGameButton, MoveToLocalLobby);
            AddClick(localJoinGameButton, OpenLocalJoin);

            AddClick(onlineCreateGameButton, ShowOnlineFeaturePending);
            AddClick(onlineJoinGameButton, ShowOnlineFeaturePending);
        }

        private void ValidateRequiredReferences()
        {
            if (!showMissingReferenceWarnings)
            {
                return;
            }

            WarnIfMissing(startGameButton, nameof(startGameButton));
            WarnIfMissing(helpButton, nameof(helpButton));
            WarnIfMissing(settingsButton, nameof(settingsButton));
            WarnIfMissing(quitButton, nameof(quitButton));
            WarnIfMissing(localPlayButton, nameof(localPlayButton));
            WarnIfMissing(onlinePlayButton, nameof(onlinePlayButton));
            WarnIfMissing(playModePanel, nameof(playModePanel));
            WarnIfMissing(localGamePanel, nameof(localGamePanel));
            WarnIfMissing(onlineGamePanel, nameof(onlineGamePanel));
            WarnIfMissing(helpPanel, nameof(helpPanel));
            WarnIfMissing(settingsPanel, nameof(settingsPanel));
            WarnIfMissing(localJoinPanel, nameof(localJoinPanel));
        }

        private void SetupSettingsControls()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = AudioListener.volume;
                masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();

                foreach (ResolutionOption option in resolutionOptions)
                {
                    resolutionDropdown.options.Add(new Dropdown.OptionData(option.Label));
                }

                resolutionDropdown.value = GetCurrentResolutionIndex();
                resolutionDropdown.RefreshShownValue();
                resolutionDropdown.onValueChanged.AddListener(SetResolution);
            }

            if (fullScreenToggle != null)
            {
                fullScreenToggle.isOn = Screen.fullScreen;
                fullScreenToggle.onValueChanged.AddListener(SetFullScreen);
            }
        }

        private void ShowPlayModePanel()
        {
            if (IsActive(playModePanel) && !IsActive(localGamePanel) && !IsActive(onlineGamePanel))
            {
                return;
            }

            HideAllPanels();
            SetActive(playModePanel, true);
        }

        private void ShowLocalGamePanel()
        {
            ClosePopups();
            SetActive(playModePanel, true);
            SetActive(localGamePanel, true);
            SetActive(onlineGamePanel, false);
        }

        private void ShowOnlineGamePanel()
        {
            ClosePopups();
            SetActive(playModePanel, true);
            SetActive(localGamePanel, false);
            SetActive(onlineGamePanel, true);
        }

        private void ShowOnlineFeaturePending()
        {
            Debug.Log("온라인 플레이 기능은 추후 구현 예정입니다.");
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
            if (IsActive(settingsPanel))
            {
                return;
            }

            HideAllPanels();
            SetActive(settingsPanel, true);
        }

        private void OpenLocalJoin()
        {
            if (IsActive(localJoinPanel))
            {
                return;
            }

            HideAllPanels();
            SetActive(localJoinPanel, true);
        }

        private void HideAllPanels()
        {
            SetActive(playModePanel, false);
            SetActive(localGamePanel, false);
            SetActive(onlineGamePanel, false);
            SetActive(helpPanel, false);
            SetActive(settingsPanel, false);
            SetActive(localJoinPanel, false);
        }

        private void ClosePopups()
        {
            SetActive(helpPanel, false);
            SetActive(settingsPanel, false);
            SetActive(localJoinPanel, false);
        }

        private void SetMasterVolume(float volume)
        {
            AudioListener.volume = volume;
        }

        private void SetResolution(int index)
        {
            if (index < 0 || index >= resolutionOptions.Length)
            {
                return;
            }

            ResolutionOption option = resolutionOptions[index];
            Screen.SetResolution(option.Width, option.Height, Screen.fullScreen);
        }

        private void SetFullScreen(bool isFullScreen)
        {
            Screen.fullScreen = isFullScreen;
        }

        private int GetCurrentResolutionIndex()
        {
            for (int i = 0; i < resolutionOptions.Length; i++)
            {
                if (Screen.width == resolutionOptions[i].Width && Screen.height == resolutionOptions[i].Height)
                {
                    return i;
                }
            }

            return resolutionOptions.Length - 1;
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

        private struct ResolutionOption
        {
            public ResolutionOption(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }
            public int Height { get; }

            public string Label
            {
                get { return Width + " x " + Height; }
            }
        }
    }
}
