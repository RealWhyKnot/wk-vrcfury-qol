// VrcfQolInspectorOverlay.PageButtons.cs

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

    internal static partial class VrcfQolInspectorOverlay {

        // ----------------------------------------------------------------------
        // Per-page inline buttons
        // ----------------------------------------------------------------------

        // Returns true if buttons were freshly injected (caller uses this to
        // decide whether the inspector's scroll position should be restored).
        private static bool TryInjectPageButtons(Label pageLabel) {
            pageLabel.AddToClassList(InjectedClass);

            var parent = pageLabel.parent;
            if (parent == null) return false;

            var specs = VrcfQol.InlinePageButtons;
            if (specs == null || specs.Count == 0) return false;

            var buttons = FindPageButtonBar(parent, pageLabel);
            var inserted = false;
            if (buttons == null) {
                // Sibling-insert approach: keep the label exactly where it is and
                // just append a button container as the next sibling. Doing it this
                // way (rather than removing the label and re-parenting it under a
                // wrapper row) avoids two extra mutations to the visual tree, which
                // is what was triggering scroll resets on every overlay tick during
                // a flipbook page drag.
                buttons = new VisualElement();
                buttons.AddToClassList(ButtonBarClass);
                buttons.userData = pageLabel;
                buttons.style.flexDirection = FlexDirection.Row;
                buttons.style.alignItems = Align.Center;
                buttons.style.justifyContent = Justify.FlexEnd;
                buttons.style.flexShrink = 0;
                buttons.style.marginLeft = 8;
                buttons.style.marginTop = 1;
                buttons.style.marginBottom = 1;

                foreach (var spec in specs) {
                    var capturedSpec = spec;
                    var btn = new Button(() => OnInlineButtonClicked(pageLabel, capturedSpec)) {
                        text = spec.Text,
                    };
                    btn.userData = spec;
                    btn.tooltip = spec.Tooltip ?? string.Empty;
                    EditorElementWalker.ApplyInlineButtonStyle(btn);
                    buttons.Add(btn);
                }

                int labelIndex = parent.IndexOf(pageLabel);
                parent.Insert(labelIndex + 1, buttons);
                inserted = true;
            }

            UpdatePageButtons(pageLabel, buttons);
            return inserted;
        }

        private static void OnInlineButtonClicked(Label pageLabel, VrcfQol.InlineButtonSpec spec) {
            if (!TryResolvePageContext(pageLabel, out var ctx, out var error)) {
                EditorUtility.DisplayDialog("WhyKnot wk-vrcfury-qol", error, "OK");
                return;
            }

            if (spec.Visible != null) {
                bool vis;
                try { vis = spec.Visible(ctx); } catch { vis = true; }
                if (!vis) {
                    EditorUtility.DisplayDialog("WhyKnot wk-vrcfury-qol",
                        "This action is not available for this page right now.", "OK");
                    return;
                }
            }

            try {
                spec.OnClick(ctx);
            } catch (System.Exception ex) {
                VrcfQolLogger.Instance.Exception(ex);
                EditorUtility.DisplayDialog("WhyKnot wk-vrcfury-qol",
                    "Action failed. See Console.\n\n" + ex.Message, "OK");
            }
        }

        private static bool TryResolvePageContext(Label pageLabel, out VrcfQol.FlipbookContext ctx, out string error) {
            ctx = default;
            error = null;
            var match = PageLabelRegex.Match(pageLabel.text ?? "");
            if (!match.Success) {
                error = "Could not determine which flipbook page this button belongs to.";
                return false;
            }
            if (!int.TryParse(match.Groups[1].Value, out var oneBasedIndex)) {
                error = "Could not determine which flipbook page this button belongs to.";
                return false;
            }
            var sourceIndex = oneBasedIndex - 1;

            var selection = Selection.activeGameObject;
            if (selection == null) {
                error = "Could not determine which GameObject is inspected. Select it in the Hierarchy and try again.";
                return false;
            }
            if (!VrcfQol.Reflection.TryEnsure(out var err)) {
                error = err;
                return false;
            }
            var r = VrcfQol.Reflection;

            foreach (Component c in selection.GetComponents(r.VRCFuryType)) {
                if (c == null) continue;
                if (!VrcfQol.TryResolveFlipbookFromComponent(c, out var resolved)) continue;
                if (resolved.pages == null || sourceIndex < 0 || sourceIndex >= resolved.pages.Count) continue;
                resolved.pageIndex = sourceIndex;
                ctx = resolved;
                return true;
            }

            error =
                "Could not find a flipbook toggle on the selected GameObject that contains this page. " +
                "If the flipbook is on a different object than the current selection, use right-click WhyKnot / wk-vrcfury-qol instead.";
            return false;
        }

        private static VisualElement FindPageButtonBar(VisualElement parent, Label pageLabel) {
            for (int i = 0; i < parent.childCount; i++) {
                var child = parent[i];
                if (child != null &&
                    child.ClassListContains(ButtonBarClass) &&
                    ReferenceEquals(child.userData, pageLabel)) {
                    return child;
                }
            }
            return null;
        }

        private static void UpdatePageButtons(Label pageLabel, VisualElement buttons) {
            var hasContext = TryResolvePageContext(pageLabel, out var ctx, out _);
            for (int i = 0; i < buttons.childCount; i++) {
                var btn = buttons[i] as Button;
                var spec = btn?.userData as VrcfQol.InlineButtonSpec;
                if (btn == null || spec == null) continue;

                var visible = true;
                if (hasContext && spec.Visible != null) {
                    try { visible = spec.Visible(ctx); } catch { visible = true; }
                }
                btn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

                btn.text = spec.Text;
                if (hasContext && spec.TextProvider != null) {
                    try {
                        var text = spec.TextProvider(ctx);
                        if (!string.IsNullOrEmpty(text)) btn.text = text;
                    } catch {
                        btn.text = spec.Text;
                    }
                }

                btn.tooltip = spec.Tooltip ?? string.Empty;
                if (hasContext && spec.TooltipProvider != null) {
                    try {
                        var tooltip = spec.TooltipProvider(ctx);
                        if (!string.IsNullOrEmpty(tooltip)) btn.tooltip = tooltip;
                    } catch {
                        btn.tooltip = spec.Tooltip ?? string.Empty;
                    }
                }

                var danger = false;
                if (hasContext && spec.Danger != null) {
                    try { danger = spec.Danger(ctx); } catch { danger = false; }
                }
                EditorElementWalker.ApplyDangerButtonStyle(btn, danger);
            }
        }
    }
}
