#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using UmeVrcfQol.Internal.Styling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmeVrcfQol.Internal.Reflection
{
    public static class EditorElementWalker
    {
        public static VisualElement? FindInspectorContent(VisualElement editorElement)
        {
            if (editorElement == null)
            {
                return null;
            }

            foreach (VisualElement? child in editorElement.Children())
            {
                if (string.Equals(child.GetType().Name, "InspectorElement", StringComparison.Ordinal))
                {
                    return child;
                }
            }
            foreach (VisualElement desc in editorElement.Query<VisualElement>().ToList())
            {
                if (desc != editorElement && string.Equals(desc.GetType().Name, "InspectorElement", StringComparison.Ordinal))
                {
                    return desc;
                }
            }
            return editorElement;
        }

        public static IEnumerable<VisualElement> EnumerateEditorWrappers(VisualElement root)
        {
            if (root == null)
            {
                yield break;
            }

            var stack = new Stack<VisualElement>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                VisualElement cur = stack.Pop();
                string n = cur.GetType().Name;
                if (n is "EditorElement" or "InspectorElement")
                {
                    yield return cur;
                }

                for (int i = 0; i < cur.childCount; i++)
                {
                    stack.Push(cur[i]);
                }
            }
        }

        private static object? ReadMember(Func<object?> read)
        {
            try
            {
                return read();
            }
            catch (Exception ex) when (ex is TargetInvocationException or TargetException or MemberAccessException
                or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                return null;
            }
        }

        public static bool TryGetEditorTarget(VisualElement element, out Component? component)
        {
            component = null;
            if (element == null)
            {
                return false;
            }

            string n = element.GetType().Name;
            if (n is not "EditorElement" and not "InspectorElement")
            {
                return false;
            }

            Type t = element.GetType();
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public
                                     | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            while (t != null && t != typeof(VisualElement))
            {
                foreach (FieldInfo? f in t.GetFields(Flags))
                {
                    if (!typeof(Editor).IsAssignableFrom(f.FieldType))
                    {
                        continue;
                    }

                    if (ReadMember(() => f.GetValue(element)) is Editor ed && ed && ed.target is Component comp)
                    {
                        component = comp;
                        return true;
                    }
                }
                foreach (PropertyInfo? p in t.GetProperties(Flags))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    if (!typeof(Editor).IsAssignableFrom(p.PropertyType))
                    {
                        continue;
                    }

                    if (ReadMember(() => p.GetValue(element)) is Editor ed && ed && ed.target is Component comp)
                    {
                        component = comp;
                        return true;
                    }
                }
                t = t.BaseType;
            }
            return false;
        }

        public static void ApplyBannerChromeStyle(VisualElement banner, WkTheme? theme = null)
        {
            if (banner == null)
            {
                return;
            }

            WkThemeVariant v = theme != null ? theme.Current : WkStyles.Current;
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

        public static void ApplyInlineButtonStyle(Button btn)
        {
            if (btn == null)
            {
                return;
            }

            btn.style.marginLeft = 4;
            btn.style.marginTop = 0;
            btn.style.marginBottom = 0;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop = 1;
            btn.style.paddingBottom = 1;
            btn.style.flexShrink = 0;
        }

        public static void ApplyDangerButtonStyle(Button btn, bool danger, WkTheme? theme = null)
        {
            if (btn == null)
            {
                return;
            }

            if (danger)
            {
                WkThemeVariant v = theme != null ? theme.Current : WkStyles.Current;
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
