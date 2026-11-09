#nullable enable

using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Styling
{
    public sealed class WkTheme
    {
        public WkThemeVariant Pro { get; set; } = new();
        public WkThemeVariant Personal { get; set; } = new();

        public WkThemeVariant Current => EditorGUIUtility.isProSkin ? Pro : Personal;

        public static readonly WkTheme WhyKnot = new()
        {
            Pro = new WkThemeVariant
            {
                Background = new Color(0.16f, 0.16f, 0.18f, 1f),
                BackgroundAlt = new Color(0.22f, 0.22f, 0.25f, 1f),
                BackgroundEmphasis = new Color(0.26f, 0.26f, 0.30f, 1f),
                Accent = new Color(0.42f, 0.68f, 1.00f, 1f),
                Warning = new Color(0.85f, 0.65f, 0.30f, 1f),
                Success = new Color(0.42f, 0.75f, 0.50f, 1f),
                Info = new Color(0.60f, 0.65f, 0.75f, 1f),
                Danger = new Color(0.85f, 0.30f, 0.30f, 1f),
                Divider = new Color(1.00f, 1.00f, 1.00f, 0.10f),
                DividerSubtle = new Color(1.00f, 1.00f, 1.00f, 0.05f),
                TextPrimary = new Color(0.92f, 0.92f, 0.94f, 1f),
                TextMuted = new Color(0.65f, 0.65f, 0.68f, 1f),
                Border = new Color(0.10f, 0.10f, 0.12f, 1f),
                ButtonHover = new Color(0.30f, 0.30f, 0.34f, 1f),
            },
            Personal = new WkThemeVariant
            {
                Background = new Color(0.95f, 0.95f, 0.96f, 1f),
                BackgroundAlt = new Color(0.90f, 0.90f, 0.92f, 1f),
                BackgroundEmphasis = new Color(0.86f, 0.86f, 0.88f, 1f),
                Accent = new Color(0.20f, 0.55f, 0.95f, 1f),
                Warning = new Color(0.85f, 0.60f, 0.20f, 1f),
                Success = new Color(0.25f, 0.60f, 0.35f, 1f),
                Info = new Color(0.50f, 0.55f, 0.65f, 1f),
                Danger = new Color(0.80f, 0.25f, 0.25f, 1f),
                Divider = new Color(0.00f, 0.00f, 0.00f, 0.15f),
                DividerSubtle = new Color(0.00f, 0.00f, 0.00f, 0.06f),
                TextPrimary = new Color(0.10f, 0.10f, 0.12f, 1f),
                TextMuted = new Color(0.40f, 0.40f, 0.45f, 1f),
                Border = new Color(0.75f, 0.75f, 0.78f, 1f),
                ButtonHover = new Color(0.82f, 0.82f, 0.86f, 1f),
            },
        };

        public static readonly WkTheme VRCFury = new()
        {
            Pro = new WkThemeVariant
            {
                Background = new Color(0.20f, 0.20f, 0.20f, 1f),
                BackgroundAlt = new Color(0.25f, 0.25f, 0.25f, 1f),
                BackgroundEmphasis = new Color(0.28f, 0.28f, 0.28f, 1f),
                Accent = new Color(0.45f, 0.65f, 0.85f, 1f),
                Warning = new Color(0.78f, 0.54f, 0.18f, 1f),
                Success = new Color(0.42f, 0.70f, 0.45f, 1f),
                Info = new Color(0.55f, 0.55f, 0.55f, 1f),
                Danger = new Color(0.64f, 0.10f, 0.10f, 1f),
                Divider = new Color(0.00f, 0.00f, 0.00f, 0.35f),
                DividerSubtle = new Color(0.00f, 0.00f, 0.00f, 0.18f),
                TextPrimary = new Color(0.92f, 0.92f, 0.92f, 1f),
                TextMuted = new Color(0.78f, 0.78f, 0.78f, 1f),
                Border = new Color(0.00f, 0.00f, 0.00f, 0.50f),
                ButtonHover = new Color(0.32f, 0.32f, 0.32f, 1f),
            },
            Personal = new WkThemeVariant
            {
                Background = new Color(0.85f, 0.85f, 0.85f, 1f),
                BackgroundAlt = new Color(0.78f, 0.78f, 0.78f, 1f),
                BackgroundEmphasis = new Color(0.72f, 0.72f, 0.72f, 1f),
                Accent = new Color(0.30f, 0.50f, 0.75f, 1f),
                Warning = new Color(0.78f, 0.54f, 0.18f, 1f),
                Success = new Color(0.30f, 0.60f, 0.35f, 1f),
                Info = new Color(0.45f, 0.45f, 0.45f, 1f),
                Danger = new Color(0.65f, 0.15f, 0.15f, 1f),
                Divider = new Color(0.00f, 0.00f, 0.00f, 0.25f),
                DividerSubtle = new Color(0.00f, 0.00f, 0.00f, 0.10f),
                TextPrimary = new Color(0.12f, 0.12f, 0.12f, 1f),
                TextMuted = new Color(0.35f, 0.35f, 0.35f, 1f),
                Border = new Color(0.45f, 0.45f, 0.45f, 1f),
                ButtonHover = new Color(0.92f, 0.92f, 0.92f, 1f),
            },
        };
    }
}
