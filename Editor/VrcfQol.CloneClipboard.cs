// VrcfQol.CloneClipboard.cs

using System;
using System.Collections;
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
