using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace YutArena.UI
{
    public class StartSceneUIController : MonoBehaviour
    {
        [Header("Main Buttons")]
        [Tooltip("게임 시작 버튼")]
        [SerializeField] private Button startGameButton;
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

        [Header("Help Buttons")]
        [Tooltip("윷놀이 규칙 버튼")]
        [SerializeField] private Button yutRuleHelpButton;
        [Tooltip("캐릭터 설명 버튼")]
        [SerializeField] private Button characterHelpButton;
        [Tooltip("맵 설명 버튼")]
        [SerializeField] private Button mapHelpButton;
        [Tooltip("모드 설명 버튼")]
        [SerializeField] private Button modeHelpButton;

        [Header("Panels")]
        [Tooltip("플레이 모드 버튼 묶음")]
        [FormerlySerializedAs("playModePanel")]
        [SerializeField] private GameObject playModeButtons;
        [Tooltip("온라인 게임 버튼 묶음")]
        [FormerlySerializedAs("onlineGamePanel")]
        [SerializeField] private GameObject onlineGameButtons;
        [Tooltip("온라인 참가 패널")]
        [SerializeField] private GameObject onlineJoinPanel;
        [Tooltip("도움말 패널")]
        [SerializeField] private GameObject helpPanel;
        [Tooltip("설정 패널")]
        [SerializeField] private GameObject settingsPanel;

        [Header("Help Image")]
        [Tooltip("도움말 표시 이미지")]
        [SerializeField] private Image helpContentImage;
        [Tooltip("윷놀이 규칙 이미지")]
        [SerializeField] private Sprite yutRuleHelpSprite;
        [Tooltip("캐릭터 설명 이미지")]
        [SerializeField] private Sprite characterHelpSprite;
        [Tooltip("맵 설명 이미지")]
        [SerializeField] private Sprite mapHelpSprite;
        [Tooltip("모드 설명 이미지")]
        [SerializeField] private Sprite modeHelpSprite;

        [Header("Settings")]
        [Tooltip("음향 슬라이더")]
        [SerializeField] private Slider masterVolumeSlider;
        [Tooltip("해상도 드롭다운")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [Tooltip("화면 모드 드롭다운")]
        [SerializeField] private TMP_Dropdown screenModeDropdown;
        [Tooltip("전체화면 토글")]
        [SerializeField] private Toggle fullScreenToggle;

        [Header("Scene")]
        [Tooltip("로컬 대기실 씬 이름")]
        [SerializeField] private string localLobbySceneName = "LocalLobbyScene";

        [Header("Debug")]
        [Tooltip("연결 누락 경고")]
        [SerializeField] private bool showMissingReferenceWarnings = true;

        private readonly ResolutionOption[] resolutionOptions =
        {
            new ResolutionOption(1024, 768, "4 : 3"),
            new ResolutionOption(1280, 960, "4 : 3"),
            new ResolutionOption(1600, 1200, "4 : 3"),
            new ResolutionOption(1280, 720, "16 : 9"),
            new ResolutionOption(1600, 900, "16 : 9"),
            new ResolutionOption(1920, 1080, "16 : 9"),
            new ResolutionOption(1280, 800, "16 : 10"),
            new ResolutionOption(1680, 1050, "16 : 10"),
            new ResolutionOption(1920, 1200, "16 : 10")
        };

        private void Awake()
        {
            ValidateRequiredReferences();
            BindButtons();
            SetupSettingsControls();
            SetupHelpImage();
            HideAllPanels();
        }

        private void BindButtons()
        {
            AddClick(startGameButton, ShowPlayModePanel);
            AddClick(helpButton, OpenHelp);
            AddClick(settingsButton, OpenSettings);
            AddClick(quitButton, QuitGame);

            AddClick(localPlayButton, MoveToLocalLobby);
            AddClick(onlinePlayButton, ShowOnlineGamePanel);

            AddClick(onlineCreateGameButton, ShowOnlineFeaturePending);
            AddClick(onlineJoinGameButton, OpenOnlineJoin);

            AddClick(yutRuleHelpButton, ShowYutRuleHelp);
            AddClick(characterHelpButton, ShowCharacterHelp);
            AddClick(mapHelpButton, ShowMapHelp);
            AddClick(modeHelpButton, ShowModeHelp);
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
            WarnIfMissing(onlineCreateGameButton, nameof(onlineCreateGameButton));
            WarnIfMissing(onlineJoinGameButton, nameof(onlineJoinGameButton));
            WarnIfMissing(playModeButtons, nameof(playModeButtons));
            WarnIfMissing(onlineGameButtons, nameof(onlineGameButtons));
            WarnIfMissing(onlineJoinPanel, nameof(onlineJoinPanel));
            WarnIfMissing(helpPanel, nameof(helpPanel));
            WarnIfMissing(settingsPanel, nameof(settingsPanel));
        }

        private void SetupSettingsControls()
        {
            SetupResolutionDropdown();
            SetupScreenModeControls();

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = AudioListener.volume;
                masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            }
        }

        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            resolutionDropdown.ClearOptions();

            foreach (ResolutionOption option in resolutionOptions)
            {
                resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(option.Label));
            }

            resolutionDropdown.value = GetCurrentResolutionIndex();
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
        }

        private void SetupScreenModeControls()
        {
            if (screenModeDropdown != null)
            {
                screenModeDropdown.ClearOptions();
                screenModeDropdown.options.Add(new TMP_Dropdown.OptionData("Windowed Mode"));
                screenModeDropdown.options.Add(new TMP_Dropdown.OptionData("Full Screen"));
                screenModeDropdown.value = Screen.fullScreen ? 1 : 0;
                screenModeDropdown.RefreshShownValue();
                screenModeDropdown.onValueChanged.AddListener(SetScreenMode);
            }

            if (fullScreenToggle != null)
            {
                fullScreenToggle.isOn = Screen.fullScreen;
                fullScreenToggle.onValueChanged.AddListener(SetFullScreen);
            }
        }

        private void SetupHelpImage()
        {
            if (helpContentImage != null && helpContentImage.sprite == null)
            {
                SetHelpImage(yutRuleHelpSprite);
            }
        }

        private void ShowPlayModePanel()
        {
            if (IsActive(playModeButtons) && !IsActive(onlineGameButtons))
            {
                return;
            }

            HideAllPanels();
            SetActive(playModeButtons, true);
        }

        private void ShowOnlineGamePanel()
        {
            ClosePopups();
            SetActive(playModeButtons, true);
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
            if (IsActive(settingsPanel))
            {
                return;
            }

            HideAllPanels();
            SetActive(settingsPanel, true);
        }

        private void HideAllPanels()
        {
            SetActive(playModeButtons, false);
            SetActive(onlineGameButtons, false);
            SetActive(onlineJoinPanel, false);
            SetActive(helpPanel, false);
            SetActive(settingsPanel, false);
        }

        private void ClosePopups()
        {
            SetActive(onlineJoinPanel, false);
            SetActive(helpPanel, false);
            SetActive(settingsPanel, false);
        }

        private void ShowYutRuleHelp()
        {
            SetHelpImage(yutRuleHelpSprite);
        }

        private void ShowCharacterHelp()
        {
            SetHelpImage(characterHelpSprite);
        }

        private void ShowMapHelp()
        {
            SetHelpImage(mapHelpSprite);
        }

        private void ShowModeHelp()
        {
            SetHelpImage(modeHelpSprite);
        }

        private void SetHelpImage(Sprite sprite)
        {
            if (helpContentImage == null || sprite == null)
            {
                return;
            }

            helpContentImage.sprite = sprite;
            helpContentImage.preserveAspect = true;
            helpContentImage.enabled = true;
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
            Screen.SetResolution(option.Width, option.Height, Screen.fullScreenMode);
        }

        private void SetScreenMode(int index)
        {
            bool isFullScreen = index == 1;
            ApplyFullScreen(isFullScreen);
        }

        private void SetFullScreen(bool isFullScreen)
        {
            ApplyFullScreen(isFullScreen);
        }

        private void ApplyFullScreen(bool isFullScreen)
        {
            Screen.fullScreenMode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.fullScreen = isFullScreen;

            if (screenModeDropdown != null && screenModeDropdown.value != (isFullScreen ? 1 : 0))
            {
                screenModeDropdown.value = isFullScreen ? 1 : 0;
                screenModeDropdown.RefreshShownValue();
            }

            if (fullScreenToggle != null && fullScreenToggle.isOn != isFullScreen)
            {
                fullScreenToggle.isOn = isFullScreen;
            }

            SetResolution(resolutionDropdown != null ? resolutionDropdown.value : 0);
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

            return 5;
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
            public ResolutionOption(int width, int height, string aspectRatio)
            {
                Width = width;
                Height = height;
                AspectRatio = aspectRatio;
            }

            public int Width { get; }
            public int Height { get; }
            public string AspectRatio { get; }

            public string Label
            {
                get { return Width + " x " + Height + " (" + AspectRatio + ")"; }
            }
        }
    }
}

