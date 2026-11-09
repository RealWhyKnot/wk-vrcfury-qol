#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmeVrcfQol.Internal.Styling
{
    public static class WkUiElements
    {
        public const string ClassNotice = "wk-notice";
        public const string ClassNoticeInfo = "wk-notice--info";
        public const string ClassNoticeWarning = "wk-notice--warning";
        public const string ClassNoticeSuccess = "wk-notice--success";
        public const string ClassNoticeDanger = "wk-notice--danger";
        public const string ClassNoticeAction = "wk-notice__action";

        public const string ClassSection = "wk-section";
        public const string ClassSectionTitle = "wk-section__title";
        public const string ClassSectionBody = "wk-section__body";

        public const string ClassPill = "wk-pill";
        public const string ClassPillInfo = "wk-pill--info";
        public const string ClassPillWarning = "wk-pill--warning";
        public const string ClassPillSuccess = "wk-pill--success";
        public const string ClassPillDanger = "wk-pill--danger";

        public const string ClassDivider = "wk-divider";
        public const string ClassDividerSubtle = "wk-divider--subtle";

        public const string ClassButtonPrimary = "wk-button--primary";
        public const string ClassButtonDanger = "wk-button--danger";
        public const string ClassButtonSecondary = "wk-button--secondary";

        public const string ClassHelpIcon = "wk-help-icon";
        public const string ClassToolbarSearch = "wk-toolbar__search";
        public const string ClassRow = "wk-row-list__row";
        public const string ClassRowStriped = "wk-row-list__row--striped";

        public const string ClassThemeWhyKnot = "wk-theme--whyknot";
        public const string ClassThemeVRCFury = "wk-theme--vrcfury";
        public const string ClassSkinPro = "wk-skin--pro";
        public const string ClassSkinPersonal = "wk-skin--personal";

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

        public static VisualElement Notice(NoticeKind kind, string text, string? actionLabel = null, Action? onAction = null)
        {
            var notice = new VisualElement();
            notice.AddToClassList(ClassNotice);
            notice.AddToClassList(KindModifier(kind, "wk-notice--"));

            var label = new Label(text ?? "");
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.Normal;
            notice.Add(label);

            if (!string.IsNullOrEmpty(actionLabel) && onAction != null)
            {
                var btn = new Button(onAction) { text = actionLabel };
                btn.AddToClassList(ClassNoticeAction);
                btn.AddToClassList(ClassButtonSecondary);
                notice.Add(btn);
            }
            return notice;
        }

        public static VisualElement Banner(string text, NoticeKind kind)
        {
            var banner = new VisualElement();
            banner.AddToClassList(ClassNotice);
            banner.AddToClassList(KindModifier(kind, "wk-notice--"));
            var label = new Label(text ?? "");
            label.style.flexGrow = 1;
            banner.Add(label);
            return banner;
        }

        public static VisualElement Section(string title, VisualElement body)
        {
            var section = new VisualElement();
            section.AddToClassList(ClassSection);
            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new Label(title);
                titleLabel.AddToClassList(ClassSectionTitle);
                section.Add(titleLabel);
            }
            var bodyHost = new VisualElement();
            bodyHost.AddToClassList(ClassSectionBody);
            if (body != null)
            {
                bodyHost.Add(body);
            }

            section.Add(bodyHost);
            return section;
        }

        public static VisualElement Divider(bool subtle = false)
        {
            var divider = new VisualElement();
            divider.AddToClassList(ClassDivider);
            if (subtle)
            {
                divider.AddToClassList(ClassDividerSubtle);
            }

            return divider;
        }

        public static Button PrimaryButton(string label, Action onClick)
        {
            var btn = new Button(onClick ?? (() => { })) { text = label };
            btn.AddToClassList(ClassButtonPrimary);
            return btn;
        }

        public static Button DangerButton(string label, Action onClick)
        {
            var btn = new Button(onClick ?? (() => { })) { text = label };
            btn.AddToClassList(ClassButtonDanger);
            return btn;
        }

        public static Button SecondaryButton(string label, Action onClick)
        {
            var btn = new Button(onClick ?? (() => { })) { text = label };
            btn.AddToClassList(ClassButtonSecondary);
            return btn;
        }

        public static VisualElement Pill(string text, NoticeKind kind)
        {
            var pill = new VisualElement();
            pill.AddToClassList(ClassPill);
            pill.AddToClassList(KindModifier(kind, "wk-pill--"));
            var label = new Label(text ?? "");
            pill.Add(label);
            return pill;
        }

        public static Button HelpIcon(Uri url, string? tooltip = null)
        {
            var btn = new Button(() => Application.OpenURL(url.AbsoluteUri))
            {
                text = "?",
                tooltip = tooltip ?? "Open the documentation for this tool.",
            };
            btn.AddToClassList(ClassHelpIcon);
            return btn;
        }

        public static ToolbarSearchField SearchField(string placeholder = "Search")
        {
            var search = new ToolbarSearchField();
            search.AddToClassList(ClassToolbarSearch);
            search.tooltip = placeholder;
            return search;
        }

        public static void ApplyTheme(VisualElement root, WkTheme? theme = null)
        {
            if (root == null)
            {
                return;
            }

            theme ??= WkStyles.CurrentTheme;

            StyleSheet? baseSheet = LoadStyleSheet("wk-theme.uss");
            StyleSheet? paletteSheet = theme == WkTheme.VRCFury
                ? LoadStyleSheet("wk-theme-vrcfury.uss")
                : LoadStyleSheet("wk-theme-whyknot.uss");

            if (baseSheet != null && !root.styleSheets.Contains(baseSheet))
            {
                root.styleSheets.Add(baseSheet);
            }

            if (paletteSheet != null && !root.styleSheets.Contains(paletteSheet))
            {
                root.styleSheets.Add(paletteSheet);
            }

            root.RemoveFromClassList(ClassThemeWhyKnot);
            root.RemoveFromClassList(ClassThemeVRCFury);
            root.AddToClassList(theme == WkTheme.VRCFury ? ClassThemeVRCFury : ClassThemeWhyKnot);

            ApplySkinClass(root);
        }

        public static void ApplySkinClass(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.RemoveFromClassList(ClassSkinPro);
            element.RemoveFromClassList(ClassSkinPersonal);
            element.AddToClassList(EditorGUIUtility.isProSkin ? ClassSkinPro : ClassSkinPersonal);
        }

        private static readonly Dictionary<string, StyleSheet> SheetCache = new(StringComparer.Ordinal);
        private static string? ScriptDirectory;

        private static StyleSheet? LoadStyleSheet(string filename)
        {
            if (SheetCache.TryGetValue(filename, out StyleSheet? cached) && cached != null)
            {
                return cached;
            }

            string? dir = GetScriptDirectory();
            if (string.IsNullOrEmpty(dir))
            {
                return null;
            }

            string path = dir + "/USS/" + filename;

            StyleSheet? sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null)
            {
                SheetCache[filename] = sheet;
            }

            return sheet;
        }

        private static string? GetScriptDirectory()
        {
            if (!string.IsNullOrEmpty(ScriptDirectory))
            {
                return ScriptDirectory;
            }

            string[] guids = AssetDatabase.FindAssets("WkUiElements t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/WkUiElements.cs", StringComparison.Ordinal))
                {
                    ScriptDirectory = path[..^"/WkUiElements.cs".Length];
                    return ScriptDirectory;
                }
            }
            return null;
        }

        private static string KindModifier(NoticeKind kind, string prefix)
        {
            return kind switch
            {
                NoticeKind.Warning => prefix + "warning",
                NoticeKind.Success => prefix + "success",
                NoticeKind.Danger => prefix + "danger",
                NoticeKind.Info => prefix + "info",
                _ => prefix + "info",
            };
        }
    }
}
