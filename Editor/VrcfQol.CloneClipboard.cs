// VrcfQol.CloneClipboard.cs

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

        // =========================== Clone / clipboard ========================

        internal static object DeepClonePage(object sourcePage) {
            var r = Reflection;
            var srcState = r.PageStateField.GetValue(sourcePage);
            var srcActions = r.StateActionsField.GetValue(srcState) as IList;

            var listType = r.StateActionsField.FieldType;
            var newActions = (IList)Activator.CreateInstance(listType);
            if (srcActions != null) {
                foreach (var action in srcActions) {
                    if (action == null) { newActions.Add(null); continue; }
                    var json = JsonUtility.ToJson(action);
                    var clone = JsonUtility.FromJson(json, action.GetType());
                    newActions.Add(clone);
                }
            }

            var newState = Activator.CreateInstance(r.StateType);
            r.StateActionsField.SetValue(newState, newActions);

            var newPage = Activator.CreateInstance(r.FlipbookPageType);
            r.PageStateField.SetValue(newPage, newState);
            return newPage;
        }

        internal static class PageClipboard {
            private static object _clone;
            private static string _sourceDescription;

            public static bool HasValue => _clone != null;
            public static string SourceDescription => _sourceDescription ?? "";

            public static void CopyFrom(FlipbookContext ctx) {
                if (ctx.pages == null || ctx.pageIndex < 0 || ctx.pageIndex >= ctx.pages.Count) return;
                _clone = DeepClonePage(ctx.pages[ctx.pageIndex]);
                _sourceDescription = $"Page #{ctx.pageIndex + 1} of \"{ctx.toggleName}\"";
            }

            public static object TakeClone() {
                if (_clone == null) return null;
                return DeepClonePage(_clone);
            }
        }

        // =========================== Helpers ==================================

        internal static object FindFlipbookAction(IList actions) {
            if (actions == null) return null;
            foreach (var a in actions) {
                if (a == null) continue;
                if (a.GetType() == Reflection.FlipbookBuilderActionType) return a;
            }
            return null;
        }
    }
}
