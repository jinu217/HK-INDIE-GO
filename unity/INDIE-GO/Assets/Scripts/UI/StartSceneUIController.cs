using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections.Generic;
using YutArena.Managers;

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
        [Tooltip("게임 전체 오디오 볼륨을 0~100으로 조절하는 슬라이더")]
        [FormerlySerializedAs("bgmVolumeSlider")]
        [FormerlySerializedAs("masterVolumeSlider")]
        [SerializeField] private Slider masterVolumeSlider;
        [Tooltip("BGM 전체 볼륨을 0~100으로 조절하는 슬라이더")]
        [SerializeField] private Slider bgmVolumeSlider;
        [Tooltip("버튼음과 게임 효과음의 볼륨을 0~100으로 조절하는 슬라이더")]
        [SerializeField] private Slider clickVolumeSlider;
        [Tooltip("게임 전체 오디오의 음소거 여부를 설정하는 토글")]
        [SerializeField] private Toggle masterMuteToggle;
        [Tooltip("BGM의 음소거 여부를 설정하는 토글")]
        [SerializeField] private Toggle bgmMuteToggle;
        [Tooltip("버튼음과 게임 효과음의 음소거 여부를 설정하는 토글")]
        [SerializeField] private Toggle clickMuteToggle;
        [Tooltip("다른 창을 볼 때도 사운드를 재생할지 선택하는 토글")]
        [SerializeField] private Toggle backgroundSoundToggle;

        [Header("Graphics Settings")]
        [Tooltip("해상도 드롭다운")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [Tooltip("전체 화면, 테두리 없는 전체 화면, 창 모드를 선택하는 드롭다운")]
        [SerializeField] private TMP_Dropdown screenModeDropdown;
        [Tooltip("기존 전체 화면 토글입니다. 켜면 테두리 없는 전체 화면으로 설정됩니다.")]
        [SerializeField] private Toggle fullScreenToggle;
        [Tooltip("수직 동기화를 켜거나 끄는 토글")]
        [SerializeField] private Toggle vSyncToggle;
        [Tooltip("30, 60, 120, 144, 제한 없음 중 최대 프레임을 선택하는 드롭다운")]
        [SerializeField] private TMP_Dropdown frameRateDropdown;
        [Tooltip("낮음, 중간, 높음 그래픽 품질을 선택하는 드롭다운")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [Tooltip("화면 전체 밝기를 조절하는 슬라이더입니다. 0.5가 기본값입니다.")]
        [SerializeField] private Slider brightnessSlider;

        [Header("Scene")]
        [Tooltip("로컬 대기실 씬 이름")]
        [SerializeField] private string localLobbySceneName = "LocalLobbyScene";

        [Header("Debug")]
        [Tooltip("연결 누락 경고")]
        [SerializeField] private bool showMissingReferenceWarnings = true;

        private List<Vector2Int> resolutionOptions;

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
            WarnIfMissing(masterVolumeSlider, nameof(masterVolumeSlider));
            WarnIfMissing(bgmVolumeSlider, nameof(bgmVolumeSlider));
            WarnIfMissing(clickVolumeSlider, nameof(clickVolumeSlider));
            WarnIfMissing(masterMuteToggle, nameof(masterMuteToggle));
            WarnIfMissing(bgmMuteToggle, nameof(bgmMuteToggle));
            WarnIfMissing(clickMuteToggle, nameof(clickMuteToggle));
            WarnIfMissing(backgroundSoundToggle, nameof(backgroundSoundToggle));
            WarnIfMissing(resolutionDropdown, nameof(resolutionDropdown));
            WarnIfMissing(screenModeDropdown, nameof(screenModeDropdown));
            WarnIfMissing(vSyncToggle, nameof(vSyncToggle));
            WarnIfMissing(frameRateDropdown, nameof(frameRateDropdown));
            WarnIfMissing(qualityDropdown, nameof(qualityDropdown));
            WarnIfMissing(brightnessSlider, nameof(brightnessSlider));
        }

        private void SetupSettingsControls()
        {
            SetupResolutionDropdown();
            SetupScreenModeControls();
            SetupGraphicsControls();

            SetupVolumeSlider(masterVolumeSlider, AudioManager.LoadMasterVolume(), SetMasterVolume);
            SetupVolumeSlider(bgmVolumeSlider, AudioManager.LoadBgmVolume(), SetBgmVolume);
            SetupVolumeSlider(clickVolumeSlider, AudioManager.LoadClickVolume(), SetClickVolume);
            SetupMuteToggle(masterMuteToggle, AudioManager.LoadMasterMuted(), SetMasterMuted);
            SetupMuteToggle(bgmMuteToggle, AudioManager.LoadBgmMuted(), SetBgmMuted);
            SetupMuteToggle(clickMuteToggle, AudioManager.LoadClickMuted(), SetClickMuted);
            SetupMuteToggle(backgroundSoundToggle, AudioManager.LoadBackgroundSoundEnabled(), SetBackgroundSoundEnabled);
        }

        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            resolutionOptions = GraphicsSettingsManager.GetSupportedResolutions();
            var labels = new List<string>();
            foreach (Vector2Int option in resolutionOptions)
                labels.Add(option.x + " x " + option.y);
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(GetCurrentResolutionIndex());
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
        }

        private void SetupScreenModeControls()
        {
            if (screenModeDropdown != null)
            {
                screenModeDropdown.ClearOptions();
                screenModeDropdown.AddOptions(new List<string> { "전체 화면", "테두리 없는 전체 화면", "창 모드" });
                screenModeDropdown.SetValueWithoutNotify(GetScreenModeIndex());
                screenModeDropdown.RefreshShownValue();
                screenModeDropdown.onValueChanged.AddListener(SetScreenMode);
            }

            if (fullScreenToggle != null)
            {
                fullScreenToggle.SetIsOnWithoutNotify(GraphicsSettingsManager.ScreenMode != FullScreenMode.Windowed);
                fullScreenToggle.onValueChanged.AddListener(SetFullScreen);
            }
        }

        private void SetupGraphicsControls()
        {
            SetupMuteToggle(vSyncToggle, GraphicsSettingsManager.VSyncEnabled, SetVSync);
            if (frameRateDropdown != null)
            {
                frameRateDropdown.ClearOptions();
                frameRateDropdown.AddOptions(new List<string> { "30", "60", "120", "144", "제한 없음" });
                frameRateDropdown.SetValueWithoutNotify(GraphicsSettingsManager.FrameRateIndex);
                frameRateDropdown.RefreshShownValue();
                frameRateDropdown.onValueChanged.AddListener(SetFrameRate);
                frameRateDropdown.interactable = !GraphicsSettingsManager.VSyncEnabled;
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string> { "낮음", "중간", "높음" });
                qualityDropdown.SetValueWithoutNotify(GraphicsSettingsManager.QualityIndex);
                qualityDropdown.RefreshShownValue();
                qualityDropdown.onValueChanged.AddListener(SetQuality);
            }

            SetupBrightnessSlider(brightnessSlider, GraphicsSettingsManager.Brightness, SetBrightness);
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
            SyncVolumeSliders();
            SyncGraphicsControls();
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
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterVolume(volume / 100f);
            }
        }

        private void SetBgmVolume(float volume)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmVolume(volume / 100f);
            }
        }

        private void SetClickVolume(float volume)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetClickVolume(volume / 100f);
            }
        }

        private void SetMasterMuted(bool muted)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterMuted(muted);
            }
        }

        private void SetBgmMuted(bool muted)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmMuted(muted);
            }
        }

        private void SetClickMuted(bool muted)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetClickMuted(muted);
            }
        }

        private void SetBackgroundSoundEnabled(bool enabled)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetBackgroundSoundEnabled(enabled);
        }

        private void SyncVolumeSliders()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(AudioManager.LoadMasterVolume() * 100f);
            }

            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.SetValueWithoutNotify(AudioManager.LoadBgmVolume() * 100f);
            }

            if (clickVolumeSlider != null)
            {
                clickVolumeSlider.SetValueWithoutNotify(AudioManager.LoadClickVolume() * 100f);
            }

            if (masterMuteToggle != null)
            {
                masterMuteToggle.SetIsOnWithoutNotify(AudioManager.LoadMasterMuted());
            }

            if (bgmMuteToggle != null)
            {
                bgmMuteToggle.SetIsOnWithoutNotify(AudioManager.LoadBgmMuted());
            }

            if (clickMuteToggle != null)
            {
                clickMuteToggle.SetIsOnWithoutNotify(AudioManager.LoadClickMuted());
            }

            if (backgroundSoundToggle != null)
                backgroundSoundToggle.SetIsOnWithoutNotify(AudioManager.LoadBackgroundSoundEnabled());
        }

        private void SyncGraphicsControls()
        {
            if (resolutionDropdown != null)
            {
                resolutionDropdown.SetValueWithoutNotify(GetCurrentResolutionIndex());
                resolutionDropdown.RefreshShownValue();
            }

            if (screenModeDropdown != null)
            {
                screenModeDropdown.SetValueWithoutNotify(GetScreenModeIndex());
                screenModeDropdown.RefreshShownValue();
            }

            if (fullScreenToggle != null)
                fullScreenToggle.SetIsOnWithoutNotify(GraphicsSettingsManager.ScreenMode != FullScreenMode.Windowed);
            if (vSyncToggle != null)
                vSyncToggle.SetIsOnWithoutNotify(GraphicsSettingsManager.VSyncEnabled);
            if (frameRateDropdown != null)
            {
                frameRateDropdown.SetValueWithoutNotify(GraphicsSettingsManager.FrameRateIndex);
                frameRateDropdown.RefreshShownValue();
                frameRateDropdown.interactable = !GraphicsSettingsManager.VSyncEnabled;
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.SetValueWithoutNotify(GraphicsSettingsManager.QualityIndex);
                qualityDropdown.RefreshShownValue();
            }

            if (brightnessSlider != null)
                brightnessSlider.SetValueWithoutNotify(GraphicsSettingsManager.Brightness);
        }

        private static void SetupVolumeSlider(
            Slider slider,
            float initialValue,
            UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(initialValue * 100f);
            slider.onValueChanged.AddListener(onValueChanged);
        }

        private static void SetupBrightnessSlider(
            Slider slider,
            float initialValue,
            UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(initialValue);
            slider.onValueChanged.AddListener(onValueChanged);
        }

        private static void SetupMuteToggle(
            Toggle toggle,
            bool initialValue,
            UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.SetIsOnWithoutNotify(initialValue);
            toggle.onValueChanged.AddListener(onValueChanged);
        }

        private void SetResolution(int index)
        {
            if (resolutionOptions == null || index < 0 || index >= resolutionOptions.Count) return;
            if (GraphicsSettingsManager.Instance != null)
                GraphicsSettingsManager.Instance.SetResolution(resolutionOptions[index]);
        }

        private void SetScreenMode(int index)
        {
            FullScreenMode mode = index == 0 ? FullScreenMode.ExclusiveFullScreen :
                index == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (GraphicsSettingsManager.Instance != null)
                GraphicsSettingsManager.Instance.SetScreenMode(mode);
            if (fullScreenToggle != null)
                fullScreenToggle.SetIsOnWithoutNotify(mode != FullScreenMode.Windowed);
        }

        private void SetFullScreen(bool isFullScreen)
        {
            FullScreenMode mode = isFullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (GraphicsSettingsManager.Instance != null)
                GraphicsSettingsManager.Instance.SetScreenMode(mode);
            if (screenModeDropdown != null)
            {
                screenModeDropdown.SetValueWithoutNotify(GetScreenModeIndex());
                screenModeDropdown.RefreshShownValue();
            }
        }

        private void SetVSync(bool enabled)
        {
            if (GraphicsSettingsManager.Instance != null)
                GraphicsSettingsManager.Instance.SetVSync(enabled);
            if (frameRateDropdown != null) frameRateDropdown.interactable = !enabled;
        }

        private void SetFrameRate(int index)
        {
            if (GraphicsSettingsManager.Instance != null)
                GraphicsSettingsManager.Instance.SetFrameRateIndex(index);
        }

        private void SetQuality(int index)
        {
            if (GraphicsSettingsManager.Instance != null)
                GraphicsSettingsManager.Instance.SetQualityIndex(index);
        }

        private void SetBrightness(float value)
        {
            if (GraphicsSettingsManager.Instance != null)
                GraphicsSettingsManager.Instance.SetBrightness(value);
        }

        private static int GetScreenModeIndex()
        {
            switch (GraphicsSettingsManager.ScreenMode)
            {
                case FullScreenMode.ExclusiveFullScreen: return 0;
                case FullScreenMode.FullScreenWindow: return 1;
                default: return 2;
            }
        }

        private int GetCurrentResolutionIndex()
        {
            if (resolutionOptions == null || resolutionOptions.Count == 0) return 0;
            Vector2Int saved = GraphicsSettingsManager.SavedResolution;
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                if (resolutionOptions[i] == saved) return i;
            }

            return resolutionOptions.Count - 1;
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


