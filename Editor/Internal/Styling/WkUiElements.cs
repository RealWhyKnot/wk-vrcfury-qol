// WkUiElements.cs
//
// UI Toolkit equivalent of the IMGUI primitive set in WkStyles. Every
// factory returns a VisualElement that carries a stable BEM USS class
// set (wk-notice, wk-section, wk-pill, wk-button--primary, ...) so
// downstream styling can over-ride without monkey-patching, and the
// colours come entirely from --wk-color-* variables defined by the
// theme stylesheets.
//
// Theme switching is a class swap (ApplyTheme adds wk-theme--whyknot or
// wk-theme--vrcfury to a root element). Skin switching is a parallel
// wk-skin--pro / wk-skin--personal class (ApplySkinClass picks based on
// EditorGUIUtility.isProSkin at construction time -- callers that need
// to react to a runtime skin flip can register an
// EditorApplication.update poller). No per-element repaint code: USS
// variable resolution handles propagation automatically when the root
// class changes.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmeVrcfQol.Internal.Styling {

    public static class WkUiElements {

        // ---- USS class set --------------------------------------------------

        public const string ClassNotice         = "wk-notice";
        public const string ClassNoticeInfo     = "wk-notice--info";
        public const string ClassNoticeWarning  = "wk-notice--warning";
        public const string ClassNoticeSuccess  = "wk-notice--success";
        public const string ClassNoticeDanger   = "wk-notice--danger";
        public const string ClassNoticeAction   = "wk-notice__action";

        public const string ClassSection        = "wk-section";
        public const string ClassSectionTitle   = "wk-section__title";
        public const string ClassSectionBody    = "wk-section__body";

        public const string ClassPill           = "wk-pill";
        public const string ClassPillInfo       = "wk-pill--info";
        public const string ClassPillWarning    = "wk-pill--warning";
        public const string ClassPillSuccess    = "wk-pill--success";
        public const string ClassPillDanger     = "wk-pill--danger";

        public const string ClassDivider        = "wk-divider";
        public const string ClassDividerSubtle  = "wk-divider--subtle";

        public const string ClassButtonPrimary  = "wk-button--primary";
        public const string ClassButtonDanger   = "wk-button--danger";
        public const string ClassButtonSecondary= "wk-button--secondary";

        public const string ClassHelpIcon       = "wk-help-icon";
        public const string ClassToolbarSearch  = "wk-toolbar__search";
        public const string ClassRow            = "wk-row-list__row";
        public const string ClassRowStriped     = "wk-row-list__row--striped";

        public const string ClassThemeWhyKnot   = "wk-theme--whyknot";
        public const string ClassThemeVRCFury   = "wk-theme--vrcfury";
        public const string ClassSkinPro        = "wk-skin--pro";
        public const string ClassSkinPersonal   = "wk-skin--personal";

        public static readonly IReadOnlyList<string> AllClasses = new[] {
            ClassNotice, ClassNoticeInfo, ClassNoticeWarning, ClassNoticeSuccess, ClassNoticeDanger, ClassNoticeAction,
            ClassSection, ClassSectionTitle, ClassSectionBody,
            ClassPill, ClassPillInfo, ClassPillWarning, ClassPillSuccess, ClassPillDanger,
            ClassDivider, ClassDividerSubtle,
            ClassButtonPrimary, ClassButtonDanger, ClassButtonSecondary,
            ClassHelpIcon, ClassToolbarSearch, ClassRow, ClassRowStriped,
            ClassThemeWhyKnot, ClassThemeVRCFury, ClassSkinPro, ClassSkinPersonal,
        };

        public static readonly IReadOnlyList<string> AllVariables = new[] {
            "--wk-color-background", "--wk-color-background-alt", "--wk-color-background-emphasis",
            "--wk-color-accent", "--wk-color-warning", "--wk-color-success", "--wk-color-info",
            "--wk-color-danger", "--wk-color-divider", "--wk-color-divider-subtle",
            "--wk-color-text-primary", "--wk-color-text-muted", "--wk-color-border", "--wk-color-button-hover",
        };

        // ---- Factories ------------------------------------------------------

        /// <summary>
        /// Notice banner with semantic kind, optional inline action button.
        /// Always carries the base wk-notice class plus the kind-specific
        /// modifier. ApplyTheme on an ancestor supplies the colours.
        /// </summary>
        public static VisualElement Notice(NoticeKind kind, string text, string actionLabel = null, Action onAction = null) {
            var notice = new VisualElement();
            notice.AddToClassList(ClassNotice);
            notice.AddToClassList(KindModifier(kind, "wk-notice--"));

            var label = new Label(text ?? "");
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.Normal;
            notice.Add(label);

            if (!string.IsNullOrEmpty(actionLabel) && onAction != null) {
                var btn = new Button(onAction) { text = actionLabel };
                btn.AddToClassList(ClassNoticeAction);
                btn.AddToClassList(ClassButtonSecondary);
                notice.Add(btn);
            }
            return notice;
        }

        /// <summary>
        /// Banner -- a single-line tinted row with the kind colour as the
        /// left-edge accent. Used inside inspector overlays where the IMGUI
        /// equivalent <see cref="WkStyles.StatusBanner"/> emits the full-width
        /// strip.
        /// </summary>
        public static VisualElement Banner(string text, NoticeKind kind) {
            var banner = new VisualElement();
            banner.AddToClassList(ClassNotice);
            banner.AddToClassList(KindModifier(kind, "wk-notice--"));
            var label = new Label(text ?? "");
            label.style.flexGrow = 1;
            banner.Add(label);
            return banner;
        }

        /// <summary>
        /// Titled section card. The body is added under .wk-section__body so
        /// callers can target the title and content separately via USS.
        /// </summary>
        public static VisualElement Section(string title, VisualElement body) {
            var section = new VisualElement();
            section.AddToClassList(ClassSection);
            if (!string.IsNullOrEmpty(title)) {
                var titleLabel = new Label(title);
                titleLabel.AddToClassList(ClassSectionTitle);
                section.Add(titleLabel);
            }
            var bodyHost = new VisualElement();
            bodyHost.AddToClassList(ClassSectionBody);
            if (body != null) bodyHost.Add(body);
            section.Add(bodyHost);
            return section;
        }

        /// <summary>Themed hairline divider (subtle variant available).</summary>
        public static VisualElement Divider(bool subtle = false) {
            var divider = new VisualElement();
            divider.AddToClassList(ClassDivider);
            if (subtle) divider.AddToClassList(ClassDividerSubtle);
            return divider;
        }

        public static Button PrimaryButton(string label, Action onClick) {
            var btn = new Button(onClick ?? (() => {})) { text = label };
            btn.AddToClassList(ClassButtonPrimary);
            return btn;
        }

        public static Button DangerButton(string label, Action onClick) {
            var btn = new Button(onClick ?? (() => {})) { text = label };
            btn.AddToClassList(ClassButtonDanger);
            return btn;
        }

        public static Button SecondaryButton(string label, Action onClick) {
            var btn = new Button(onClick ?? (() => {})) { text = label };
            btn.AddToClassList(ClassButtonSecondary);
            return btn;
        }

        public static VisualElement Pill(string text, NoticeKind kind) {
            var pill = new VisualElement();
            pill.AddToClassList(ClassPill);
            pill.AddToClassList(KindModifier(kind, "wk-pill--"));
            var label = new Label(text ?? "");
            pill.Add(label);
            return pill;
        }

        public static Button HelpIcon(string url, string tooltip = null) {
            var btn = new Button(() => { if (!string.IsNullOrEmpty(url)) Application.OpenURL(url); }) {
                text = "?",
                tooltip = tooltip ?? "Open the documentation for this tool.",
            };
            btn.AddToClassList(ClassHelpIcon);
            return btn;
        }

        public static ToolbarSearchField SearchField(string placeholder = "Search") {
            var search = new ToolbarSearchField();
            search.AddToClassList(ClassToolbarSearch);
            // ToolbarSearchField doesn't expose a placeholder directly; the
            // hint stays in the tooltip so screen-readers and hover hints
            // surface it.
            search.tooltip = placeholder;
            return search;
        }

        // ---- Theme + skin application --------------------------------------

        /// <summary>
        /// Apply <paramref name="theme"/>'s stylesheets to <paramref name="root"/>
        /// and add the matching .wk-theme--{name} + .wk-skin--{pro,personal}
        /// classes. Pass null to use <see cref="WkStyles.CurrentTheme"/>.
        /// Idempotent -- safe to call repeatedly on the same root.
        /// </summary>
        public static void ApplyTheme(VisualElement root, WkTheme theme = null) {
            if (root == null) return;
            theme = theme ?? WkStyles.CurrentTheme;

            var baseSheet  = LoadStyleSheet("wk-theme.uss");
            var paletteSheet = theme == WkTheme.VRCFury
                ? LoadStyleSheet("wk-theme-vrcfury.uss")
                : LoadStyleSheet("wk-theme-whyknot.uss");

            if (baseSheet != null    && !root.styleSheets.Contains(baseSheet))    root.styleSheets.Add(baseSheet);
            if (paletteSheet != null && !root.styleSheets.Contains(paletteSheet)) root.styleSheets.Add(paletteSheet);

            // Drop any previous wk-theme--* / wk-skin--* class then add the right ones.
            root.RemoveFromClassList(ClassThemeWhyKnot);
            root.RemoveFromClassList(ClassThemeVRCFury);
            root.AddToClassList(theme == WkTheme.VRCFury ? ClassThemeVRCFury : ClassThemeWhyKnot);

            ApplySkinClass(root);
        }

        /// <summary>
        /// Set wk-skin--pro / wk-skin--personal on <paramref name="element"/>
        /// based on <see cref="EditorGUIUtility.isProSkin"/>. Caller invokes
        /// when the editor skin flips at runtime -- USS variables propagate
        /// through descendants automatically once the class is updated.
        /// </summary>
        public static void ApplySkinClass(VisualElement element) {
            if (element == null) return;
            element.RemoveFromClassList(ClassSkinPro);
            element.RemoveFromClassList(ClassSkinPersonal);
            element.AddToClassList(EditorGUIUtility.isProSkin ? ClassSkinPro : ClassSkinPersonal);
        }

        // ---- Stylesheet loading -------------------------------------------

        private static readonly Dictionary<string, StyleSheet> _sheetCache = new Dictionary<string, StyleSheet>();
        private static string _scriptDirectory;

        private static StyleSheet LoadStyleSheet(string filename) {
            if (_sheetCache.TryGetValue(filename, out var cached) && cached != null) return cached;

            var dir = GetScriptDirectory();
            if (string.IsNullOrEmpty(dir)) return null;
            var path = dir + "/USS/" + filename;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null) _sheetCache[filename] = sheet;
            return sheet;
        }

        /// <summary>
        /// Find the directory containing this class's source file. Works
        /// in both wk-core's source location (Packages/dev.whyknot.core/Editor/Styling)
        /// and the synced downstream location (.../Editor/Internal/Styling)
        /// by finding the MonoScript asset by name regardless of where it
        /// lives in the AssetDatabase.
        /// </summary>
        private static string GetScriptDirectory() {
            if (!string.IsNullOrEmpty(_scriptDirectory)) return _scriptDirectory;

            var guids = AssetDatabase.FindAssets("WkUiElements t:MonoScript");
            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/WkUiElements.cs")) {
                    _scriptDirectory = path.Substring(0, path.Length - "/WkUiElements.cs".Length);
                    return _scriptDirectory;
                }
            }
            return null;
        }

        private static string KindModifier(NoticeKind kind, string prefix) {
            switch (kind) {
                case NoticeKind.Warning: return prefix + "warning";
                case NoticeKind.Success: return prefix + "success";
                case NoticeKind.Danger:  return prefix + "danger";
                default:                 return prefix + "info";
            }
        }
    }
}
