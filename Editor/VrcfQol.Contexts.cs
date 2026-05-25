// VrcfQol.Contexts.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol {

    internal static partial class VrcfQol {

        // =========================== Context resolvers ========================

        internal struct FlipbookContext {
            public Component vrcfComponent;
            public object toggle;
            public string toggleName;
            public object flipbookAction;
            public IList pages;
            public int pageIndex;
        }

        internal struct ToggleContext {
            public Component vrcfComponent;
            public object toggle;
            public string toggleName;
            public object state;
            public IList actions;
            public object flipbookAction;
            public bool slider;
        }

        internal static bool TryResolveFlipbookFromPage(SerializedProperty pageProp, out FlipbookContext ctx) {
            ctx = default;
            if (pageProp == null) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            var r = Reflection;

            var m = PagePathRegex.Match(pageProp.propertyPath ?? "");
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups[1].Value, out var pageIndex)) return false;

            var comp = pageProp.serializedObject?.targetObject as Component;
            if (comp == null || comp.GetType() != r.VRCFuryType) return false;

            var content = r.ContentField.GetValue(comp);
            if (content == null || content.GetType() != r.ToggleType) return false;

            var state = r.ToggleStateField.GetValue(content);
            var toggleActions = r.StateActionsField.GetValue(state) as IList;
            var fb = FindFlipbookAction(toggleActions);
            if (fb == null) return false;
            var pages = r.PagesField.GetValue(fb) as IList;
            if (pages == null) return false;

            ctx = new FlipbookContext {
                vrcfComponent = comp,
                toggle = content,
                toggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                flipbookAction = fb,
                pages = pages,
                pageIndex = pageIndex,
            };
            return true;
        }

        internal static bool TryResolveFlipbookFromBuilder(SerializedProperty builderProp, out FlipbookContext ctx) {
            ctx = default;
            if (builderProp == null) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            var r = Reflection;

            var comp = builderProp.serializedObject?.targetObject as Component;
            if (comp == null || comp.GetType() != r.VRCFuryType) return false;
            var content = r.ContentField.GetValue(comp);
            if (content == null || content.GetType() != r.ToggleType) return false;
            var state = r.ToggleStateField.GetValue(content);
            var toggleActions = r.StateActionsField.GetValue(state) as IList;
            var fb = builderProp.managedReferenceValue;
            if (fb == null || fb.GetType() != r.FlipbookBuilderActionType) {
                fb = FindFlipbookAction(toggleActions);
            }
            if (fb == null) return false;
            var pages = r.PagesField.GetValue(fb) as IList;
            ctx = new FlipbookContext {
                vrcfComponent = comp,
                toggle = content,
                toggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                flipbookAction = fb,
                pages = pages,
                pageIndex = -1,
            };
            return true;
        }

        internal static bool TryResolveToggle(SerializedProperty contentProp, out ToggleContext ctx) {
            ctx = default;
            if (contentProp == null) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            var r = Reflection;

            var comp = contentProp.serializedObject?.targetObject as Component;
            if (comp == null || comp.GetType() != r.VRCFuryType) return false;
            var content = r.ContentField.GetValue(comp);
            if (content == null || content.GetType() != r.ToggleType) return false;

            var state = r.ToggleStateField.GetValue(content);
            var actions = r.StateActionsField.GetValue(state) as IList;
            var fb = FindFlipbookAction(actions);
            bool slider = false;
            try { if (r.ToggleSliderField != null) slider = (bool)r.ToggleSliderField.GetValue(content); } catch { slider = false; }

            ctx = new ToggleContext {
                vrcfComponent = comp,
                toggle = content,
                toggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                state = state,
                actions = actions,
                flipbookAction = fb,
                slider = slider,
            };
            return true;
        }

        internal static bool TryResolveFlipbookFromComponent(Component vrcf, out FlipbookContext ctx) {
            ctx = default;
            if (vrcf == null) return false;
            if (!Reflection.TryEnsure(out _)) return false;
            var r = Reflection;
            if (vrcf.GetType() != r.VRCFuryType) return false;
            var content = r.ContentField.GetValue(vrcf);
            if (content == null || content.GetType() != r.ToggleType) return false;
            var state = r.ToggleStateField.GetValue(content);
            var actions = r.StateActionsField.GetValue(state) as IList;
            var fb = FindFlipbookAction(actions);
            if (fb == null) return false;
            var pages = r.PagesField.GetValue(fb) as IList;
            ctx = new FlipbookContext {
                vrcfComponent = vrcf,
                toggle = content,
                toggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                flipbookAction = fb,
                pages = pages,
                pageIndex = -1,
            };
            return true;
        }
    }
}
