using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YutArena.Managers
{
    /// <summary>Persistent presentation speed and UI text size settings.</summary>
    public sealed class GeneralSettingsManager : MonoBehaviour
    {
        private const string PresentationSpeedKey = "PRESENTATION_SPEED";
        private const string TextSizeKey = "UI_TEXT_SIZE";
        private static readonly float[] TextScales = { 1f, 1.2f, 1.4f };
        private static GeneralSettingsManager instance;

        private readonly Dictionary<TMP_Text, TextSizeState> tmpTexts = new Dictionary<TMP_Text, TextSizeState>();
        private readonly Dictionary<Text, LegacyTextSizeState> legacyTexts = new Dictionary<Text, LegacyTextSizeState>();
        private float nextRefresh;

        private sealed class TextSizeState
        {
            public float baseSize;
            public float baseMin;
            public float baseMax;
            public float appliedSize;
        }

        private sealed class LegacyTextSizeState
        {
            public int baseSize;
            public int appliedSize;
        }

        public static GeneralSettingsManager Instance => instance;
        public static int PresentationSpeedIndex => Mathf.Clamp(PlayerPrefs.GetInt(PresentationSpeedKey, 0), 0, 1);
        public static int TextSizeIndex => Mathf.Clamp(PlayerPrefs.GetInt(TextSizeKey, 0), 0, 2);
        public static float PresentationSpeedMultiplier => PresentationSpeedIndex == 0 ? 1f : 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance == null) new GameObject(nameof(GeneralSettingsManager)).AddComponent<GeneralSettingsManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            nextRefresh = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            RefreshTextSizes();
        }

        public void SetPresentationSpeed(int index)
        {
            if (index < 0 || index > 1) return;
            PlayerPrefs.SetInt(PresentationSpeedKey, index);
            PlayerPrefs.Save();
        }

        public void SetTextSize(int index)
        {
            if (index < 0 || index > 2) return;
            PlayerPrefs.SetInt(TextSizeKey, index);
            PlayerPrefs.Save();
            RefreshTextSizes();
        }

        public void RefreshTextSizes()
        {
            float scale = TextScales[TextSizeIndex];
            foreach (TMP_Text label in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (label == null || !label.gameObject.scene.IsValid() || !label.gameObject.scene.isLoaded) continue;
                if (!tmpTexts.TryGetValue(label, out TextSizeState state))
                {
                    state = new TextSizeState
                    {
                        baseSize = label.fontSize,
                        baseMin = label.fontSizeMin,
                        baseMax = label.fontSizeMax
                    };
                    tmpTexts[label] = state;
                }
                else if (state.appliedSize > 0f && !Mathf.Approximately(label.fontSize, state.appliedSize))
                {
                    state.baseSize = label.fontSize;
                    state.baseMin = label.fontSizeMin;
                    state.baseMax = label.fontSizeMax;
                }

                label.fontSize = state.baseSize * scale;
                if (label.enableAutoSizing)
                {
                    label.fontSizeMin = state.baseMin * scale;
                    label.fontSizeMax = state.baseMax * scale;
                }
                state.appliedSize = label.fontSize;
            }

            foreach (Text label in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (label == null || !label.gameObject.scene.IsValid() || !label.gameObject.scene.isLoaded) continue;
                if (!legacyTexts.TryGetValue(label, out LegacyTextSizeState state))
                {
                    state = new LegacyTextSizeState { baseSize = label.fontSize };
                    legacyTexts[label] = state;
                }
                else if (state.appliedSize > 0 && label.fontSize != state.appliedSize)
                {
                    state.baseSize = label.fontSize;
                }

                label.fontSize = Mathf.Max(1, Mathf.RoundToInt(state.baseSize * scale));
                state.appliedSize = label.fontSize;
            }
        }
    }
}
