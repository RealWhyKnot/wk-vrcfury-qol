// VrcfQolInspectorOverlay.cs
// Best-effort: watches the Unity Inspector and enriches recognisable VRCFury
// elements with inline UI.
//
// Currently:
//   - Each "Page #N" label inside a Flipbook Builder gets a row of buttons
//     sourced from the VrcfQol.InlinePageButtons registry. Tools register the
//     button; the overlay handles placement.
//   - Each State Action row with a bound SerializedProperty path gets a
//     "Duplicate item" button that duplicates only that one action, plus a
//     "Copy to page" picker when the action lives inside a flipbook page.
//   - Each Toggle inspector (the VRCFury component with a Toggle content) gets
//     a status banner pinned to the top of the window explaining that the
//     Global Parameter is being auto-managed, with inline Preview and opt-in/out
//     buttons.
//
// This is intentionally defensive: if VRCFury restructures its inspector, the
// overlay silently finds nothing and does nothing - the right-click context
// menu still works as the authoritative entry point.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UmeVrcfQol.Tools;
using UmeVrcfQol.Internal.Reflection;

namespace UmeVrcfQol {

    [InitializeOnLoad]
    internal static partial class VrcfQolInspectorOverlay {
        private const string InjectedClass = "vrcfqol-injected";
        private const string ButtonBarClass = "vrcfqol-buttons";
        private const string ActionToolsClass = "vrcfqol-action-tools";
        private const string ActionToolsInjectedClass = "vrcfqol-action-tools-injected";
        private const string ToggleBannerClass = "vrcfqol-toggle-banner";

        private static readonly Regex PageLabelRegex = new Regex(@"^Page #(\d+)$", RegexOptions.Compiled);
        private static readonly Regex ActionPathRegex = new Regex(
            @"(^|\.)actions\.Array\.data\[(\d+)\]$", RegexOptions.Compiled);
        private static readonly Regex FlipbookActionPathRegex = new Regex(
            @"\.pages\.Array\.data\[(\d+)\]\.state\.actions\.Array\.data\[(\d+)\]$",
            RegexOptions.Compiled);

        private const double ScanIntervalSeconds = 0.25;
        private static double _nextScan;

        static VrcfQolInspectorOverlay() {
            EditorApplication.update += Tick;
        }

        private static void Tick() {
            if (EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
            try { Scan(); } catch { /* defensive */ }
        }

        private static void Scan() {
            if (!VrcfQol.Reflection.TryEnsure(out _)) return;

            var allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var w in allWindows) {
                if (w == null) continue;
                if (w.GetType().Name != "InspectorWindow") continue;
                var root = w.rootVisualElement;
                if (root == null) continue;
                ScanRoot(root);
            }
        }

        private static void ScanRoot(VisualElement root) {
            EnsureToggleBanner(root);

            // Capture the inspector's scroll position before injecting UI.
            // VRCFury rebuilds its visual tree during drag operations, and a
            // restore avoids scroll jumps when page buttons are re-injected.
            var scroll = root.Q<ScrollView>();
            Vector2 savedScrollOffset = scroll != null ? scroll.scrollOffset : Vector2.zero;

            int injectedThisScan = 0;
            var labels = root.Query<Label>().ToList();
            foreach (var label in labels) {
                var match = PageLabelRegex.Match(label.text ?? "");
                if (!match.Success) continue;
                if (TryInjectPageButtons(label)) injectedThisScan++;
            }
            injectedThisScan += InjectActionButtons(root);

            if (injectedThisScan > 0 && scroll != null) {
                // Defer the restore one frame so any layout pass triggered by
                // our insertion has a chance to finish first; otherwise it can
                // set scrollOffset to a value that gets re-clamped immediately.
                var capturedScroll = scroll;
                var capturedOffset = savedScrollOffset;
                scroll.schedule
                    .Execute(() => capturedScroll.scrollOffset = capturedOffset)
                    .ExecuteLater(0);
            }
        }





    }
}
