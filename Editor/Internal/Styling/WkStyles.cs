#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Styling
{
    public static partial class WkStyles
    {
        private static readonly Stack<WkTheme> ThemeStack = new();

        public static WkTheme DefaultTheme { get; set; } = WkTheme.WhyKnot;

        public static WkTheme CurrentTheme =>
            ThemeStack.Count > 0 ? ThemeStack.Peek() : DefaultTheme;

        public static WkThemeVariant Current => CurrentTheme.Current;

        public static IDisposable Scope(WkTheme theme)
        {
            if (theme == null)
            {
                return NoopScope.Instance;
            }

            ThemeStack.Push(theme);
            return new ThemeScope();
        }

        private sealed class ThemeScope : IDisposable
        {
            public void Dispose()
            {
                if (ThemeStack.Count > 0)
                {
                    ThemeStack.Pop();
                }
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }

        public static Color ColorAccent => Current.Accent;

        public static Color ColorWarning => Current.Warning;

        public static Color ColorSuccess => Current.Success;

        public static Color ColorInfo => Current.Info;

        public static Color ColorDanger => Current.Danger;

        public static Color ColorDivider => Current.Divider;

        public static Color ColorDividerSubtle => Current.DividerSubtle;

        public static Color ColorBackground => Current.Background;
        public static Color ColorBackgroundAlt => Current.BackgroundAlt;
        public static Color ColorBackgroundEmphasis => Current.BackgroundEmphasis;

        public static Color ColorTextPrimary => Current.TextPrimary;
        public static Color ColorTextMuted => Current.TextMuted;
        public static Color ColorBorder => Current.Border;
        public static Color ColorButtonHover => Current.ButtonHover;

        private static Texture2D? CachedBrandLogo;
        private static bool BrandLogoLoaded;

        public const string BrandFooterText = "Made with \u2665 by WhyKnot";
        public const string BrandLogoAssetName = "WhyKnotLogo";

        private static readonly string[] BrandLogoAssetPaths = {
            "Packages/dev.whyknot.wk-vrcfury-qol/Editor/Internal/Assets/WhyKnotLogo.png",
        };

        public static Texture2D? BrandLogoTexture
        {
            get
            {
                if (!BrandLogoLoaded)
                {
                    CachedBrandLogo = LoadBrandLogoTexture();
                    BrandLogoLoaded = true;
                }
                return CachedBrandLogo;
            }
        }

        public const float LabelColumn = 110f;

        public static readonly Vector2 DefaultMaxAutoWindowSize = new(980f, 760f);

        public static float CappedListHeight(int itemCount, float rowHeight = 20f, float minHeight = 80f, float maxHeight = 260f)
        {
            int count = Mathf.Max(0, itemCount);
            return Mathf.Clamp((count * rowHeight) + 12f, minHeight, maxHeight);
        }

        public static Vector2 ClampAutoWindowSize(Vector2 preferredSize, Vector2 minSize, Vector2 maxAutoSize)
        {
            var max = new Vector2(
                Mathf.Max(minSize.x, maxAutoSize.x),
                Mathf.Max(minSize.y, maxAutoSize.y));
            return new Vector2(
                Mathf.Clamp(preferredSize.x, minSize.x, max.x),
                Mathf.Clamp(preferredSize.y, minSize.y, max.y));
        }

        public static bool AutoSizeWindow(
                EditorWindow window,
                ref string? lastSignature,
                string signature,
                Vector2 minSize,
                Vector2 preferredSize,
                Vector2 maxAutoSize)
        {
            if (window == null)
            {
                return false;
            }

            window.minSize = minSize;

            string normalized = signature ?? "";
            if (string.Equals(lastSignature, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            lastSignature = normalized;

            ScheduleAutoSize(window, minSize, preferredSize, maxAutoSize);
            return true;
        }

        public static void ScheduleAutoSize(
                EditorWindow window,
                Vector2 minSize,
                Vector2 preferredSize,
                Vector2 maxAutoSize)
        {
            if (window == null)
            {
                return;
            }

            Vector2 target = ClampAutoWindowSize(preferredSize, minSize, maxAutoSize);
            EditorApplication.delayCall += () =>
            {
                if (window == null)
                {
                    return;
                }

                window.minSize = minSize;
                Rect pos = window.position;
                if (pos.width <= 1f || pos.height <= 1f)
                {
                    return;
                }

                if (Mathf.Abs(pos.width - target.x) < 1f && Mathf.Abs(pos.height - target.y) < 1f)
                {
                    return;
                }

                window.position = new Rect(pos.x, pos.y, target.x, target.y);
            };
        }

        public static IDisposable Section(string title, string? tooltip = null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (!string.IsNullOrEmpty(title))
            {
                EditorGUILayout.LabelField(new GUIContent(title, tooltip ?? ""), SubsectionTitle);
            }
            return new SectionScope();
        }

        private sealed class SectionScope : IDisposable
        {
            public void Dispose() { EditorGUILayout.EndVertical(); }
        }

        public static Color ColorForKind(NoticeKind kind)
        {
            return kind switch
            {
                NoticeKind.Warning => ColorWarning,
                NoticeKind.Success => ColorSuccess,
                NoticeKind.Danger => ColorDanger,
                NoticeKind.Info => ColorInfo,
                _ => ColorInfo,
            };
        }

        public static void BadgePill(string text, Color tint, string? tooltip = null)
        {
            var content = new GUIContent(text, tooltip ?? "");
            Vector2 size = BadgePillStyle.CalcSize(content);
            Rect rect = GUILayoutUtility.GetRect(size.x + 4, 16, BadgePillStyle, GUILayout.ExpandWidth(expand: false));
            Color prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: false, 0, tint, 0, 3f);
            GUI.color = Color.white;
            GUI.Label(rect, content, BadgePillStyle);
            GUI.color = prev;
        }

        public static void Divider(float thickness = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, thickness);
            EditorGUI.DrawRect(rect, ColorDivider);
        }

        public static void SubtleDivider(float thickness = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, thickness);
            EditorGUI.DrawRect(rect, ColorDividerSubtle);
        }

        public static void BadgePill(string text, NoticeKind kind, string? tooltip = null)
        {
            BadgePill(text, ColorForKind(kind), tooltip);
        }

        public static void LabeledField(GUIContent label, Action drawField, float labelWidth = LabelColumn)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                drawField?.Invoke();
            }
        }

        public static bool PrimaryButtonInline(GUIContent content, params GUILayoutOption[] options)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = ColorAccent;
            bool clicked = GUILayout.Button(content, PrimaryButton, options);
            GUI.backgroundColor = prev;
            return clicked;
        }

        public static bool Notice(NoticeKind kind, string message,
                                  string? actionLabel = null,
                                  string? actionTooltip = null,
                                  GUILayoutOption[]? actionOptions = null)
        {
            Color bg = ColorForKind(kind);
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(bg.r, bg.g, bg.b, 0.35f);
            bool clicked = false;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(message, Body, GUILayout.ExpandWidth(expand: true));
                if (!string.IsNullOrEmpty(actionLabel))
                {
                    var content = new GUIContent(actionLabel, actionTooltip ?? "");
                    GUILayoutOption[] opts = actionOptions ?? new[] { GUILayout.Width(160), GUILayout.Height(26) };
                    GUI.backgroundColor = bg;
                    if (GUILayout.Button(content, opts))
                    {
                        clicked = true;
                    }
                }
            }
            GUI.backgroundColor = prev;
            return clicked;
        }

        public static void HelpIcon(Uri documentationUrl, string tooltip = "Open the documentation for this tool.")
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("?", tooltip),
                        EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(18)))
                {
                    Application.OpenURL(documentationUrl.AbsoluteUri);
                }
            }
        }

        public static bool ConsoleResultNotice(string what)
        {
            return Notice(NoticeKind.Info,
                $"{what} printed to the Unity console.",
                "Open Console",
                "Open the Console window so you can read the output.");
        }

        public static bool DangerButtonInline(GUIContent content, params GUILayoutOption[] options)
        {
            Color prevBg = GUI.backgroundColor;
            Color prevFg = GUI.contentColor;
            GUI.backgroundColor = ColorDanger;
            GUI.contentColor = Color.white;
            bool clicked = GUILayout.Button(content, PrimaryButton, options);
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevFg;
            return clicked;
        }

        public static bool SecondaryButtonInline(GUIContent content, params GUILayoutOption[] options)
        {
            return GUILayout.Button(content, PrimaryButton, options);
        }

        public static bool FoldoutHeaderRow(string label, bool expanded, string? tooltip = null)
        {
            var content = new GUIContent(label, tooltip ?? "");
            return EditorGUILayout.Foldout(expanded, content, toggleOnLabelClick: true, FoldoutHeader);
        }

        public static void TwoColumn(float leftWidth, Action drawLeft, Action drawRight)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth)))
                {
                    drawLeft?.Invoke();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    drawRight?.Invoke();
                }
            }
        }

        public static bool SearchField(ref string query, float width = 0)
        {
            CachedSearchField ??= new UnityEditor.IMGUI.Controls.SearchField();
            string prev = query ?? "";
            GUILayoutOption[] opts = width > 0 ? new[] { GUILayout.Width(width) } : Array.Empty<GUILayoutOption>();
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, 18, opts);
            string next = CachedSearchField.OnGUI(rect, prev);
            query = next;
            return !string.Equals(next, prev, StringComparison.Ordinal);
        }

        private static UnityEditor.IMGUI.Controls.SearchField? CachedSearchField;

        public static int TabBar(int selected, params GUIContent[] tabs)
        {
            return tabs == null || tabs.Length == 0
                ? selected
                : GUILayout.Toolbar(Mathf.Clamp(selected, 0, tabs.Length - 1), tabs, EditorStyles.toolbarButton);
        }

        public static void ProgressBar(float t01, string? label = null, float height = 12f)
        {
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, height);
            EditorGUI.DrawRect(rect, ColorBackgroundAlt);
            var filled = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(t01), rect.height);
            EditorGUI.DrawRect(filled, ColorAccent);
            if (!string.IsNullOrEmpty(label))
            {
                Color prev = GUI.contentColor;
                GUI.contentColor = ColorTextPrimary;
                GUI.Label(rect, label, EditorStyles.miniLabel);
                GUI.contentColor = prev;
            }
        }

        public static T ObjectFieldRow<T>(GUIContent label, T value, bool allowSceneObjects = true) where T : UnityEngine.Object
        {
            T next = value;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelColumn));
                next = (T)EditorGUILayout.ObjectField(value, typeof(T), allowSceneObjects);
            }
            return next;
        }

        public static void StatusBanner(string text, Color tint, GUIContent? icon = null, float height = 22)
        {
            Rect rect = EditorGUILayout.GetControlRect(hasLabel: false, height, GUILayout.ExpandWidth(expand: true));
            EditorGUI.DrawRect(rect, tint);
            Color prev = GUI.contentColor;
            GUI.contentColor = Color.white;
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
            GUI.Label(rect, icon != null ? new GUIContent(text, icon.image) : new GUIContent(text), labelStyle);
            GUI.contentColor = prev;
        }

        public static void StatusBanner(string text, NoticeKind kind, GUIContent? icon = null, float height = 22)
        {
            StatusBanner(text, ColorForKind(kind), icon, height);
        }

        public static GUIContent TitleContent(string title, string? tooltip = null)
        {
            return new GUIContent(title, tooltip ?? title);
        }

        public static bool BrandLogoMark(float width = 40f, float height = 22f, float alpha = 0.9f)
        {
            if (BrandLogoTexture == null)
            {
                return false;
            }

            Rect rect = GUILayoutUtility.GetRect(
                width,
                height,
                GUILayout.Width(width),
                GUILayout.Height(height),
                GUILayout.ExpandWidth(expand: false));
            DrawBrandLogo(rect, alpha);
            return true;
        }

        public static void DrawBrandLogo(Rect rect, float alpha = 1f)
        {
            Texture2D? texture = BrandLogoTexture;
            if (texture == null || rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, previous.a * Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, alphaBlend: true);
            GUI.color = previous;
        }

        private static Texture2D? LoadBrandLogoTexture()
        {
            foreach (string path in BrandLogoAssetPaths)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    return texture;
                }
            }

            string[] guids = AssetDatabase.FindAssets(BrandLogoAssetName + " t:Texture2D");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith("/WhyKnotLogo.png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        public static void BrandFooter()
        {
            string heartColor = ColorUtility.ToHtmlStringRGB(ColorAccent);
            string text = "Made with <color=#" + heartColor + ">\u2665</color> by WhyKnot";
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(expand: true), GUILayout.MinHeight(22)))
            {
                if (BrandLogoTexture != null)
                {
                    BrandLogoMark(38f, 20f, 0.82f);
                }
                EditorGUILayout.LabelField(
                    new GUIContent(text, "Made with care by WhyKnot."),
                    BrandFooterStyle,
                    GUILayout.ExpandWidth(expand: true),
                    GUILayout.MinHeight(20));
            }
        }

        public static void WindowFooter()
        {
            Divider();
            BrandFooter();
        }

        public static void Checker(Rect rect, int squareSize = 8)
        {
            if (squareSize < 1)
            {
                squareSize = 1;
            }

            var c1 = new Color(0.35f, 0.35f, 0.35f, 1f);
            var c2 = new Color(0.25f, 0.25f, 0.25f, 1f);
            int cols = Mathf.CeilToInt(rect.width / squareSize);
            int rows = Mathf.CeilToInt(rect.height / squareSize);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var r = new Rect(
                        rect.x + (x * squareSize),
                        rect.y + (y * squareSize),
                        Mathf.Min(squareSize, rect.xMax - (rect.x + (x * squareSize))),
                        Mathf.Min(squareSize, rect.yMax - (rect.y + (y * squareSize))));
                    EditorGUI.DrawRect(r, ((x + y) & 1) == 0 ? c1 : c2);
                }
            }
        }

        public static void RectBorder(Rect rect, Color color, float thickness = 1f)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void TitleBar(string title, Uri? helpUrl = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, SectionTitle);
                if (helpUrl != null)
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("?", "Open the documentation for this tool."),
                            EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        Application.OpenURL(helpUrl.AbsoluteUri);
                    }
                }
            }
        }
    }
}
