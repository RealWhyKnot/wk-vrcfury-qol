#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using UmeVrcfQol.Internal.Reflection;
using UmeVrcfQol.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmeVrcfQol
{
    internal static partial class VrcfQolInspectorOverlay
    {
        private static int InjectActionButtons(VisualElement root)
        {
            int injected = 0;
            foreach (PropertyField field in root.Query<PropertyField>().ToList())
            {
                if (field != null && TryInjectActionTools(field))
                {
                    injected++;
                }
            }

            return injected;
        }

        private static bool TryInjectActionTools(PropertyField field)
        {
            string bindingPath = field.bindingPath ?? "";
            if (!ActionPathRegex.IsMatch(bindingPath))
            {
                if (field.ClassListContains(ActionToolsInjectedClass))
                {
                    field.Q<VisualElement>(className: ActionToolsClass)?.RemoveFromHierarchy();
                    field.RemoveFromClassList(ActionToolsInjectedClass);
                }

                return false;
            }

            VisualElement? existing = field.Q<VisualElement>(className: ActionToolsClass);
            string toolsKey = bindingPath + "|action-tools-v2";
            if (field.ClassListContains(ActionToolsInjectedClass))
            {
                if (existing != null && string.Equals(existing.userData as string, toolsKey, StringComparison.Ordinal))
                {
                    return false;
                }

                existing?.RemoveFromHierarchy();
            }

            field.AddToClassList(ActionToolsInjectedClass);
            field.Insert(0, BuildActionTools(field, bindingPath, toolsKey));
            return true;
        }

        private static VisualElement BuildActionTools(PropertyField field, string bindingPath, string toolsKey)
        {
            var tools = new VisualElement();
            tools.AddToClassList(ActionToolsClass);
            tools.userData = toolsKey;
            tools.style.flexDirection = FlexDirection.Row;
            tools.style.alignItems = Align.Center;
            tools.style.justifyContent = Justify.FlexEnd;
            tools.style.marginTop = 2;
            tools.style.marginBottom = 2;
            tools.style.paddingRight = 2;

            var btn = new Button(() => InvokeWithResolvedOwner(field, bindingPath, "Duplicate item", OnDuplicateActionClicked))
            {
                text = "Duplicate item",
                tooltip =
                    "Clone only this one VRCFury action, such as a single BlendShape or Material Swap, and insert the copy directly below it.",
            };
            EditorElementWalker.ApplyInlineButtonStyle(btn);
            tools.Add(btn);

            if (FlipbookActionPathRegex.IsMatch(bindingPath))
            {
                var copyBtn = new Button(() => InvokeWithResolvedOwner(field, bindingPath, "Copy to page", OnDuplicateActionToPageClicked))
                {
                    text = "Copy to page",
                    tooltip =
                        "Clone only this one action and append the copy to another page in this flipbook.",
                };
                EditorElementWalker.ApplyInlineButtonStyle(copyBtn);
                tools.Add(copyBtn);
            }

            return tools;
        }

        private static void OnDuplicateActionClicked(int owningComponentInstanceId, string actionPropertyPath)
        {
            if (!TryFindSelectedActionComponent(owningComponentInstanceId, actionPropertyPath, out Component? component, out string? error))
            {
                EditorUtility.DisplayDialog("Duplicate VRCFury Action", error, "OK");
                return;
            }

            if (!DuplicateActionTool.TryDuplicate(component, actionPropertyPath, out error))
            {
                EditorUtility.DisplayDialog("Duplicate VRCFury Action", error, "OK");
            }
        }

        private static void OnDuplicateActionToPageClicked(int owningComponentInstanceId, string actionPropertyPath)
        {
            if (!TryFindSelectedActionComponent(owningComponentInstanceId, actionPropertyPath, out Component? component, out string? error))
            {
                EditorUtility.DisplayDialog("Copy VRCFury Action To Page", error, "OK");
                return;
            }
            if (!DuplicateActionTool.TryGetFlipbookActionCopyInfo(component, actionPropertyPath,
                    out DuplicateActionTool.FlipbookActionCopyInfo info, out error))
            {
                EditorUtility.DisplayDialog("Copy VRCFury Action To Page", error, "OK");
                return;
            }
            if (info.PageCount <= 1)
            {
                EditorUtility.DisplayDialog("Copy VRCFury Action To Page",
                    "This flipbook only has one page. Use Duplicate item to copy the action on the same page.",
                    "OK");
                return;
            }

            var menu = new GenericMenu();
            for (int i = 0; i < info.PageCount; i++)
            {
                string label = i == info.SourcePageIndex
                    ? $"Page {i + 1} - current page"
                    : $"Page {i + 1}";
                if (i == info.SourcePageIndex)
                {
                    menu.AddDisabledItem(new GUIContent(label));
                    continue;
                }

                int targetPageIndex = i;
                menu.AddItem(new GUIContent(label), on: false, () =>
                {
                    if (!DuplicateActionTool.TryDuplicateToFlipbookPage(
                            component, actionPropertyPath, targetPageIndex, out string? copyError))
                    {
                        EditorUtility.DisplayDialog("Copy VRCFury Action To Page", copyError, "OK");
                    }
                });
            }
            menu.ShowAsContext();
        }

        private static Component? FindOwningComponent(VisualElement element)
        {
            VisualElement current = element;
            while (current != null)
            {
                if (EditorElementWalker.TryGetEditorTarget(current, out Component? comp))
                {
                    return comp;
                }

                current = current.parent;
            }
            return null;
        }

        private static void InvokeWithResolvedOwner(
            VisualElement field,
            string actionPath,
            string action,
            Action<int, string> next)
        {
            Component? owner = FindOwningComponent(field);
            int ownerId = owner != null ? owner.GetInstanceID() : 0;
            if (owner != null)
            {
                VrcfQolLogger.Instance.Info(action + " click: path=" + actionPath +
                    " resolved to " + owner.gameObject.name + "/" + owner.GetType().Name +
                    " (instanceId=" + ownerId + ").");
            }
            else
            {
                VrcfQolLogger.Instance.Warning(action + " click: path=" + actionPath +
                    " could NOT be tied to a specific VRCFury component via the editor wrapper walk. " +
                    "Falling back to first-match scan on the selected GameObject. " +
                    "If the wrong component gets operated on, the inspector's internal layout has changed.");
            }
            next(ownerId, actionPath);
        }

        private static bool HasActionProperty(Component component, string actionPropertyPath)
        {
            using var so = new SerializedObject(component);
            SerializedProperty prop = so.FindProperty(actionPropertyPath);
            return prop != null && prop.propertyType == SerializedPropertyType.ManagedReference;
        }

        private static Component? FindFirstActionOwner(GameObject selection, Type vrcfuryType, string actionPropertyPath)
        {
            foreach (Component c in selection.GetComponents(vrcfuryType))
            {
                if (c != null && HasActionProperty(c, actionPropertyPath))
                {
                    return c;
                }
            }

            return null;
        }

        private static bool TryFindSelectedActionComponent(
            int owningComponentInstanceId,
            string actionPropertyPath,
            [NotNullWhen(true)] out Component? component,
            out string? error)
        {
            component = null;
            error = null;

            if (owningComponentInstanceId != 0)
            {
                var resolved = EditorUtility.InstanceIDToObject(owningComponentInstanceId) as Component;
                if (resolved != null && HasActionProperty(resolved, actionPropertyPath))
                {
                    component = resolved;
                    return true;
                }
            }

            GameObject selection = Selection.activeGameObject;
            if (selection == null)
            {
                error = "Select the GameObject that owns this VRCFury component, then try again.";
                return false;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out error))
            {
                return false;
            }

            Component? firstMatch = FindFirstActionOwner(selection, r.VRCFuryType, actionPropertyPath);
            if (firstMatch == null)
            {
                error = "Could not match this inspector row to a VRCFury action on the selected GameObject. " +
                    "If the inspector is locked or showing a different object, select the object again and retry.";
                return false;
            }

            if (owningComponentInstanceId != 0)
            {
                VrcfQolLogger.Instance.Warning("Inline action resolver fell back to first-match scan on '" +
                    selection.name + "' -- EditorElement lookup missed for instance id " +
                    owningComponentInstanceId + ". If the wrong VRCFury component was operated on, " +
                    "the Unity internal type layout may have changed; report this so the resolver can be updated.");
            }

            component = firstMatch;
            return true;
        }
    }
}
