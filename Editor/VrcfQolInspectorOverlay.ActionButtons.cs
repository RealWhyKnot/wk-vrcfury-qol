// VrcfQolInspectorOverlay.ActionButtons.cs

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UmeVrcfQol.Tools;
using UmeVrcfQol.Internal.Reflection;

namespace UmeVrcfQol {

    internal static partial class VrcfQolInspectorOverlay {

        // ----------------------------------------------------------------------
        // Per-action inline duplicate button
        // ----------------------------------------------------------------------

        private static int InjectActionButtons(VisualElement root) {
            int injected = 0;
            var fields = root.Query<PropertyField>().ToList();
            foreach (var field in fields) {
                if (field == null) continue;
                var bindingPath = field.bindingPath ?? "";
                if (!ActionPathRegex.IsMatch(bindingPath)) {
                    if (field.ClassListContains(ActionToolsInjectedClass)) {
                        var stale = field.Q<VisualElement>(className: ActionToolsClass);
                        if (stale != null) stale.RemoveFromHierarchy();
                        field.RemoveFromClassList(ActionToolsInjectedClass);
                    }
                    continue;
                }

                var existing = field.Q<VisualElement>(className: ActionToolsClass);
                var toolsKey = bindingPath + "|action-tools-v2";
                if (field.ClassListContains(ActionToolsInjectedClass)) {
                    if (existing != null && (existing.userData as string) == toolsKey) continue;
                    if (existing != null) existing.RemoveFromHierarchy();
                }
                field.AddToClassList(ActionToolsInjectedClass);

                var tools = new VisualElement();
                tools.AddToClassList(ActionToolsClass);
                tools.userData = toolsKey;
                tools.style.flexDirection = FlexDirection.Row;
                tools.style.alignItems = Align.Center;
                tools.style.justifyContent = Justify.FlexEnd;
                tools.style.marginTop = 2;
                tools.style.marginBottom = 2;
                tools.style.paddingRight = 2;

                var capturedPath = bindingPath;
                var capturedField = field;
                var btn = new Button(() => InvokeWithResolvedOwner(capturedField, capturedPath, "Duplicate item", OnDuplicateActionClicked)) {
                    text = "Duplicate item"
                };
                btn.tooltip =
                    "Clone only this one VRCFury action, such as a single BlendShape or Material Swap, and insert the copy directly below it.";
                EditorElementWalker.ApplyInlineButtonStyle(btn);
                tools.Add(btn);

                if (FlipbookActionPathRegex.IsMatch(bindingPath)) {
                    var copyBtn = new Button(() => InvokeWithResolvedOwner(capturedField, capturedPath, "Copy to page", OnDuplicateActionToPageClicked)) {
                        text = "Copy to page"
                    };
                    copyBtn.tooltip =
                        "Clone only this one action and append the copy to another page in this flipbook.";
                    EditorElementWalker.ApplyInlineButtonStyle(copyBtn);
                    tools.Add(copyBtn);
                }

                field.Insert(0, tools);
                injected++;
            }
            return injected;
        }

        private static void OnDuplicateActionClicked(int owningComponentInstanceId, string actionPropertyPath) {
            if (!TryFindSelectedActionComponent(owningComponentInstanceId, actionPropertyPath, out var component, out var error)) {
                EditorUtility.DisplayDialog("Duplicate VRCFury Action", error, "OK");
                return;
            }

            if (!DuplicateActionTool.TryDuplicate(component, actionPropertyPath, out error)) {
                EditorUtility.DisplayDialog("Duplicate VRCFury Action", error, "OK");
            }
        }

        private static void OnDuplicateActionToPageClicked(int owningComponentInstanceId, string actionPropertyPath) {
            if (!TryFindSelectedActionComponent(owningComponentInstanceId, actionPropertyPath, out var component, out var error)) {
                EditorUtility.DisplayDialog("Copy VRCFury Action To Page", error, "OK");
                return;
            }
            if (!DuplicateActionTool.TryGetFlipbookActionCopyInfo(component, actionPropertyPath,
                    out var info, out error)) {
                EditorUtility.DisplayDialog("Copy VRCFury Action To Page", error, "OK");
                return;
            }
            if (info.PageCount <= 1) {
                EditorUtility.DisplayDialog("Copy VRCFury Action To Page",
                    "This flipbook only has one page. Use Duplicate item to copy the action on the same page.",
                    "OK");
                return;
            }

            var menu = new GenericMenu();
            for (int i = 0; i < info.PageCount; i++) {
                var label = i == info.SourcePageIndex
                    ? $"Page {i + 1} - current page"
                    : $"Page {i + 1}";
                if (i == info.SourcePageIndex) {
                    menu.AddDisabledItem(new GUIContent(label));
                    continue;
                }

                var targetPageIndex = i;
                menu.AddItem(new GUIContent(label), false, () => {
                    if (!DuplicateActionTool.TryDuplicateToFlipbookPage(
                            component, actionPropertyPath, targetPageIndex, out var copyError)) {
                        EditorUtility.DisplayDialog("Copy VRCFury Action To Page", copyError, "OK");
                    }
                });
            }
            menu.ShowAsContext();
        }

        // ----------------------------------------------------------------------
        // Editor-target resolution helpers
        //
        // Unity's inspector hosts each component's Editor inside an
        // EditorElement (and, for UIElements editors, a nested
        // InspectorElement). Both are internal types whose field/property names
        // are not part of the public contract. Rather than hardcode m_Editor /
        // boundEditor / etc. (which has caused real bugs when Unity renames
        // them between versions) we look for ANY Editor-typed member on the
        // wrapper, walking the class hierarchy.
        //
        // FindOwningComponent walks UP from a leaf VisualElement (e.g. a
        // PropertyField on an action row) to find the component its editor is
        // editing. EnumerateEditorWrappers walks DOWN from the inspector root
        // to yield every component-editor wrapper, used by the per-Toggle
        // banner placement.
        // ----------------------------------------------------------------------

        private static Component FindOwningComponent(VisualElement element) {
            var current = element;
            while (current != null) {
                if (EditorElementWalker.TryGetEditorTarget(current, out var comp)) return comp;
                current = current.parent;
            }
            return null;
        }


        // Click-time resolver: defers the EditorElement walk until click time,
        // when the visual tree is guaranteed to be fully
        // built and the editor's target field is populated. Captures and
        // surfaces the resolution via a Debug.Log so any "wrong row" report
        // immediately points to whether the resolver succeeded or fell back.
        private static void InvokeWithResolvedOwner(
            VisualElement field,
            string actionPath,
            string action,
            Action<int, string> next) {
            var owner = FindOwningComponent(field);
            var ownerId = owner != null ? owner.GetInstanceID() : 0;
            if (owner != null) {
                VrcfQolLogger.Instance.Info(action + " click: path=" + actionPath +
                    " resolved to " + owner.gameObject.name + "/" + owner.GetType().Name +
                    " (instanceId=" + ownerId + ").");
            } else {
                VrcfQolLogger.Instance.Warning(action + " click: path=" + actionPath +
                    " could NOT be tied to a specific VRCFury component via the editor wrapper walk. " +
                    "Falling back to first-match scan on the selected GameObject. " +
                    "If the wrong component gets operated on, the inspector's internal layout has changed.");
            }
            next(ownerId, actionPath);
        }

        // When owningComponentInstanceId is nonzero, looks up that exact
        // Component directly -- this is the path that prevents picking the
        // wrong VRCFury component when two are on the same GameObject and
        // both expose a valid SerializedProperty at the same actionPropertyPath
        // (each component's SerializedObject roots its own path namespace).
        // When zero, falls back to the legacy "first component on the selection
        // that has a valid property at this path" scan -- keeps any call sites
        // that don't (yet) have a visual context for disambiguation working.
        private static bool TryFindSelectedActionComponent(
            int owningComponentInstanceId,
            string actionPropertyPath,
            out Component component,
            out string error) {
            component = null;
            error = null;

            if (owningComponentInstanceId != 0) {
                var resolved = EditorUtility.InstanceIDToObject(owningComponentInstanceId) as Component;
                if (resolved != null) {
                    using (var so = new SerializedObject(resolved)) {
                        var prop = so.FindProperty(actionPropertyPath);
                        if (prop != null && prop.propertyType == SerializedPropertyType.ManagedReference) {
                            component = resolved;
                            return true;
                        }
                    }
                }
            }

            var selection = Selection.activeGameObject;
            if (selection == null) {
                error = "Select the GameObject that owns this VRCFury component, then try again.";
                return false;
            }
            if (!VrcfQol.Reflection.TryEnsure(out var err)) {
                error = err;
                return false;
            }

            Component firstMatch = null;
            foreach (Component c in selection.GetComponents(VrcfQol.Reflection.VRCFuryType)) {
                if (c == null) continue;
                using (var so = new SerializedObject(c)) {
                    var prop = so.FindProperty(actionPropertyPath);
                    if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference) continue;
                }
                firstMatch = c;
                break;
            }

            if (firstMatch != null) {
                if (owningComponentInstanceId != 0) {
                    VrcfQolLogger.Instance.Warning("Inline action resolver fell back to first-match scan on '" +
                        selection.name + "' -- EditorElement lookup missed for instance id " +
                        owningComponentInstanceId + ". If the wrong VRCFury component was operated on, " +
                        "the Unity internal type layout may have changed; report this so the resolver can be updated.");
                }
                component = firstMatch;
                return true;
            }

            error = "Could not match this inspector row to a VRCFury action on the selected GameObject. " +
                "If the inspector is locked or showing a different object, select the object again and retry.";
            return false;
        }
    }
}
