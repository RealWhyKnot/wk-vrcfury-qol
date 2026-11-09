#nullable enable

using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    internal static partial class PreviewTool
    {
        private static void Tick()
        {
            if (ActiveSession == null)
            {
                return;
            }

            if (ActiveSession.CloneRoot == null)
            {
                RestoreSourceVisibility(ActiveSession.SourceRoot, ActiveSession.SourceWasHidden);
                ActiveSession = null;
                ForgetPreview();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingEditMode or PlayModeStateChange.EnteredPlayMode)
            {
                StopPreview("stopped before play mode");
            }
        }

        private static void StopPreview(string reason)
        {
            if (ActiveSession == null)
            {
                return;
            }

            GameObject? clone = ActiveSession.CloneRoot;
            GameObject? source = ActiveSession.SourceRoot;
            bool sourceWasHidden = ActiveSession.SourceWasHidden;
            string title = ActiveSession.Title;
            Object[] previousSelection = ActiveSession.PreviousSelection;
            bool restoreSelection = Selection.activeGameObject == null ||
                (clone != null && IsPreviewObject(Selection.activeGameObject, clone));
            ActiveSession = null;
            if (clone != null)
            {
                Object.DestroyImmediate(clone);
            }

            RestoreSourceVisibility(source, sourceWasHidden);
            if (restoreSelection)
            {
                Selection.objects = previousSelection;
            }

            ForgetPreview();
            SceneView.RepaintAll();
            VrcfQolLogger.Instance.Info($"Preview '{title}' {reason}; temporary clone destroyed.");
        }

        private static IList? GetActionsFromPage(object page)
        {
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (page == null || !r.TryEnsure(out _))
            {
                return null;
            }

            object state = r.PageStateField.GetValue(page);
            return state == null ? null : r.StateActionsField.GetValue(state) as IList;
        }

        private static bool TryResolveToggleFromComponent(
            Component component,
            out VrcfQol.ToggleContext ctx,
            out string? error)
        {
            ctx = default;
            error = null;
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out error))
            {
                return false;
            }

            if (component == null || component.GetType() != r.VRCFuryType)
            {
                error = "Could not resolve the selected VRCFury component.";
                return false;
            }

            object content = r.ContentField.GetValue(component);
            if (content == null || content.GetType() != r.ToggleType)
            {
                error = "This VRCFury component is not a Toggle.";
                return false;
            }

            object state = r.ToggleStateField.GetValue(content);
            var actions = r.StateActionsField.GetValue(state) as IList;
            object? flipbook = VrcfQol.FindFlipbookAction(actions);
            bool slider = VrcfQol.ReadToggleSlider(r.ToggleSliderField, content);

            ctx = new VrcfQol.ToggleContext
            {
                VrcfComponent = component,
                Toggle = content,
                ToggleName = (string)r.ToggleNameField.GetValue(content) ?? "",
                State = state,
                Actions = actions,
                FlipbookAction = flipbook,
                Slider = slider,
            };
            return true;
        }
    }
}
