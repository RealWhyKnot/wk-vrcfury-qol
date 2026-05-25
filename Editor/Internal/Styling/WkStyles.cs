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

    public enum NoticeKind { Info, Warning, Success, Danger }

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
        public static Color ColorDivider       => Current.Divider;

        /// <summary>Lower-contrast divider for dense row lists.</summary>
        public static Color ColorDividerSubtle => Current.DividerSubtle;

        /// <summary>Surface color for banners and panels.</summary>
        public static Color ColorBackground         => Current.Background;
        public static Color ColorBackgroundAlt      => Current.BackgroundAlt;
        public static Color ColorBackgroundEmphasis => Current.BackgroundEmphasis;

        /// <summary>Default label color.</summary>
        public static Color ColorTextPrimary => Current.TextPrimary;
        public static Color ColorTextMuted   => Current.TextMuted;
        public static Color ColorBorder      => Current.Border;
        public static Color ColorButtonHover => Current.ButtonHover;

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
        private static GUIStyle _caption;
        private static GUIStyle _code;
        private static GUIStyle _titleBar;
        private static GUIStyle _rowAlt;

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

        /// <summary>Italic muted caption text -- "Last updated:" annotations.</summary>
        public static GUIStyle Caption =>
            _caption ??= new GUIStyle(EditorStyles.miniLabel) {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
            };

        /// <summary>Inline code block: monospace, no wrap, plain text (no rich-text markup honoured).</summary>
        public static GUIStyle Code =>
            _code ??= BuildCode();

        /// <summary>Section-title font with right-edge padding reserved for the inline help icon.</summary>
        public static GUIStyle TitleBarStyle =>
            _titleBar ??= new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 14,
                margin = new RectOffset(0, 0, 4, 2),
                padding = new RectOffset(0, 26, 0, 0),
            };

        /// <summary>Striped-row background for dense list views.</summary>
        public static GUIStyle RowAlt =>
            _rowAlt ??= new GUIStyle(EditorStyles.label) {
                padding = new RectOffset(4, 4, 2, 2),
                margin = new RectOffset(0, 0, 0, 0),
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

        private static GUIStyle BuildCode() {
            var s = BuildMono();
            s.wordWrap = false;
            s.richText = false;
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

        /// <summary>Resolve a semantic <see cref="NoticeKind"/> to the active theme color.</summary>
        public static Color ColorForKind(NoticeKind kind) {
            switch (kind) {
                case NoticeKind.Warning: return ColorWarning;
                case NoticeKind.Success: return ColorSuccess;
                case NoticeKind.Danger:  return ColorDanger;
                default:                 return ColorInfo;
            }
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

        /// <summary>Lower-contrast divider for dense row lists; reads ColorDividerSubtle.</summary>
        public static void SubtleDivider(float thickness = 1f) {
            var rect = EditorGUILayout.GetControlRect(false, thickness);
            EditorGUI.DrawRect(rect, ColorDividerSubtle);
        }

        /// <summary>NoticeKind-shaped <see cref="BadgePill"/> overload that routes through the theme.</summary>
        public static void BadgePill(string text, NoticeKind kind, string tooltip = null) {
            BadgePill(text, ColorForKind(kind), tooltip);
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
            Color bg = ColorForKind(kind);
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

        // ---- Extended primitives ------------------------------------------

        /// <summary>Destructive-action button, red background + white bold text. Returns true on click.</summary>
        public static bool DangerButtonInline(GUIContent content, params GUILayoutOption[] options) {
            var prevBg = GUI.backgroundColor;
            var prevFg = GUI.contentColor;
            GUI.backgroundColor = ColorDanger;
            GUI.contentColor = Color.white;
            bool clicked = GUILayout.Button(content, PrimaryButton, options);
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevFg;
            return clicked;
        }

        /// <summary>Neutral footer-row companion to <see cref="PrimaryButtonInline"/>.</summary>
        public static bool SecondaryButtonInline(GUIContent content, params GUILayoutOption[] options) {
            return GUILayout.Button(content, PrimaryButton, options);
        }

        /// <summary>
        /// Bold foldout header. Returns the new expanded state -- caller
        /// stores it themselves and gates the body with `if (expanded) { ... }`.
        /// Matches <see cref="EditorGUILayout.Foldout(bool, string)"/>'s shape
        /// but routes through <see cref="FoldoutHeader"/> styling.
        /// </summary>
        public static bool FoldoutHeaderRow(string label, bool expanded, string tooltip = null) {
            var content = new GUIContent(label, tooltip ?? "");
            return EditorGUILayout.Foldout(expanded, content, true, FoldoutHeader);
        }

        /// <summary>
        /// Two-column horizontal layout: <paramref name="drawLeft"/> claims
        /// <paramref name="leftWidth"/> pixels on the left, <paramref name="drawRight"/>
        /// fills the remainder.
        /// </summary>
        public static void TwoColumn(float leftWidth, Action drawLeft, Action drawRight) {
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth))) {
                    drawLeft?.Invoke();
                }
                using (new EditorGUILayout.VerticalScope()) {
                    drawRight?.Invoke();
                }
            }
        }

        /// <summary>
        /// Editor search field. Returns true when <paramref name="query"/>
        /// changed this frame so the caller can re-filter. Stack-allocated
        /// SearchField backing is cached internally; first call after a
        /// domain reload reconstructs it.
        /// </summary>
        public static bool SearchField(ref string query, string placeholder = "Search", float width = 0) {
            _searchField ??= new UnityEditor.IMGUI.Controls.SearchField();
            var prev = query ?? "";
            var opts = width > 0 ? new[] { GUILayout.Width(width) } : new GUILayoutOption[0];
            var rect = EditorGUILayout.GetControlRect(false, 18, opts);
            var next = _searchField.OnGUI(rect, prev);
            query = next;
            return next != prev;
        }

        private static UnityEditor.IMGUI.Controls.SearchField _searchField;

        /// <summary>
        /// Tab bar. Renders <paramref name="tabs"/> as a horizontal toolbar
        /// and returns the newly-selected index. Caller stores the index;
        /// gate body content on it.
        /// </summary>
        public static int TabBar(int selected, params GUIContent[] tabs) {
            if (tabs == null || tabs.Length == 0) return selected;
            return GUILayout.Toolbar(Mathf.Clamp(selected, 0, tabs.Length - 1), tabs, EditorStyles.toolbarButton);
        }

        /// <summary>
        /// Themed progress bar. <paramref name="t01"/> clamps to [0, 1].
        /// </summary>
        public static void ProgressBar(float t01, string label = null, float height = 12f) {
            var rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, ColorBackgroundAlt);
            var filled = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(t01), rect.height);
            EditorGUI.DrawRect(filled, ColorAccent);
            if (!string.IsNullOrEmpty(label)) {
                var prev = GUI.contentColor;
                GUI.contentColor = ColorTextPrimary;
                GUI.Label(rect, label, EditorStyles.miniLabel);
                GUI.contentColor = prev;
            }
        }

        /// <summary>Labeled <see cref="EditorGUILayout.ObjectField"/> row with theme-routed label width.</summary>
        public static T ObjectFieldRow<T>(GUIContent label, T value, bool allowSceneObjects = true) where T : UnityEngine.Object {
            T next = value;
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelColumn));
                next = (T) EditorGUILayout.ObjectField(value, typeof(T), allowSceneObjects);
            }
            return next;
        }

        /// <summary>
        /// Full-width tinted strip with a centred bold label. Used for
        /// "last build: ok / failed" sticky status bars.
        /// </summary>
        public static void StatusBanner(string text, Color tint, GUIContent icon = null, float height = 22) {
            var rect = EditorGUILayout.GetControlRect(false, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, tint);
            var prev = GUI.contentColor;
            GUI.contentColor = Color.white;
            var labelStyle = new GUIStyle(EditorStyles.boldLabel) {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            GUI.Label(rect, icon != null ? new GUIContent(text, icon.image) : new GUIContent(text), labelStyle);
            GUI.contentColor = prev;
        }

        /// <summary>NoticeKind-shaped <see cref="StatusBanner"/> overload that routes through the theme.</summary>
        public static void StatusBanner(string text, NoticeKind kind, GUIContent icon = null, float height = 22) {
            StatusBanner(text, ColorForKind(kind), icon, height);
        }

        /// <summary>
        /// Tile a checker pattern across <paramref name="rect"/>. Used as
        /// the transparency-aware background for preview thumbnails.
        /// </summary>
        public static void Checker(Rect rect, int squareSize = 8) {
            if (squareSize < 1) squareSize = 1;
            var c1 = new Color(0.35f, 0.35f, 0.35f, 1f);
            var c2 = new Color(0.25f, 0.25f, 0.25f, 1f);
            int cols = Mathf.CeilToInt(rect.width / squareSize);
            int rows = Mathf.CeilToInt(rect.height / squareSize);
            for (int y = 0; y < rows; y++) {
                for (int x = 0; x < cols; x++) {
                    var r = new Rect(
                        rect.x + x * squareSize,
                        rect.y + y * squareSize,
                        Mathf.Min(squareSize, rect.xMax - (rect.x + x * squareSize)),
                        Mathf.Min(squareSize, rect.yMax - (rect.y + y * squareSize)));
                    EditorGUI.DrawRect(r, ((x + y) & 1) == 0 ? c1 : c2);
                }
            }
        }

        /// <summary>Draw a four-slab border around <paramref name="rect"/> in the given <paramref name="color"/>.</summary>
        public static void RectBorder(Rect rect, Color color, float thickness = 1f) {
            EditorGUI.DrawRect(new Rect(rect.x,                       rect.y,                        rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x,                       rect.yMax - thickness,         rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x,                       rect.y,                        thickness,  rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness,        rect.y,                        thickness,  rect.height), color);
        }

        /// <summary>
        /// Window title row -- big bold label on the left, optional help
        /// icon on the right. Absorbs the per-window <c>DrawTitleBar()</c>
        /// pattern that every WkToolWindow subclass would otherwise inline.
        /// </summary>
        public static void TitleBar(string title, string helpUrl = null) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(title, SectionTitle);
                if (!string.IsNullOrEmpty(helpUrl)) {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("?", "Open the documentation for this tool."),
                            EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(18))) {
                        Application.OpenURL(helpUrl);
                    }
                }
            }
        }
    }
}
