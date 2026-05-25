// WkStyles.cs
//
// Shared IMGUI styling for the WhyKnot Editor tools. Windows would
// otherwise mix EditorStyles.helpBox + boldLabel + miniLabel by hand at
// each call site; this file centralises the palette, typography, and a
// small set of widget primitives so every screen looks like the same app.
//
// Theming. Colors come from WkTheme. The default theme is WkTheme.WhyKnot
// (the brand palette: black / gray / light blue). Downstream tools that
// live inside someone else's chrome -- the VRCFury inspector overlay,
// for instance -- wrap their OnGUI body in
//     using (WkStyles.Scope(WkTheme.VRCFury)) { ... }
// to push a different palette for the duration of that scope. Scopes
// nest. Without an explicit scope every WkStyles call resolves through
// WkTheme.WhyKnot.
//
// Lazy GUIStyle initialisation. EditorStyles is null when assemblies
// first load, so static initialisers that touch it throw and the type
// becomes permanently broken. Every GUIStyle is a property with a
// backing field that's built on first access from inside an OnGUI call.
// Backing fields go null after a domain reload -- the lazy pattern
// handles that automatically without any extra plumbing in window code.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Styling {

    public enum NoticeKind { Info, Warning, Success }

    public static class WkStyles {

        // ---- Theming ------------------------------------------------------

        private static readonly Stack<WkTheme> _themeStack = new Stack<WkTheme>();

        /// <summary>Theme used when no scope is active. Set this in each consumer's [InitializeOnLoad] if you want a different default; otherwise WkTheme.WhyKnot applies.</summary>
        public static WkTheme DefaultTheme { get; set; } = WkTheme.WhyKnot;

        /// <summary>The theme currently in effect. Resolves to the top of the scope stack, or DefaultTheme if no scope is active.</summary>
        public static WkTheme CurrentTheme =>
            _themeStack.Count > 0 ? _themeStack.Peek() : DefaultTheme;

        /// <summary>The variant of the current theme matching the user's Pro/Personal skin choice.</summary>
        public static WkTheme.Variant Current => CurrentTheme.Current;

        /// <summary>
        /// Push <paramref name="theme"/> onto the theme stack for the duration
        /// of a `using` block. Idiomatic usage:
        ///     using (WkStyles.Scope(WkTheme.VRCFury)) {
        ///         WkStyles.Notice(NoticeKind.Info, "...");
        ///     }
        /// Passing null leaves the stack alone.
        /// </summary>
        public static IDisposable Scope(WkTheme theme) {
            if (theme == null) return NoopScope.Instance;
            _themeStack.Push(theme);
            return new ThemeScope();
        }

        private sealed class ThemeScope : IDisposable {
            public void Dispose() {
                if (_themeStack.Count > 0) _themeStack.Pop();
            }
        }

        private sealed class NoopScope : IDisposable {
            public static readonly NoopScope Instance = new NoopScope();
            public void Dispose() { }
        }

        // ---- Palette (resolves through the active theme) ------------------

        /// <summary>Brand accent -- primary buttons, suggested-card border, suggestion bar fill.</summary>
        public static Color ColorAccent  => Current.Accent;

        /// <summary>Warning notices, banner backgrounds.</summary>
        public static Color ColorWarning => Current.Warning;

        /// <summary>Success notices ("scan clean").</summary>
        public static Color ColorSuccess => Current.Success;

        /// <summary>Info notices, neutral pills.</summary>
        public static Color ColorInfo    => Current.Info;

        /// <summary>Destructive action signal (red button background, "Stop Previewing", etc).</summary>
        public static Color ColorDanger  => Current.Danger;

        /// <summary>Hairline divider tint with alpha baked in.</summary>
        public static Color ColorDivider => Current.Divider;

        /// <summary>Surface color for banners and panels.</summary>
        public static Color ColorBackground    => Current.Background;
        public static Color ColorBackgroundAlt => Current.BackgroundAlt;

        /// <summary>Default label color.</summary>
        public static Color ColorTextPrimary => Current.TextPrimary;
        public static Color ColorTextMuted   => Current.TextMuted;
        public static Color ColorBorder      => Current.Border;

        // ---- Typography ---------------------------------------------------
        // Each style is built from a baseline EditorStyles entry to inherit
        // theme colours, then overridden with our font size / weight.

        private static GUIStyle _sectionTitle;
        private static GUIStyle _subsectionTitle;
        private static GUIStyle _body;
        private static GUIStyle _muted;
        private static GUIStyle _mono;
        private static GUIStyle _primaryButton;
        private static GUIStyle _miniRowButton;
        private static GUIStyle _badgePillStyle;
        private static GUIStyle _cardSelected;
        private static GUIStyle _foldoutHeader;

        public static GUIStyle SectionTitle =>
            _sectionTitle ??= new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 14,
                margin = new RectOffset(0, 0, 4, 2),
            };

        public static GUIStyle SubsectionTitle =>
            _subsectionTitle ??= new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 12,
                margin = new RectOffset(0, 0, 2, 2),
            };

        public static GUIStyle Body =>
            _body ??= new GUIStyle(EditorStyles.label) {
                fontSize = 11,
                wordWrap = true,
            };

        public static GUIStyle Muted =>
            _muted ??= new GUIStyle(EditorStyles.miniLabel) {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
            };

        public static GUIStyle Mono =>
            _mono ??= BuildMono();

        public static GUIStyle PrimaryButton =>
            _primaryButton ??= new GUIStyle(GUI.skin.button) {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 28f,
                padding = new RectOffset(16, 16, 6, 6),
            };

        public static GUIStyle MiniRowButton =>
            _miniRowButton ??= new GUIStyle(EditorStyles.miniButton) {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2),
            };

        public static GUIStyle BadgePillStyle =>
            _badgePillStyle ??= new GUIStyle(EditorStyles.miniLabel) {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 1, 1),
                margin = new RectOffset(0, 4, 1, 1),
                normal = { textColor = Color.white },
            };

        public static GUIStyle CardSelected =>
            _cardSelected ??= new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(2, 2, 2, 2),
            };

        public static GUIStyle FoldoutHeader =>
            _foldoutHeader ??= new GUIStyle(EditorStyles.foldout) {
                fontStyle = FontStyle.Bold,
            };

        private static GUIStyle BuildMono() {
            // Consolas exists on Windows; Menlo on Mac; Courier New everywhere.
            // CreateDynamicFontFromOSFont returns null when the font isn't
            // available -- fall through.
            Font font = null;
            foreach (var name in new[] { "Consolas", "Menlo", "Courier New" }) {
                font = Font.CreateDynamicFontFromOSFont(name, 10);
                if (font != null) break;
            }
            var s = new GUIStyle(EditorStyles.label) { fontSize = 10 };
            if (font != null) s.font = font;
            return s;
        }

        // ---- Layout -------------------------------------------------------

        /// <summary>Default labelled-row label width for LabeledField.</summary>
        public const float LabelColumn = 110f;

        // ---- Primitives ---------------------------------------------------

        /// <summary>
        /// Begin a titled, helpBox-bordered region. Use with a `using` block:
        ///   using (WkStyles.Section("Tunables")) { ... }
        /// Header is rendered inside the box so the title stays attached on
        /// scroll.
        /// </summary>
        public static IDisposable Section(string title, string tooltip = null) {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (!string.IsNullOrEmpty(title)) {
                EditorGUILayout.LabelField(new GUIContent(title, tooltip ?? ""), SubsectionTitle);
            }
            return new SectionScope();
        }

        private sealed class SectionScope : IDisposable {
            public void Dispose() { EditorGUILayout.EndVertical(); }
        }

        /// <summary>
        /// Coloured pill -- small rounded label for category tags. The pill
        /// claims width to fit `text`. Tooltip is honoured via GUIContent.
        /// </summary>
        public static void BadgePill(string text, Color tint, string tooltip = null) {
            var content = new GUIContent(text, tooltip ?? "");
            var size = BadgePillStyle.CalcSize(content);
            // Add a tiny padding so the text doesn't sit flush against the edge.
            var rect = GUILayoutUtility.GetRect(size.x + 4, 16, BadgePillStyle, GUILayout.ExpandWidth(false));
            var prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, tint, 0, 3f);
            GUI.color = Color.white;
            GUI.Label(rect, content, BadgePillStyle);
            GUI.color = prev;
        }

        /// <summary>1px (or thicker) horizontal divider with theme-aware alpha.</summary>
        public static void Divider(float thickness = 1f) {
            var rect = EditorGUILayout.GetControlRect(false, thickness);
            EditorGUI.DrawRect(rect, ColorDivider);
        }

        /// <summary>
        /// Label + inline control on one row, label clipped to LabelColumn.
        /// Use to consolidate the repeated `BeginHorizontal + LabelField(width:N) + control + EndHorizontal` pattern.
        /// </summary>
        public static void LabeledField(GUIContent label, Action drawField, float labelWidth = LabelColumn) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                drawField?.Invoke();
            }
        }

        /// <summary>Tall, weight-bold accent button. Returns true on click.</summary>
        public static bool PrimaryButtonInline(GUIContent content, params GUILayoutOption[] options) {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = ColorAccent;
            bool clicked = GUILayout.Button(content, PrimaryButton, options);
            GUI.backgroundColor = prev;
            return clicked;
        }

        /// <summary>
        /// Replacement for HelpBox. Optional inline action button on the right.
        /// Returns true iff the action button was clicked. Action is null →
        /// no button, just the message.
        /// </summary>
        public static bool Notice(NoticeKind kind, string message,
                                  string actionLabel = null,
                                  string actionTooltip = null,
                                  GUILayoutOption[] actionOptions = null) {
            Color bg;
            switch (kind) {
                case NoticeKind.Warning: bg = ColorWarning; break;
                case NoticeKind.Success: bg = ColorSuccess; break;
                default:                 bg = ColorInfo;    break;
            }
            // Soft tint as a background; the helpBox border still sells it as a panel.
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(bg.r, bg.g, bg.b, 0.35f);
            bool clicked = false;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField(message, Body, GUILayout.ExpandWidth(true));
                if (!string.IsNullOrEmpty(actionLabel)) {
                    var content = new GUIContent(actionLabel, actionTooltip ?? "");
                    var opts = actionOptions ?? new[] { GUILayout.Width(160), GUILayout.Height(26) };
                    GUI.backgroundColor = bg;
                    if (GUILayout.Button(content, opts)) clicked = true;
                }
            }
            GUI.backgroundColor = prev;
            return clicked;
        }

        // ---- Convenience: open a documentation URL ----------------------

        /// <summary>Draw a small `?` help icon top-right that opens the given URL.</summary>
        public static void HelpIcon(string documentationUrl, string tooltip = "Open the documentation for this tool.") {
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("?", tooltip),
                        EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(18))) {
                    Application.OpenURL(documentationUrl);
                }
            }
        }

        // ---- Console notice helper --------------------------------------

        /// <summary>
        /// Drop a transient "see console" notice into the IMGUI flow.
        /// Returns true iff the user clicked the inline "Open Console" action.
        /// </summary>
        public static bool ConsoleResultNotice(string what) {
            return Notice(NoticeKind.Info,
                $"{what} printed to the Unity console.",
                "Open Console",
                "Open the Console window so you can read the output.");
        }
    }
}
