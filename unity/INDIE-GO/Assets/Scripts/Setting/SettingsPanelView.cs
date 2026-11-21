using System;
using UnityEngine;
using UnityEngine.UI;

namespace YutArena.UI
{
    /// <summary>Authored layout shared by the scene instance and its reusable prefab.</summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        public RectTransform window;
        public GameObject[] pages;
        public Button[] tabs;
        public GameObject[] confirmationTemplates;
        public GameObject dropdownTemplate;

        public T Control<T>(string name) where T : Component => Control<T>(transform, name);

        public static T Control<T>(Transform parent, string name) where T : Component
        {
            var bindings = parent.GetComponent<UIControlBindings>();
            if (bindings == null) throw new InvalidOperationException($"UI reference table is missing on {parent.name}.");
            return bindings.Get<T>(name);
        }

        public RectTransform ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == index);
            return (RectTransform)pages[index].transform;
        }

        public static void Click(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }
    }
}
