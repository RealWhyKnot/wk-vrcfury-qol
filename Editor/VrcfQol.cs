#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol
{
    internal static partial class VrcfQol
    {
        internal delegate bool PropertyMatcher(SerializedProperty prop);
        internal delegate void PropertyToolAction(SerializedProperty prop);

        private sealed class PropEntry
        {
            public PropEntry(string label, PropertyMatcher match, PropertyToolAction action, int priority, Func<SerializedProperty, bool>? enabled)
            {
                Label = label;
                Match = match;
                Action = action;
                Priority = priority;
                Enabled = enabled;
            }

            public string Label { get; }

            public PropertyMatcher Match { get; }

            public PropertyToolAction Action { get; }

            public int Priority { get; }

            public Func<SerializedProperty, bool>? Enabled { get; }
        }

        private static readonly List<PropEntry> PropEntries = new();
        private static bool ContextHookInstalled;

        public static void RegisterPropertyTool(
            string label,
            PropertyMatcher match,
            PropertyToolAction action,
            int priority = 0,
            Func<SerializedProperty, bool>? enabled = null)
        {
            if (string.IsNullOrEmpty(label))
            {
                throw new ArgumentException("label is required", nameof(label));
            }

            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            EnsureContextHook();
            PropEntries.Add(new PropEntry(label, match, action, priority, enabled));
        }

        private static void EnsureContextHook()
        {
            if (ContextHookInstalled)
            {
                return;
            }

            ContextHookInstalled = true;
            EditorApplication.contextualPropertyMenu += OnContextMenu;
        }

        private static void OnContextMenu(GenericMenu menu, SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            bool addedSeparator = false;
            foreach (PropEntry e in PropEntries.OrderByDescending(x => x.Priority))
            {
                if (!Evaluate(e.Match.Invoke, property))
                {
                    continue;
                }

                bool enabled = e.Enabled == null || Evaluate(e.Enabled, property);
                if (!addedSeparator)
                {
                    menu.AddSeparator(string.Empty);
                    addedSeparator = true;
                }

                if (enabled)
                {
                    SerializedProperty captured = property.Copy();
                    PropertyToolAction act = e.Action;
                    menu.AddItem(new GUIContent(e.Label), on: false, () => RunAction(act, captured));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(e.Label));
                }
            }
        }

        private static bool Evaluate(Func<SerializedProperty, bool> predicate, SerializedProperty property)
        {
            try
            {
                return predicate(property);
            }
            catch (Exception ex) when (IsCallbackFailure(ex))
            {
                return false;
            }
        }

        private static void RunAction(PropertyToolAction action, SerializedProperty property)
        {
            try
            {
                action(property);
            }
            catch (Exception ex) when (IsCallbackFailure(ex))
            {
                VrcfQolLogger.Instance.Exception(ex);
            }
        }

        internal static void MarkModified(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            EditorUtility.SetDirty(target);
        }
    }
}
