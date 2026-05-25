// VrcfQol.TypedTools.cs

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

        // =========================== Typed convenience ========================

        // ---- FlipBookPage ----

        private static readonly Regex PagePathRegex = new Regex(
            @"\.pages\.Array\.data\[(\d+)\]$", RegexOptions.Compiled);

        public static bool IsFlipbookPageProperty(SerializedProperty prop) {
            if (prop == null) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            return PagePathRegex.IsMatch(prop.propertyPath ?? "");
        }

        public static int GetFlipbookPageIndex(SerializedProperty prop) {
            if (prop == null) return -1;
            var m = PagePathRegex.Match(prop.propertyPath ?? "");
            return m.Success && int.TryParse(m.Groups[1].Value, out var i) ? i : -1;
        }

        public static void RegisterFlipbookPageTool(
            string label,
            Action<FlipbookContext> action,
            int priority = 0,
            Func<FlipbookContext, bool> enabled = null) {
            RegisterPropertyTool(
                label,
                IsFlipbookPageProperty,
                prop => {
                    if (!TryResolveFlipbookFromPage(prop, out var ctx)) return;
                    action(ctx);
                },
                priority,
                enabled == null ? null : new Func<SerializedProperty, bool>(prop => {
                    return TryResolveFlipbookFromPage(prop, out var ctx) && enabled(ctx);
                }));
        }

        // ---- FlipBookBuilderAction ----

        public static bool IsFlipbookBuilderProperty(SerializedProperty prop) {
            if (prop == null) return false;
            if (prop.propertyType != SerializedPropertyType.ManagedReference) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            var t = prop.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(t)) return false;
            return t.EndsWith(" " + Reflection.FlipbookBuilderActionType.FullName);
        }

        public static void RegisterFlipbookBuilderTool(
            string label,
            Action<FlipbookContext> action,
            int priority = 0,
            Func<FlipbookContext, bool> enabled = null) {
            RegisterPropertyTool(
                label,
                IsFlipbookBuilderProperty,
                prop => {
                    if (!TryResolveFlipbookFromBuilder(prop, out var ctx)) return;
                    action(ctx);
                },
                priority,
                enabled == null ? null : new Func<SerializedProperty, bool>(prop => {
                    return TryResolveFlipbookFromBuilder(prop, out var ctx) && enabled(ctx);
                }));
        }

        // ---- VRCFury Toggle component (right-click anywhere on the component) ----

        public static bool IsToggleContentProperty(SerializedProperty prop) {
            if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            var t = prop.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(t)) return false;
            return t.EndsWith(" " + Reflection.ToggleType.FullName);
        }

        public static void RegisterToggleTool(
            string label,
            Action<ToggleContext> action,
            int priority = 0,
            Func<ToggleContext, bool> enabled = null) {
            RegisterPropertyTool(
                label,
                IsToggleContentProperty,
                prop => {
                    if (!TryResolveToggle(prop, out var ctx)) return;
                    action(ctx);
                },
                priority,
                enabled == null ? null : new Func<SerializedProperty, bool>(prop => {
                    return TryResolveToggle(prop, out var ctx) && enabled(ctx);
                }));
        }

        // ---- Action (right-click a specific VF.Model.StateAction.* instance) ----

        public static bool IsActionProperty(SerializedProperty prop, Type actionType) {
            if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            if (actionType == null) return false;
            var t = prop.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(t)) return false;
            return t.EndsWith(" " + actionType.FullName);
        }

        public static void RegisterActionTool(
            string vrcfActionFullName,
            string label,
            Action<SerializedProperty, object> action,
            int priority = 0) {
            RegisterPropertyTool(
                label,
                prop => {
                    if (!Reflection.TryEnsure(out _)) return false;
                    var actionType = Reflection.VrcfuryAsm.GetType(vrcfActionFullName, false);
                    return actionType != null && IsActionProperty(prop, actionType);
                },
                prop => {
                    var value = prop.managedReferenceValue;
                    if (value == null) return;
                    action(prop, value);
                },
                priority);
        }

        // =========================== Inline button registry ===================

        internal sealed class InlineButtonSpec {
            public string Text;
            public string Tooltip;
            public Func<FlipbookContext, string> TextProvider;
            public Func<FlipbookContext, string> TooltipProvider;
            public Action<FlipbookContext> OnClick;
            public Func<FlipbookContext, bool> Visible;
            public Func<FlipbookContext, bool> Danger;
            public int Order;
        }

        private static readonly List<InlineButtonSpec> _inlinePageButtons = new List<InlineButtonSpec>();

        internal static IReadOnlyList<InlineButtonSpec> InlinePageButtons => _inlinePageButtons;

        public static void RegisterFlipbookPageButton(
            string text,
            string tooltip,
            Action<FlipbookContext> onClick,
            int order = 0,
            Func<FlipbookContext, bool> visible = null,
            Func<FlipbookContext, string> textProvider = null,
            Func<FlipbookContext, string> tooltipProvider = null,
            Func<FlipbookContext, bool> danger = null) {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("text is required");
            if (onClick == null) throw new ArgumentNullException(nameof(onClick));
            _inlinePageButtons.Add(new InlineButtonSpec {
                Text = text, Tooltip = tooltip, TextProvider = textProvider, TooltipProvider = tooltipProvider,
                OnClick = onClick, Visible = visible, Danger = danger, Order = order,
            });
            _inlinePageButtons.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
}
