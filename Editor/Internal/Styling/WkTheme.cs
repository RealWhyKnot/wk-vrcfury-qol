// WkTheme.cs
//
// A WkTheme defines the palette and chrome that WkStyles emits. Each
// theme carries two Variants -- one for the dark "Pro" editor skin and
// one for the light "Personal" editor skin. The active theme is picked
// per OnGUI scope via WkStyles.Scope(theme); the WkStyles palette
// properties resolve through Theme.Current at draw time so flipping the
// editor skin at runtime takes effect on the next repaint without code
// changes.
//
// Two presets ship in core:
//   WkTheme.WhyKnot  -- black / gray / light-blue. Default for any
//                       WhyKnot tool that doesn't sit inside someone
//                       else's chrome.
//   WkTheme.VRCFury  -- mimics VRCFury's own dark-gray rows + warm
//                       accents so tools rendered next to VRCFury
//                       components don't visually compete.

using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Styling {

    public sealed class WkTheme {

        public Variant Pro;
        public Variant Personal;

        /// <summary>Variant chosen by the current EditorGUIUtility.isProSkin.</summary>
        public Variant Current => EditorGUIUtility.isProSkin ? Pro : Personal;

        public sealed class Variant {

            /// <summary>Primary surface color: banner backgrounds, panel fills.</summary>
            public Color Background;

            /// <summary>Secondary surface: alternating rows, sub-cards.</summary>
            public Color BackgroundAlt;

            /// <summary>Tertiary surface: emphasised inset cards inside a Section.</summary>
            public Color BackgroundEmphasis;

            /// <summary>Brand accent: primary buttons, suggested-card border, links.</summary>
            public Color Accent;

            /// <summary>Warning notices and amber banners.</summary>
            public Color Warning;

            /// <summary>Success notices ("scan clean", green banners).</summary>
            public Color Success;

            /// <summary>Neutral information notices and gray pills.</summary>
            public Color Info;

            /// <summary>Destructive-action signal: red button background, "Stop Previewing", etc.</summary>
            public Color Danger;

            /// <summary>Hairline divider tint with alpha baked in.</summary>
            public Color Divider;

            /// <summary>Lower-contrast divider used between rows in dense lists.</summary>
            public Color DividerSubtle;

            /// <summary>Default label color.</summary>
            public Color TextPrimary;

            /// <summary>Secondary label color (italic notes, descriptions, hints).</summary>
            public Color TextMuted;

            /// <summary>Border for cards and pills.</summary>
            public Color Border;

            /// <summary>Hover-highlight color for buttons and selectable rows.</summary>
            public Color ButtonHover;
        }

        // -------------------------------------------------------------
        // WhyKnot theme: black / gray / light blue.
        // The brand palette. Default theme that applies to anything not
        // explicitly inside a third-party chrome.
        // -------------------------------------------------------------
        public static readonly WkTheme WhyKnot = new WkTheme {
            Pro = new Variant {
                Background         = new Color(0.16f, 0.16f, 0.18f, 1f),
                BackgroundAlt      = new Color(0.22f, 0.22f, 0.25f, 1f),
                BackgroundEmphasis = new Color(0.26f, 0.26f, 0.30f, 1f),
                Accent             = new Color(0.42f, 0.68f, 1.00f, 1f),  // light blue
                Warning            = new Color(0.85f, 0.65f, 0.30f, 1f),
                Success            = new Color(0.42f, 0.75f, 0.50f, 1f),
                Info               = new Color(0.60f, 0.65f, 0.75f, 1f),
                Danger             = new Color(0.85f, 0.30f, 0.30f, 1f),
                Divider            = new Color(1.00f, 1.00f, 1.00f, 0.10f),
                DividerSubtle      = new Color(1.00f, 1.00f, 1.00f, 0.05f),
                TextPrimary        = new Color(0.92f, 0.92f, 0.94f, 1f),
                TextMuted          = new Color(0.65f, 0.65f, 0.68f, 1f),
                Border             = new Color(0.10f, 0.10f, 0.12f, 1f),
                ButtonHover        = new Color(0.30f, 0.30f, 0.34f, 1f),
            },
            Personal = new Variant {
                Background         = new Color(0.95f, 0.95f, 0.96f, 1f),
                BackgroundAlt      = new Color(0.90f, 0.90f, 0.92f, 1f),
                BackgroundEmphasis = new Color(0.86f, 0.86f, 0.88f, 1f),
                Accent             = new Color(0.20f, 0.55f, 0.95f, 1f),  // light-blue, more saturated for contrast on light bg
                Warning            = new Color(0.85f, 0.60f, 0.20f, 1f),
                Success            = new Color(0.25f, 0.60f, 0.35f, 1f),
                Info               = new Color(0.50f, 0.55f, 0.65f, 1f),
                Danger             = new Color(0.80f, 0.25f, 0.25f, 1f),
                Divider            = new Color(0.00f, 0.00f, 0.00f, 0.15f),
                DividerSubtle      = new Color(0.00f, 0.00f, 0.00f, 0.06f),
                TextPrimary        = new Color(0.10f, 0.10f, 0.12f, 1f),
                TextMuted          = new Color(0.40f, 0.40f, 0.45f, 1f),
                Border             = new Color(0.75f, 0.75f, 0.78f, 1f),
                ButtonHover        = new Color(0.82f, 0.82f, 0.86f, 1f),
            },
        };

        // -------------------------------------------------------------
        // VRCFury theme: matches VRCFury's existing dark-gray row chrome
        // with warm-tinted accents so banners and inline tools rendered
        // inside the VRCFury inspector read as part of the same surface.
        // Values lifted from VRCFury's own component header rows + the
        // existing inspector overlay's banner palette.
        // -------------------------------------------------------------
        public static readonly WkTheme VRCFury = new WkTheme {
            Pro = new Variant {
                Background         = new Color(0.20f, 0.20f, 0.20f, 1f),
                BackgroundAlt      = new Color(0.25f, 0.25f, 0.25f, 1f),
                BackgroundEmphasis = new Color(0.28f, 0.28f, 0.28f, 1f),
                Accent             = new Color(0.45f, 0.65f, 0.85f, 1f),
                Warning            = new Color(0.78f, 0.54f, 0.18f, 1f),
                Success            = new Color(0.42f, 0.70f, 0.45f, 1f),
                Info               = new Color(0.55f, 0.55f, 0.55f, 1f),
                Danger             = new Color(0.64f, 0.10f, 0.10f, 1f),
                Divider            = new Color(0.00f, 0.00f, 0.00f, 0.35f),
                DividerSubtle      = new Color(0.00f, 0.00f, 0.00f, 0.18f),
                TextPrimary        = new Color(0.92f, 0.92f, 0.92f, 1f),
                TextMuted          = new Color(0.78f, 0.78f, 0.78f, 1f),
                Border             = new Color(0.00f, 0.00f, 0.00f, 0.50f),
                ButtonHover        = new Color(0.32f, 0.32f, 0.32f, 1f),
            },
            Personal = new Variant {
                // VRCFury is dark-mode-first; the personal-skin values
                // here keep the same hue family but brighten enough that
                // text remains legible on a light editor background.
                Background         = new Color(0.85f, 0.85f, 0.85f, 1f),
                BackgroundAlt      = new Color(0.78f, 0.78f, 0.78f, 1f),
                BackgroundEmphasis = new Color(0.72f, 0.72f, 0.72f, 1f),
                Accent             = new Color(0.30f, 0.50f, 0.75f, 1f),
                Warning            = new Color(0.78f, 0.54f, 0.18f, 1f),
                Success            = new Color(0.30f, 0.60f, 0.35f, 1f),
                Info               = new Color(0.45f, 0.45f, 0.45f, 1f),
                Danger             = new Color(0.65f, 0.15f, 0.15f, 1f),
                Divider            = new Color(0.00f, 0.00f, 0.00f, 0.25f),
                DividerSubtle      = new Color(0.00f, 0.00f, 0.00f, 0.10f),
                TextPrimary        = new Color(0.12f, 0.12f, 0.12f, 1f),
                TextMuted          = new Color(0.35f, 0.35f, 0.35f, 1f),
                Border             = new Color(0.45f, 0.45f, 0.45f, 1f),
                ButtonHover        = new Color(0.92f, 0.92f, 0.92f, 1f),
            },
        };
    }
}
