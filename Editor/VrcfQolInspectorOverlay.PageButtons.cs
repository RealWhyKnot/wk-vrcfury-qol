#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UmeVrcfQol.Internal.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmeVrcfQol
{
    internal static partial class VrcfQolInspectorOverlay
    {
        private const string DialogTitle = "WhyKnot wk-vrcfury-qol";
        private const string UnknownPageMessage = "Could not determine which flipbook page this button belongs to.";

        private static bool TryInjectPageButtons(Label pageLabel)
        {
            pageLabel.AddToClassList(InjectedClass);

            VisualElement parent = pageLabel.parent;
            if (parent == null)
            {
                return false;
            }

            IReadOnlyList<VrcfQol.InlineButtonSpec> specs = VrcfQol.InlinePageButtons;
            if (specs.Count == 0)
            {
                return false;
            }

            VisualElement? buttons = FindPageButtonBar(parent, pageLabel);
            bool inserted = false;
            if (buttons == null)
            {
                buttons = BuildPageButtonBar(pageLabel, specs);
                parent.Insert(parent.IndexOf(pageLabel) + 1, buttons);
                inserted = true;
            }

            UpdatePageButtons(pageLabel, buttons);
            return inserted;
        }

        private static VisualElement BuildPageButtonBar(Label pageLabel, IReadOnlyList<VrcfQol.InlineButtonSpec> specs)
        {
            var buttons = new VisualElement();
            buttons.AddToClassList(ButtonBarClass);
            buttons.userData = pageLabel;
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.alignItems = Align.Center;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.flexShrink = 0;
            buttons.style.marginLeft = 8;
            buttons.style.marginTop = 1;
            buttons.style.marginBottom = 1;

            foreach (VrcfQol.InlineButtonSpec spec in specs)
            {
                VrcfQol.InlineButtonSpec capturedSpec = spec;
                var btn = new Button(() => OnInlineButtonClicked(pageLabel, capturedSpec))
                {
                    text = spec.Text,
                    userData = spec,
                    tooltip = spec.Tooltip,
                };
                EditorElementWalker.ApplyInlineButtonStyle(btn);
                buttons.Add(btn);
            }

            return buttons;
        }

        private static bool Evaluate(Func<VrcfQol.FlipbookContext, bool>? predicate, VrcfQol.FlipbookContext ctx, bool fallback)
        {
            if (predicate == null)
            {
                return fallback;
            }

            try
            {
                return predicate(ctx);
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                return fallback;
            }
        }

        private static string Describe(Func<VrcfQol.FlipbookContext, string>? provider, VrcfQol.FlipbookContext ctx, string fallback)
        {
            if (provider == null)
            {
                return fallback;
            }

            try
            {
                string text = provider(ctx);
                return string.IsNullOrEmpty(text) ? fallback : text;
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                return fallback;
            }
        }

        private static void OnInlineButtonClicked(Label pageLabel, VrcfQol.InlineButtonSpec spec)
        {
            if (!TryResolvePageContext(pageLabel, out VrcfQol.FlipbookContext ctx, out string? error))
            {
                EditorUtility.DisplayDialog(DialogTitle, error, "OK");
                return;
            }

            if (!Evaluate(spec.Visible, ctx, fallback: true))
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "This action is not available for this page right now.", "OK");
                return;
            }

            try
            {
                spec.OnClick(ctx);
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                VrcfQolLogger.Instance.Exception(ex);
                EditorUtility.DisplayDialog(DialogTitle,
                    "Action failed. See Console.\n\n" + ex.Message, "OK");
            }
        }

        private static bool TryResolvePageContext(Label pageLabel, out VrcfQol.FlipbookContext ctx, out string? error)
        {
            ctx = default;
            error = null;
            Match match = PageLabelRegex.Match(pageLabel.text ?? "");
            if (!match.Success
                || !int.TryParse(match.Groups["page"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int oneBasedIndex))
            {
                error = UnknownPageMessage;
                return false;
            }

            string? pagePath = FindPageBindingPath(pageLabel);
            if (pagePath != null)
            {
                Component? owner = FindOwningComponent(pageLabel);
                if (owner != null && VrcfQol.TryResolveFlipbookFromPagePath(owner, pagePath, out ctx))
                {
                    return true;
                }
            }

            return TryResolvePageFromSelection(oneBasedIndex - 1, out ctx, out error);
        }

        private static bool TryResolvePageFromSelection(int sourceIndex, out VrcfQol.FlipbookContext ctx, out string? error)
        {
            ctx = default;
            GameObject selection = Selection.activeGameObject;
            if (selection == null)
            {
                error = "Could not determine which GameObject is inspected. Select it in the Hierarchy and try again.";
                return false;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out error))
            {
                return false;
            }

            int candidates = 0;
            foreach (Component c in selection.GetComponents(r.VRCFuryType))
            {
                if (c == null || !VrcfQol.TryResolveFlipbookFromComponent(c, out VrcfQol.FlipbookContext resolved))
                {
                    continue;
                }

                if (resolved.Pages == null || sourceIndex < 0 || sourceIndex >= resolved.Pages.Count)
                {
                    continue;
                }

                resolved.PageIndex = sourceIndex;
                ctx = resolved;
                candidates++;
            }

            if (candidates == 1)
            {
                return true;
            }

            ctx = default;
            error = candidates > 1
                ? "Could not tell which flipbook this page belongs to. Right-click the page and use WhyKnot / wk-vrcfury-qol instead."
                : "Could not find a flipbook toggle on the selected GameObject that contains this page. " +
                  "If the flipbook is on a different object than the current selection, use right-click WhyKnot / wk-vrcfury-qol instead.";
            return false;
        }

        private static string? FindPageBindingPath(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current is PropertyField field && VrcfQol.PagePathRegex.IsMatch(field.bindingPath ?? ""))
                {
                    return field.bindingPath;
                }
            }
            return null;
        }

        private static VisualElement? FindPageButtonBar(VisualElement parent, Label pageLabel)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                VisualElement child = parent[i];
                if (child != null &&
                    child.ClassListContains(ButtonBarClass) &&
                    ReferenceEquals(child.userData, pageLabel))
                {
                    return child;
                }
            }
            return null;
        }

        private static void UpdatePageButtons(Label pageLabel, VisualElement buttons)
        {
            bool hasContext = TryResolvePageContext(pageLabel, out VrcfQol.FlipbookContext ctx, out _);
            for (int i = 0; i < buttons.childCount; i++)
            {
                if (buttons[i] is not Button btn || btn.userData is not VrcfQol.InlineButtonSpec spec)
                {
                    continue;
                }

                bool visible = !hasContext || Evaluate(spec.Visible, ctx, fallback: true);
                btn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                btn.text = hasContext ? Describe(spec.TextProvider, ctx, spec.Text) : spec.Text;
                btn.tooltip = hasContext ? Describe(spec.TooltipProvider, ctx, spec.Tooltip) : spec.Tooltip;
                EditorElementWalker.ApplyDangerButtonStyle(btn, hasContext && Evaluate(spec.Danger, ctx, fallback: false));
            }
        }
    }
}
