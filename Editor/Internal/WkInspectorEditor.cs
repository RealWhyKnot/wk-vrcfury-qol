// WkInspectorEditor.cs
//
// Abstract base for custom-inspector Editors. Opens a WkStyles.Scope
// around OnInspectorGUI so primitives inside resolve through the right
// theme, and renders an optional help icon row at the top.
//
// Subclass shape:
//   [CustomEditor(typeof(MyComponent))]
//   public sealed class MyComponentEditor : WkInspectorEditor {
//       protected override string HelpUrl => "https://...";
//       protected override void OnBodyGUI() { ... }
//   }
//
// Theme defaults to WkTheme.WhyKnot. Override Theme to render inside
// VRCFury's chrome (e.g. for an inspector that decorates a VRCFury
// component) by returning WkTheme.VRCFury.

using UmeVrcfQol.Internal.Styling;

namespace UmeVrcfQol.Internal {

    public abstract class WkInspectorEditor : UnityEditor.Editor {

        /// <summary>Optional documentation URL. When set, a `?` help icon appears at the top of the inspector.</summary>
        protected virtual string HelpUrl => null;

        /// <summary>Theme scope opened around OnBodyGUI. Defaults to <see cref="WkTheme.WhyKnot"/>.</summary>
        protected virtual WkTheme Theme => WkTheme.WhyKnot;

        /// <summary>Subclass-supplied inspector body. Called inside the theme scope.</summary>
        protected abstract void OnBodyGUI();

        public override void OnInspectorGUI() {
            using (WkStyles.Scope(Theme)) {
                if (!string.IsNullOrEmpty(HelpUrl)) {
                    WkStyles.HelpIcon(HelpUrl);
                }
                OnBodyGUI();
            }
        }
    }
}
