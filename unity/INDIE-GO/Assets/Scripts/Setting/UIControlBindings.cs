using System;
using System.Collections.Generic;
using UnityEngine;

namespace YutArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UIControlBindings : MonoBehaviour
    {
        [Serializable]
        public struct Reference
        {
            public string role;
            public Component target;
        }

        [SerializeField] private Reference[] references = Array.Empty<Reference>();
        private readonly Dictionary<(string, Type), Component> cache = new Dictionary<(string, Type), Component>();

        public T Get<T>(string role) where T : Component
        {
            var key = (role, typeof(T));
            if (cache.TryGetValue(key, out var found) && found != null) return (T)found;
            foreach (var reference in references)
                if (reference.role == role && reference.target is T target)
                {
                    cache[key] = target;
                    return target;
                }
            throw new InvalidOperationException($"UI reference '{role}' ({typeof(T).Name}) is missing on {name}.");
        }

#if UNITY_EDITOR
        public void CaptureReferences()
        {
            var existing = new Dictionary<Component, string>();
            foreach (var entry in references)
                if (entry.target != null) existing[entry.target] = entry.role;
            var result = new List<Reference>();
            void Capture(Transform node)
            {
                foreach (var target in node.GetComponents<Component>())
                {
                    bool control = target is UnityEngine.UI.Selectable || target is UnityEngine.UI.Graphic || target is UnityEngine.UI.ScrollRect;
                    if (control) result.Add(new Reference { role = existing.TryGetValue(target, out var role) ? role : target.name, target = target });
                }
                if (node != transform && node.GetComponent<UIControlBindings>() != null) return;
                foreach (Transform child in node) Capture(child);
            }
            Capture(transform);
            references = result.ToArray(); cache.Clear();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
