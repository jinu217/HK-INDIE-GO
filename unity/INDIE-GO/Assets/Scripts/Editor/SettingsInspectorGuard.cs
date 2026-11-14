#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YutArena.UI;

namespace YutArena.EditorTools
{
    /// <summary>Detach inspectors from generated UI before its GameObjects or Mask materials disappear.</summary>
    [InitializeOnLoad]
    public static class SettingsInspectorGuard
    {
        private static readonly Type InspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        private static readonly PropertyInfo TrackerProperty = InspectorType?.GetProperty("tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        static SettingsInspectorGuard()
        {
            SettingsPanel.GeneratedObjectWillBeRemoved -= BeforeRemove;
            SettingsPanel.GeneratedObjectWillBeRemoved += BeforeRemove;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += RepairInvalidInspectors;
        }
        private static IEnumerable<ActiveEditorTracker> Trackers()
        {
            var seen = new HashSet<ActiveEditorTracker>();
            if (seen.Add(ActiveEditorTracker.sharedTracker)) yield return ActiveEditorTracker.sharedTracker;
            if (InspectorType == null || TrackerProperty == null) yield break;
            foreach (var window in Resources.FindObjectsOfTypeAll(InspectorType))
            {
                ActiveEditorTracker tracker = null;
                try { tracker = TrackerProperty.GetValue(window) as ActiveEditorTracker; }
                catch (TargetInvocationException) { }
                if (tracker != null && seen.Add(tracker)) yield return tracker;
            }
        }
        private static Transform ObjectTransform(UnityEngine.Object target)
        {
            if (target is GameObject go && go != null) return go.transform;
            if (target is Component component && component != null) return component.transform;
            return null;
        }
        private static bool IsRemoved(UnityEngine.Object target, GameObject root)
        {
            if (target == null) return true;
            var transform = ObjectTransform(target);
            return transform != null && (transform == root.transform || transform.IsChildOf(root.transform));
        }
        private static void BeforeRemove(GameObject root)
        {
            if (root == null) return;
            var affected = Trackers().Where(tracker => tracker.activeEditors.Any(editor => editor != null && editor.targets.Any(target => IsRemoved(target, root)))).ToArray();
            var selected = Selection.objects;
            if (selected.Any(target => IsRemoved(target, root)))
            {
                var remaining = selected.Where(target => !IsRemoved(target, root)).ToArray();
                if (remaining.Length == 0)
                {
                    var owner = root.GetComponentInParent<SettingsPanel>();
                    var matchOwner = root.GetComponentInParent<InGameSettingsPanel>();
                    UnityEngine.Object fallback = owner != null && owner.gameObject != root ? owner.gameObject : Resources.Load<SettingsTheme>("SettingsTheme");
                    if (matchOwner != null && matchOwner.gameObject != root) fallback = matchOwner.gameObject;
                    remaining = fallback != null ? new[] { fallback } : Array.Empty<UnityEngine.Object>();
                }
                Selection.objects = remaining;
            }
            foreach (var tracker in affected)
            {
                // Only unlock inspectors whose target is about to be removed.
                tracker.isLocked = false;
                Rebuild(tracker);
            }
        }
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += RepairInvalidInspectors;
                return;
            }
            if (state != PlayModeStateChange.ExitingPlayMode && state != PlayModeStateChange.ExitingEditMode) return;
            var panels = UnityEngine.Object.FindObjectsByType<SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var panel in panels) BeforeRemove(panel.gameObject);
            foreach (var panel in UnityEngine.Object.FindObjectsByType<InGameSettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BeforeRemove(panel.gameObject);
            // Scene UI (including a locked dropdown Inspector) is replaced when Play changes mode.
            var inspected = Selection.objects.Concat(Trackers().SelectMany(tracker => tracker.activeEditors)
                .Where(editor => editor != null).SelectMany(editor => editor.targets));
            var ui = inspected.Where(target => target != null && !EditorUtility.IsPersistent(target))
                .Select(ObjectTransform).Where(transform => transform is RectTransform && transform.gameObject.scene.IsValid())
                .Select(transform => transform.gameObject).Distinct().ToArray();
            foreach (var root in ui) BeforeRemove(root);
        }
        public static void RepairInvalidInspectors()
        {
            Selection.objects = Selection.objects.Where(target => target != null).ToArray();
            foreach (var tracker in Trackers())
            {
                if (!tracker.activeEditors.Any(editor => editor != null && editor.targets.Any(target => target == null))) continue;
                tracker.isLocked = false;
                Rebuild(tracker);
            }
        }
        public static void RefreshInspectors()
        {
            RepairInvalidInspectors();
            foreach (var tracker in Trackers()) Rebuild(tracker);
        }
        private static void Rebuild(ActiveEditorTracker tracker)
        {
            try { tracker.ForceRebuild(); }
            catch (MissingReferenceException) { EditorApplication.delayCall += RepairInvalidInspectors; }
        }
    }
}
#endif
