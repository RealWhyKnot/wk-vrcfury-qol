#nullable enable

using System;
using System.Collections;
using UnityEngine;

namespace UmeVrcfQol
{
    internal static partial class VrcfQol
    {
        internal static object DeepClonePage(object sourcePage)
        {
            ReflectionCache r = Reflection;
            if (!r.TryEnsure(out string? error))
            {
                throw new InvalidOperationException(error);
            }

            object srcState = r.PageStateField.GetValue(sourcePage);
            Type listType = r.StateActionsField.FieldType;
            var newActions = (IList)Activator.CreateInstance(listType);
            if (r.StateActionsField.GetValue(srcState) is IList srcActions)
            {
                foreach (object? action in srcActions)
                {
                    if (action == null)
                    {
                        newActions.Add(value: null);
                        continue;
                    }

                    string json = JsonUtility.ToJson(action);
                    object clone = JsonUtility.FromJson(json, action.GetType());
                    newActions.Add(clone);
                }
            }

            object newState = Activator.CreateInstance(r.StateType);
            r.StateActionsField.SetValue(newState, newActions);

            object newPage = Activator.CreateInstance(r.FlipbookPageType);
            r.PageStateField.SetValue(newPage, newState);
            return newPage;
        }

        internal static object? FindFlipbookAction(IList? actions)
        {
            if (actions == null)
            {
                return null;
            }

            foreach (object? a in actions)
            {
                if (a == null)
                {
                    continue;
                }

                if (a.GetType() == Reflection.FlipbookBuilderActionType)
                {
                    return a;
                }
            }
            return null;
        }
    }
}
