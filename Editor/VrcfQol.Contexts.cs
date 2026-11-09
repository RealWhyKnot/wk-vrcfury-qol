#nullable enable

using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol
{
    internal static partial class VrcfQol
    {
        internal struct FlipbookContext
        {
            public Component VrcfComponent;
            public object Toggle;
            public string ToggleName;
            public object FlipbookAction;
            public IList? Pages;
            public int PageIndex;
        }

        internal struct ToggleContext
        {
            public Component VrcfComponent;
            public object Toggle;
            public string ToggleName;
            public object State;
            public IList? Actions;
            public object? FlipbookAction;
            public bool Slider;
        }

        internal static bool TryResolveFlipbookFromPage(SerializedProperty pageProp, out FlipbookContext ctx)
        {
            ctx = default;
            return pageProp != null
                && TryResolveFlipbookFromPagePath(
                    pageProp.serializedObject?.targetObject as Component, pageProp.propertyPath, out ctx);
        }

        internal static bool TryResolveFlipbookFromPagePath(Component? comp, string pagePath, out FlipbookContext ctx)
        {
            ctx = default;
            ReflectionCache r = Reflection;
            if (!r.TryEnsure(out _))
            {
                return false;
            }

            if (pagePath == null)
            {
                return false;
            }

            Match m = PagePathRegex.Match(pagePath);
            if (!m.Success)
            {
                return false;
            }

            if (!int.TryParse(m.Groups["page"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pageIndex))
            {
                return false;
            }

            if (comp == null || comp.GetType() != r.VRCFuryType)
            {
                return false;
            }

            object content = r.ContentField.GetValue(comp);
            if (content == null || content.GetType() != r.ToggleType)
            {
                return false;
            }

            object? fb = Tools.DuplicateActionTool.Walk(comp, pagePath[..m.Index]);
            if (fb == null || fb.GetType() != r.FlipbookBuilderActionType)
            {
                return false;
            }

            if (r.PagesField.GetValue(fb) is not IList pages || pageIndex >= pages.Count)
            {
                return false;
            }

            ctx = new FlipbookContext
            {
                VrcfComponent = comp,
                Toggle = content,
                ToggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                FlipbookAction = fb,
                Pages = pages,
                PageIndex = pageIndex,
            };
            return true;
        }

        internal static bool TryResolveFlipbookFromBuilder(SerializedProperty builderProp, out FlipbookContext ctx)
        {
            ctx = default;
            if (builderProp == null)
            {
                return false;
            }

            ReflectionCache r = Reflection;
            if (!r.TryEnsure(out _))
            {
                return false;
            }

            var comp = builderProp.serializedObject?.targetObject as Component;
            if (comp == null || comp.GetType() != r.VRCFuryType)
            {
                return false;
            }

            object content = r.ContentField.GetValue(comp);
            if (content == null || content.GetType() != r.ToggleType)
            {
                return false;
            }

            object state = r.ToggleStateField.GetValue(content);
            var toggleActions = r.StateActionsField.GetValue(state) as IList;
            object? fb = builderProp.managedReferenceValue;
            if (fb == null || fb.GetType() != r.FlipbookBuilderActionType)
            {
                fb = FindFlipbookAction(toggleActions);
            }
            if (fb == null)
            {
                return false;
            }

            var pages = r.PagesField.GetValue(fb) as IList;
            ctx = new FlipbookContext
            {
                VrcfComponent = comp,
                Toggle = content,
                ToggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                FlipbookAction = fb,
                Pages = pages,
                PageIndex = -1,
            };
            return true;
        }

        internal static bool TryResolveToggle(SerializedProperty contentProp, out ToggleContext ctx)
        {
            ctx = default;
            if (contentProp == null)
            {
                return false;
            }

            ReflectionCache r = Reflection;
            if (!r.TryEnsure(out _))
            {
                return false;
            }

            var comp = contentProp.serializedObject?.targetObject as Component;
            if (comp == null || comp.GetType() != r.VRCFuryType)
            {
                return false;
            }

            object content = r.ContentField.GetValue(comp);
            if (content == null || content.GetType() != r.ToggleType)
            {
                return false;
            }

            object state = r.ToggleStateField.GetValue(content);
            var actions = r.StateActionsField.GetValue(state) as IList;
            object? fb = FindFlipbookAction(actions);
            bool slider = ReadToggleSlider(r.ToggleSliderField, content);

            ctx = new ToggleContext
            {
                VrcfComponent = comp,
                Toggle = content,
                ToggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                State = state,
                Actions = actions,
                FlipbookAction = fb,
                Slider = slider,
            };
            return true;
        }

        internal static bool TryResolveFlipbookFromComponent(Component vrcf, out FlipbookContext ctx)
        {
            ctx = default;
            if (vrcf == null)
            {
                return false;
            }

            ReflectionCache r = Reflection;
            if (!r.TryEnsure(out _))
            {
                return false;
            }
            if (vrcf.GetType() != r.VRCFuryType)
            {
                return false;
            }

            object content = r.ContentField.GetValue(vrcf);
            if (content == null || content.GetType() != r.ToggleType)
            {
                return false;
            }

            object state = r.ToggleStateField.GetValue(content);
            var actions = r.StateActionsField.GetValue(state) as IList;
            object? fb = FindFlipbookAction(actions);
            if (fb == null)
            {
                return false;
            }

            var pages = r.PagesField.GetValue(fb) as IList;
            ctx = new FlipbookContext
            {
                VrcfComponent = vrcf,
                Toggle = content,
                ToggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                FlipbookAction = fb,
                Pages = pages,
                PageIndex = -1,
            };
            return true;
        }
    }
}
