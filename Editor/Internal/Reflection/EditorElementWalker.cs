// EditorElementWalker.cs
//
// UIElements-side helpers that walk Unity's Inspector tree. These hook
// into UIElements types whose names Unity exposes only by string
// (EditorElement, InspectorElement) -- reflection-by-name is the
// supported pattern, since the concrete types live in internal Editor
// assemblies that shift between Unity versions.
//
// The button-style helpers exist so an inspector overlay's inline
// buttons, page bars, and per-action tools all read as the same control
// type without each site reimplementing the padding/margin tweaks.

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UmeVrcfQol.Internal.Styling;

namespace UmeVrcfQol.Internal.Reflection {

    public static class EditorElementWalker {

        /// <summary>
        /// From a UIElements EditorElement (one component slot in the
        /// Inspector), find the inner InspectorElement that holds the
        /// custom-editor's drawn content. Falls back to the editorElement
        /// itself when no InspectorElement child is present.
        /// </summary>
        public static VisualElement FindInspectorContent(VisualElement editorElement) {
            if (editorElement == null) return null;
            foreach (var child in editorElement.Children()) {
                if (child.GetType().Name == "InspectorElement") return child;
            }
            foreach (var desc in editorElement.Query<VisualElement>().ToList()) {
                if (desc != editorElement && desc.GetType().Name == "InspectorElement") return desc;
            }
            return editorElement;
        }

        /// <summary>
        /// Depth-first enumerate all EditorElement and InspectorElement
        /// descendants of <paramref name="root"/> (inclusive).
        /// </summary>
        public static IEnumerable<VisualElement> EnumerateEditorWrappers(VisualElement root) {
            if (root == null) yield break;
            var stack = new Stack<VisualElement>();
            stack.Push(root);
            while (stack.Count > 0) {
                var cur = stack.Pop();
                var n = cur.GetType().Name;
                if (n == "EditorElement" || n == "InspectorElement") yield return cur;
                for (int i = 0; i < cur.childCount; i++) stack.Push(cur[i]);
            }
        }

        /// <summary>
        /// Pulls the Component target out of an EditorElement / InspectorElement
        /// by reflecting against any field or property whose declared type is
        /// (or derives from) UnityEditor.Editor. Returns false on any other
        /// VisualElement type, or when the editor's target isn't a Component.
        /// </summary>
        public static bool TryGetEditorTarget(VisualElement element, out Component component) {
            component = null;
            if (element == null) return false;
            var n = element.GetType().Name;
            if (n != "EditorElement" && n != "InspectorElement") return false;

            var t = element.GetType();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public
                                     | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            while (t != null && t != typeof(VisualElement)) {
                foreach (var f in t.GetFields(flags)) {
                    if (!typeof(Editor).IsAssignableFrom(f.FieldType)) continue;
                    object value;
                    try { value = f.GetValue(element); } catch { continue; }
                    if (value is Editor ed && ed != null && ed.target is Component comp) {
                        component = comp;
                        return true;
                    }
                }
                foreach (var p in t.GetProperties(flags)) {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    if (!typeof(Editor).IsAssignableFrom(p.PropertyType)) continue;
                    object value;
                    try { value = p.GetValue(element); } catch { continue; }
                    if (value is Editor ed && ed != null && ed.target is Component comp) {
                        component = comp;
                        return true;
                    }
                }
                t = t.BaseType;
            }
            return false;
        }

        // --- Banner / inline-button styling ----------------------------------

        /// <summary>
        /// Shared chrome for an inspector banner: themed one-line row with
        /// a thin colored left edge. Reads <see cref="WkStyles.Current"/>
        /// at apply time so callers inside a <see cref="WkStyles.Scope"/>
        /// get the active theme's background + divider; pass an explicit
        /// <paramref name="theme"/> to override. Caller sets the left-edge
        /// color via <see cref="VisualElement.style"/>.borderLeftColor
        /// afterwards (semantic accent is caller-supplied).
        /// </summary>
        public static void ApplyBannerChromeStyle(VisualElement banner, WkTheme theme = null) {
            if (banner == null) return;
            var v = theme != null ? theme.Current : WkStyles.Current;
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.paddingLeft = 6;
            banner.style.paddingRight = 4;
            banner.style.paddingTop = 2;
            banner.style.paddingBottom = 2;
            banner.style.marginTop = 0;
            banner.style.marginBottom = 1;
            banner.style.backgroundColor = new StyleColor(v.Background);
            banner.style.borderLeftWidth = 3;
            banner.style.borderBottomWidth = 1;
            banner.style.borderBottomColor = new StyleColor(v.Divider);
        }

        /// <summary>
        /// Compact inline-button chrome so a banner, page bar, and per-action
        /// tools all read as the same control type. Pure margin/padding;
        /// background and text color are not touched.
        /// </summary>
        public static void ApplyInlineButtonStyle(Button btn) {
            if (btn == null) return;
            btn.style.marginLeft = 4;
            btn.style.marginTop = 0;
            btn.style.marginBottom = 0;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop = 1;
            btn.style.paddingBottom = 1;
            btn.style.flexShrink = 0;
        }

        /// <summary>
        /// Toggle a button between neutral and "danger" styling (themed
        /// red background, white bold text). Used to signal destructive
        /// states like "Stop Previewing". Reads <see cref="WkStyles.Current"/>
        /// at apply time; pass an explicit <paramref name="theme"/> to
        /// override.
        /// </summary>
        public static void ApplyDangerButtonStyle(Button btn, bool danger, WkTheme theme = null) {
            if (btn == null) return;
            if (danger) {
                var v = theme != null ? theme.Current : WkStyles.Current;
                btn.style.backgroundColor = new StyleColor(v.Danger);
                btn.style.color = new StyleColor(Color.white);
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                return;
            }
            btn.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            btn.style.color = new StyleColor(StyleKeyword.Null);
            btn.style.unityFontStyleAndWeight = FontStyle.Normal;
        }
    }
}
