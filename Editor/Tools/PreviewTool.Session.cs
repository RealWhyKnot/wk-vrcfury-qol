// PreviewTool.Session.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools {

    internal static partial class PreviewTool {

        private static void Tick() {
            if (_active == null) return;
            if (_active.CloneRoot == null) {
                RestoreSourceVisibility(_active.SourceRoot, _active.SourceWasHidden);
                _active = null;
                ForgetPreview();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode) {
                StopPreview("stopped before play mode");
            }
        }

        private static void StopPreview(string reason) {
            if (_active == null) return;
            var clone = _active.CloneRoot;
            var source = _active.SourceRoot;
            var sourceWasHidden = _active.SourceWasHidden;
            var title = _active.Title;
            var previousSelection = _active.PreviousSelection;
            var restoreSelection = Selection.activeGameObject == null ||
                (clone != null && IsPreviewObject(Selection.activeGameObject, clone));
            _active = null;
            if (clone != null) Object.DestroyImmediate(clone);
            RestoreSourceVisibility(source, sourceWasHidden);
            if (restoreSelection) Selection.objects = previousSelection ?? Array.Empty<Object>();
            ForgetPreview();
            SceneView.RepaintAll();
            VrcfQolLogger.Instance.Info($"Preview '{title}' {reason}; temporary clone destroyed.");
        }

        private static IList GetActionsFromPage(object page) {
            if (page == null || !VrcfQol.Reflection.TryEnsure(out _)) return null;
            var state = VrcfQol.Reflection.PageStateField.GetValue(page);
            return state == null ? null : VrcfQol.Reflection.StateActionsField.GetValue(state) as IList;
        }

        private static bool TryResolveToggleFromComponent(
            Component component,
            out VrcfQol.ToggleContext ctx,
            out string error) {
            ctx = default;
            error = null;
            if (!VrcfQol.Reflection.TryEnsure(out error)) return false;
            if (component == null || component.GetType() != VrcfQol.Reflection.VRCFuryType) {
                error = "Could not resolve the selected VRCFury component.";
                return false;
            }

            var r = VrcfQol.Reflection;
            var content = r.ContentField.GetValue(component);
            if (content == null || content.GetType() != r.ToggleType) {
                error = "This VRCFury component is not a Toggle.";
                return false;
            }

            var state = r.ToggleStateField.GetValue(content);
            var actions = r.StateActionsField.GetValue(state) as IList;
            var flipbook = VrcfQol.FindFlipbookAction(actions);
            var slider = false;
            try {
                if (r.ToggleSliderField != null) slider = (bool)r.ToggleSliderField.GetValue(content);
            } catch {
                slider = false;
            }

            ctx = new VrcfQol.ToggleContext {
                vrcfComponent = component,
                toggle = content,
                toggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                state = state,
                actions = actions,
                flipbookAction = flipbook,
                slider = slider,
            };
            return true;
        }
    }
}
