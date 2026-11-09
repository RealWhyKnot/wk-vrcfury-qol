#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;

namespace UmeVrcfQol
{
    internal static partial class VrcfQol
    {
        internal static readonly Regex PagePathRegex = new(
            @"\.pages\.Array\.data\[(?<page>\d+)\]$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));

        public static bool IsFlipbookPageProperty(SerializedProperty prop)
        {
            return prop != null && Reflection.TryEnsure(out _) && PagePathRegex.IsMatch(prop.propertyPath ?? "");
        }

        public static int GetFlipbookPageIndex(SerializedProperty prop)
        {
            if (prop == null)
            {
                return -1;
            }

            Match m = PagePathRegex.Match(prop.propertyPath ?? "");
            return m.Success && int.TryParse(m.Groups["page"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : -1;
        }

        private static bool IsManagedReferenceOfType(SerializedProperty prop, Type? type)
        {
            if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference || type == null)
            {
                return false;
            }

            string t = prop.managedReferenceFullTypename;
            return !string.IsNullOrEmpty(t) && t.EndsWith(" " + type.FullName, StringComparison.Ordinal);
        }

        public static void RegisterFlipbookPageTool(
            string label,
            Action<FlipbookContext> action,
            int priority = 0,
            Func<FlipbookContext, bool>? enabled = null)
        {
            RegisterPropertyTool(
                label,
                IsFlipbookPageProperty,
                prop =>
                {
                    if (!TryResolveFlipbookFromPage(prop, out FlipbookContext ctx))
                    {
                        return;
                    }

                    action(ctx);
                },
                priority,
                enabled == null
                    ? null
                    : new Func<SerializedProperty, bool>(prop => TryResolveFlipbookFromPage(prop, out FlipbookContext ctx) && enabled(ctx)));
        }

        public static bool IsFlipbookBuilderProperty(SerializedProperty prop)
        {
            ReflectionCache r = Reflection;
            return r.TryEnsure(out _) && IsManagedReferenceOfType(prop, r.FlipbookBuilderActionType);
        }

        public static void RegisterFlipbookBuilderTool(
            string label,
            Action<FlipbookContext> action,
            int priority = 0,
            Func<FlipbookContext, bool>? enabled = null)
        {
            RegisterPropertyTool(
                label,
                IsFlipbookBuilderProperty,
                prop =>
                {
                    if (!TryResolveFlipbookFromBuilder(prop, out FlipbookContext ctx))
                    {
                        return;
                    }

                    action(ctx);
                },
                priority,
                enabled == null
                    ? null
                    : new Func<SerializedProperty, bool>(prop => TryResolveFlipbookFromBuilder(prop, out FlipbookContext ctx) && enabled(ctx)));
        }

        public static bool IsToggleContentProperty(SerializedProperty prop)
        {
            ReflectionCache r = Reflection;
            return r.TryEnsure(out _) && IsManagedReferenceOfType(prop, r.ToggleType);
        }

        public static void RegisterToggleTool(
            string label,
            Action<ToggleContext> action,
            int priority = 0,
            Func<ToggleContext, bool>? enabled = null)
        {
            RegisterPropertyTool(
                label,
                IsToggleContentProperty,
                prop =>
                {
                    if (!TryResolveToggle(prop, out ToggleContext ctx))
                    {
                        return;
                    }

                    action(ctx);
                },
                priority,
                enabled == null
                    ? null
                    : new Func<SerializedProperty, bool>(prop => TryResolveToggle(prop, out ToggleContext ctx) && enabled(ctx)));
        }

        public static bool IsActionProperty(SerializedProperty prop, Type actionType)
        {
            return Reflection.TryEnsure(out _) && IsManagedReferenceOfType(prop, actionType);
        }

        public static void RegisterActionTool(
            string vrcfActionFullName,
            string label,
            Action<SerializedProperty, object> action,
            int priority = 0)
        {
            RegisterPropertyTool(
                label,
                prop =>
                {
                    ReflectionCache r = Reflection;
                    if (!r.TryEnsure(out _))
                    {
                        return false;
                    }

                    Type? actionType = r.VrcfuryAsm.GetType(vrcfActionFullName, throwOnError: false);
                    return actionType != null && IsActionProperty(prop, actionType);
                },
                prop =>
                {
                    object value = prop.managedReferenceValue;
                    if (value == null)
                    {
                        return;
                    }

                    action(prop, value);
                },
                priority);
        }

        internal sealed class InlineButtonSpec
        {
            public InlineButtonSpec(string text, string tooltip, Action<FlipbookContext> onClick)
            {
                Text = text;
                Tooltip = tooltip;
                OnClick = onClick;
            }

            public string Text { get; }

            public string Tooltip { get; }

            public Action<FlipbookContext> OnClick { get; }

            public Func<FlipbookContext, string>? TextProvider { get; set; }

            public Func<FlipbookContext, string>? TooltipProvider { get; set; }

            public Func<FlipbookContext, bool>? Visible { get; set; }

            public Func<FlipbookContext, bool>? Danger { get; set; }

            public int Order { get; set; }
        }

        private static readonly List<InlineButtonSpec> InlinePageButtonSpecs = new();

        internal static IReadOnlyList<InlineButtonSpec> InlinePageButtons => InlinePageButtonSpecs;

        public static void RegisterFlipbookPageButton(
            string text,
            string tooltip,
            Action<FlipbookContext> onClick,
            int order = 0,
            Func<FlipbookContext, bool>? visible = null,
            Func<FlipbookContext, string>? textProvider = null,
            Func<FlipbookContext, string>? tooltipProvider = null,
            Func<FlipbookContext, bool>? danger = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("text is required", nameof(text));
            }

            if (onClick == null)
            {
                throw new ArgumentNullException(nameof(onClick));
            }

            InlinePageButtonSpecs.Add(new InlineButtonSpec(text, tooltip, onClick)
            {
                TextProvider = textProvider,
                TooltipProvider = tooltipProvider,
                Visible = visible,
                Danger = danger,
                Order = order,
            });
            InlinePageButtonSpecs.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
}
