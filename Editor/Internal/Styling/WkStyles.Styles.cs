#nullable enable

using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Styling
{
    public static partial class WkStyles
    {
        private static GUIStyle? CachedSectionTitle;
        private static GUIStyle? CachedSubsectionTitle;
        private static GUIStyle? CachedBody;
        private static GUIStyle? CachedMuted;
        private static GUIStyle? CachedMono;
        private static GUIStyle? CachedPrimaryButton;
        private static GUIStyle? CachedMiniRowButton;
        private static GUIStyle? CachedBadgePillStyle;
        private static GUIStyle? CachedCardSelected;
        private static GUIStyle? CachedFoldoutHeader;
        private static GUIStyle? CachedCaption;
        private static GUIStyle? CachedCode;
        private static GUIStyle? CachedTitleBar;
        private static GUIStyle? CachedRowAlt;
        private static GUIStyle? CachedBrandFooter;

        public static GUIStyle SectionTitle =>
            CachedSectionTitle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 4, 2),
            };

        public static GUIStyle SubsectionTitle =>
            CachedSubsectionTitle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 2, 2),
            };

        public static GUIStyle Body =>
            CachedBody ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
            };

        public static GUIStyle Muted =>
            CachedMuted ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
            };

        public static GUIStyle Mono =>
            CachedMono ??= BuildMono();

        public static GUIStyle PrimaryButton =>
            CachedPrimaryButton ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 28f,
                padding = new RectOffset(16, 16, 6, 6),
            };

        public static GUIStyle MiniRowButton =>
            CachedMiniRowButton ??= new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2),
            };

        public static GUIStyle BadgePillStyle =>
            CachedBadgePillStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 1, 1),
                margin = new RectOffset(0, 4, 1, 1),
                normal = { textColor = Color.white },
            };

        public static GUIStyle CardSelected =>
            CachedCardSelected ??= new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(2, 2, 2, 2),
            };

        public static GUIStyle FoldoutHeader =>
            CachedFoldoutHeader ??= new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
            };

        public static GUIStyle Caption =>
            CachedCaption ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
            };

        public static GUIStyle Code =>
            CachedCode ??= BuildCode();

        public static GUIStyle TitleBarStyle =>
            CachedTitleBar ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 4, 2),
                padding = new RectOffset(0, 26, 0, 0),
            };

        public static GUIStyle RowAlt =>
            CachedRowAlt ??= new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(4, 4, 2, 2),
                margin = new RectOffset(0, 0, 0, 0),
            };

        public static GUIStyle BrandFooterStyle =>
            CachedBrandFooter ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(4, 4, 2, 2),
                margin = new RectOffset(0, 0, 0, 0),
            };

        private static readonly string[] MonoFontNames = { "Consolas", "Menlo", "Courier New" };

        private static Font? FindMonoFont()
        {
            foreach (string name in MonoFontNames)
            {
                var candidate = Font.CreateDynamicFontFromOSFont(name, 10);
                if (candidate)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static GUIStyle BuildMono()
        {
            Font? font = FindMonoFont();
            var s = new GUIStyle(EditorStyles.label) { fontSize = 10 };
            if (font != null)
            {
                s.font = font;
            }

            return s;
        }

        private static GUIStyle BuildCode()
        {
            GUIStyle s = BuildMono();
            s.wordWrap = false;
            s.richText = false;
            return s;
        }
    }
}
