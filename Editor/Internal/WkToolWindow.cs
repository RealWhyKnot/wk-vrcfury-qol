// WkToolWindow.cs
//
// Abstract base for WhyKnot tool windows. Absorbs the title-bar +
// theme-scope + optional-scroll-body + optional-footer boilerplate
// every EditorWindow subclass in the family reinvents, so a new tool
// only has to fill in OnBodyGUI plus a handful of property overrides.
//
// Subclass shape:
//   public sealed class MyToolWindow : WkToolWindow {
//       protected override string Title => "My Tool";
//       protected override string HelpUrl => "https://...";
//       protected override void OnBodyGUI() { ... }
//   }
//
// Theme defaults to WkTheme.WhyKnot. Override Theme to render inside
// VRCFury's chrome by returning WkTheme.VRCFury.

using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Styling;

namespace UmeVrcfQol.Internal {

    public abstract class WkToolWindow : EditorWindow {

        /// <summary>Title rendered by the built-in TitleBar at the top of OnGUI.</summary>
        protected abstract string Title { get; }

        /// <summary>Optional documentation URL. When set, a `?` help icon appears on the right of the title row.</summary>
        protected virtual string HelpUrl => null;

        /// <summary>Theme scope opened around OnBodyGUI / OnFooterGUI. Defaults to <see cref="WkTheme.WhyKnot"/>.</summary>
        protected virtual WkTheme Theme => WkTheme.WhyKnot;

        /// <summary>Initial window <see cref="EditorWindow.minSize"/>.</summary>
        protected virtual Vector2 InitialMinSize => new Vector2(420, 320);

        /// <summary>When true (default), the body is wrapped in a ScrollViewScope.</summary>
        protected virtual bool ShowScrollView => true;

        /// <summary>When true (default), a divider + footer row renders below the body.</summary>
        protected virtual bool ShowFooter => true;

        /// <summary>Subclass-supplied window body. Called inside the theme scope.</summary>
        protected abstract void OnBodyGUI();

        /// <summary>
        /// Override to render the footer row. Default draws a right-aligned
        /// Close button so every window has a consistent exit affordance.
        /// </summary>
        protected virtual void OnFooterGUI() {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Height(22), GUILayout.Width(100))) {
                Close();
            }
        }

        protected virtual void OnEnable() {
            titleContent = new GUIContent(Title);
            minSize = InitialMinSize;
        }

        private Vector2 _scroll;

        private void OnGUI() {
            using (WkStyles.Scope(Theme)) {
                WkStyles.TitleBar(Title, HelpUrl);
                WkStyles.Divider();
                if (ShowScrollView) {
                    using (var s = new EditorGUILayout.ScrollViewScope(_scroll)) {
                        _scroll = s.scrollPosition;
                        OnBodyGUI();
                    }
                } else {
                    OnBodyGUI();
                }
                if (ShowFooter) {
                    WkStyles.Divider();
                    using (new EditorGUILayout.HorizontalScope()) {
                        OnFooterGUI();
                    }
                }
            }
        }
    }
}
