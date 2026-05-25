// VrcfQol.cs
// Core framework for VRCFury QoL tools.
//
// This file provides three things to tool authors:
//
//   1. VrcfQol.Reflection   - lazily-resolved reflection handles for VRCFury's
//                             internal types (VRCFury component, Toggle feature,
//                             State, FlipBookBuilderAction, FlipBookPage).
//
//   2. Registration API     - small, typed helpers so a new tool is usually one
//                             file with one [InitializeOnLoad] registration call:
//                                RegisterPropertyTool         (generic, by SerializedProperty)
//                                RegisterFlipbookPageTool     (page right-click)
//                                RegisterFlipbookPageButton   (page inline button)
//                                RegisterFlipbookBuilderTool  (builder right-click)
//                                RegisterToggleTool           (VRCFury Toggle right-click)
//                                RegisterActionTool           (generic action right-click)
//
//   3. Helpers              - page clipboard for copy/paste, path formatting,
//                             flipbook resolution from a SerializedProperty,
//                             deep-clone of a FlipBookPage.
//
// The inspector overlay (VrcfQolInspectorOverlay.cs) reads from the page-button
// registry to render inline buttons next to each page row. Nothing else touches
// the inspector's visual tree directly - tools just register and let the overlay
// handle placement.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol {

    internal static partial class VrcfQol {

        // ============================== Property tools =========================

        internal delegate bool PropertyMatcher(SerializedProperty prop);
        internal delegate void PropertyToolAction(SerializedProperty prop);

        private sealed class PropEntry {
            public string Label;
            public PropertyMatcher Match;
            public PropertyToolAction Action;
            public int Priority;
            public Func<SerializedProperty, bool> Enabled; // optional, greys out when false
        }

        private static readonly List<PropEntry> _propEntries = new List<PropEntry>();
        private static bool _contextHookInstalled;

        public static void RegisterPropertyTool(
            string label,
            PropertyMatcher match,
            PropertyToolAction action,
            int priority = 0,
            Func<SerializedProperty, bool> enabled = null) {
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("label is required");
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (action == null) throw new ArgumentNullException(nameof(action));
            EnsureContextHook();
            _propEntries.Add(new PropEntry {
                Label = label, Match = match, Action = action,
                Priority = priority, Enabled = enabled,
            });
        }

        private static void EnsureContextHook() {
            if (_contextHookInstalled) return;
            _contextHookInstalled = true;
            EditorApplication.contextualPropertyMenu += OnContextMenu;
        }

        private static void OnContextMenu(GenericMenu menu, SerializedProperty property) {
            if (property == null) return;
            bool addedSeparator = false;
            foreach (var e in _propEntries.OrderByDescending(x => x.Priority)) {
                bool matched;
                try { matched = e.Match(property); } catch { matched = false; }
                if (!matched) continue;

                bool enabled = true;
                if (e.Enabled != null) {
                    try { enabled = e.Enabled(property); } catch { enabled = false; }
                }

                if (!addedSeparator) {
                    menu.AddSeparator(string.Empty);
                    addedSeparator = true;
                }

                var captured = property.Copy();
                var act = e.Action;
                if (enabled) {
                    menu.AddItem(new GUIContent(e.Label), false, () => {
                        try { act(captured); } catch (Exception ex) { VrcfQolLogger.Instance.Exception(ex); }
                    });
                } else {
                    menu.AddDisabledItem(new GUIContent(e.Label));
                }
            }
        }




    }
}
