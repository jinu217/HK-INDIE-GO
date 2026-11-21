using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI
{
    public sealed class HelpPanelView : MonoBehaviour
    {
        [SerializeField] private HelpContentData content;
        [SerializeField] private Image contentImage;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button rulesButton;
        [SerializeField] private Button charactersButton;
        [SerializeField] private Button mapsButton;
        [SerializeField] private Button modesButton;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rulesButton != null) rulesButton.onClick.AddListener(ShowRules);
            if (charactersButton != null) charactersButton.onClick.AddListener(ShowCharacters);
            if (mapsButton != null) mapsButton.onClick.AddListener(ShowMaps);
            if (modesButton != null) modesButton.onClick.AddListener(ShowModes);
            if (contentImage != null && contentImage.sprite == null) ShowRules();
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (rulesButton != null) rulesButton.onClick.RemoveListener(ShowRules);
            if (charactersButton != null) charactersButton.onClick.RemoveListener(ShowCharacters);
            if (mapsButton != null) mapsButton.onClick.RemoveListener(ShowMaps);
            if (modesButton != null) modesButton.onClick.RemoveListener(ShowModes);
        }

        public void Close() => gameObject.SetActive(false);
        private void ShowRules() => Show(content != null ? content.yutRules : null);
        private void ShowCharacters() => Show(content != null ? content.characters : null);
        private void ShowMaps() => Show(content != null ? content.maps : null);
        private void ShowModes() => Show(content != null ? content.modes : null);
        private void Show(Sprite sprite)
        {
            if (contentImage == null || sprite == null) return;
            contentImage.sprite = sprite;
            contentImage.preserveAspect = true;
            contentImage.enabled = true;
        }
    }
}
