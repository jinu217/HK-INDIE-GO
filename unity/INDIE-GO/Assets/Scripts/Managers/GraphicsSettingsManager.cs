using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.Managers
{
    /// <summary>모든 씬에 적용되는 그래픽 설정과 밝기 오버레이를 관리합니다.</summary>
    public sealed class GraphicsSettingsManager : MonoBehaviour
    {
        private const string ScreenModeKey = "SCREEN_MODE";
        private const string ResolutionWidthKey = "RESOLUTION_WIDTH";
        private const string ResolutionHeightKey = "RESOLUTION_HEIGHT";
        private const string VSyncKey = "VSYNC_ENABLED";
        private const string FrameRateKey = "MAX_FRAME_RATE";
        private const string QualityKey = "GRAPHICS_QUALITY";
        private const string BrightnessKey = "BRIGHTNESS";

        private static readonly int[] FrameRates = { 30, 60, 120, 144, -1 };
        private static GraphicsSettingsManager instance;
        private Image brightnessOverlay;

        public static GraphicsSettingsManager Instance => instance;
        public static bool VSyncEnabled => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
        public static int FrameRateIndex => Mathf.Clamp(PlayerPrefs.GetInt(FrameRateKey, 1), 0, FrameRates.Length - 1);
        public static int QualityIndex => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 2), 0, 2);
        public static float Brightness => Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey, 0.5f));
        public static FullScreenMode ScreenMode
        {
            get
            {
                int saved = PlayerPrefs.GetInt(ScreenModeKey, (int)FullScreenMode.FullScreenWindow);
                return saved == (int)FullScreenMode.ExclusiveFullScreen ||
                       saved == (int)FullScreenMode.Windowed
                    ? (FullScreenMode)saved
                    : FullScreenMode.FullScreenWindow;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance != null) return;
            new GameObject("GraphicsSettingsManager").AddComponent<GraphicsSettingsManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateBrightnessOverlay();
            ApplySavedSettings();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static List<Vector2Int> GetSupportedResolutions()
        {
            var choices = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!choices.Contains(size)) choices.Add(size);
            }

            if (choices.Count == 0)
                choices.Add(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));

            choices.Sort((a, b) =>
            {
                int widthComparison = a.x.CompareTo(b.x);
                return widthComparison != 0 ? widthComparison : a.y.CompareTo(b.y);
            });
            return choices;
        }

        public static Vector2Int SavedResolution => new Vector2Int(
            PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width),
            PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height));

        public void SetScreenMode(FullScreenMode mode)
        {
            if (mode != FullScreenMode.ExclusiveFullScreen &&
                mode != FullScreenMode.FullScreenWindow &&
                mode != FullScreenMode.Windowed) return;

            PlayerPrefs.SetInt(ScreenModeKey, (int)mode);
            ApplyDisplay();
            PlayerPrefs.Save();
        }

        public void SetResolution(Vector2Int size)
        {
            if (!GetSupportedResolutions().Contains(size)) return;

            PlayerPrefs.SetInt(ResolutionWidthKey, size.x);
            PlayerPrefs.SetInt(ResolutionHeightKey, size.y);
            ApplyDisplay();
            PlayerPrefs.Save();
        }

        public void SetVSync(bool enabled)
        {
            PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
            ApplyFrameSettings();
            PlayerPrefs.Save();
        }

        public void SetFrameRateIndex(int index)
        {
            if (index < 0 || index >= FrameRates.Length) return;
            PlayerPrefs.SetInt(FrameRateKey, index);
            ApplyFrameSettings();
            PlayerPrefs.Save();
        }

        public void SetQualityIndex(int index)
        {
            if (index < 0 || index > 2) return;
            PlayerPrefs.SetInt(QualityKey, index);
            ApplyQuality();
            ApplyFrameSettings(); // 품질 프리셋의 수직 동기화 값보다 사용자 설정을 우선합니다.
            PlayerPrefs.Save();
        }

        public void SetBrightness(float value)
        {
            float brightness = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BrightnessKey, brightness);
            ApplyBrightness(brightness);
            PlayerPrefs.Save();
        }

        private void ApplySavedSettings()
        {
            ApplyQuality();
            ApplyFrameSettings();
            ApplyDisplay();
            ApplyBrightness(Brightness);
        }

        private static void ApplyDisplay()
        {
#if !UNITY_EDITOR
            Vector2Int size = SavedResolution;
            if (ScreenMode == FullScreenMode.ExclusiveFullScreen &&
                !GetSupportedResolutions().Contains(size))
            {
                size = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            }

            Screen.SetResolution(size.x, size.y, ScreenMode);
#endif
        }

        private static void ApplyFrameSettings()
        {
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
            Application.targetFrameRate = VSyncEnabled ? -1 : FrameRates[FrameRateIndex];
        }

        private static void ApplyQuality()
        {
            string[] names = QualitySettings.names;
            if (names.Length == 0) return;

            string targetName = QualityIndex == 0 ? "Low" : QualityIndex == 1 ? "Medium" : "High";
            int qualityLevel = System.Array.IndexOf(names, targetName);
            if (qualityLevel < 0) qualityLevel = Mathf.RoundToInt((names.Length - 1) * (QualityIndex / 2f));
            QualitySettings.SetQualityLevel(qualityLevel, true);
        }

        private void CreateBrightnessOverlay()
        {
            var canvasObject = new GameObject("BrightnessOverlay", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;

            var imageObject = new GameObject("Tint", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            brightnessOverlay = imageObject.GetComponent<Image>();
            brightnessOverlay.raycastTarget = false;
        }

        private void ApplyBrightness(float value)
        {
            if (brightnessOverlay == null) return;
            if (value < 0.5f)
                brightnessOverlay.color = new Color(0f, 0f, 0f, (0.5f - value) * 1.3f);
            else
                brightnessOverlay.color = new Color(1f, 1f, 1f, (value - 0.5f) * 0.4f);
        }
    }
}
