#nullable enable

using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    [InitializeOnLoad]
    internal static partial class PreviewTool
    {
        private const BindingFlags AnyInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const string PreviewPrefix = "[VRCF QoL Preview]";
        private const string SessionPreviewId = "WhyKnot.VrcfQol.Preview.CloneId";
        private const string SessionSourceId = "WhyKnot.VrcfQol.Preview.SourceId";
        private const string SessionSourceWasHidden = "WhyKnot.VrcfQol.Preview.SourceWasHidden";
        private const string PrefsSourceGlobalId = "WhyKnot.VrcfQol.Preview.SourceGlobalId";
        private const string PrefsSourceWasHidden = "WhyKnot.VrcfQol.Preview.SourceWasHidden";

        private static PreviewSession? ActiveSession;

        static PreviewTool()
        {
            RestoreAbandonedPreviewState();
            CleanupAbandonedPreviewClones();
            EditorApplication.delayCall += RestoreAbandonedPreviewState;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened += (_, __) => RestoreAbandonedPreviewState();
            AssemblyReloadEvents.beforeAssemblyReload += StopActivePreview;
            EditorApplication.quitting += StopActivePreview;

            VrcfQol.RegisterFlipbookPageButton(
                text: "Preview",
                tooltip: "Create a temporary avatar copy with this flipbook page applied. This does not move the Scene camera.",
                onClick: TogglePagePreview,
                order: -10,
                textProvider: ctx => IsPreviewingPage(ctx) ? "Stop Previewing" : "Preview",
                tooltipProvider: ctx => IsPreviewingPage(ctx)
                    ? "Destroy the temporary preview copy and return to the original avatar."
                    : "Create a temporary avatar copy with this flipbook page applied. This does not move the Scene camera.",
                danger: IsPreviewingPage
            );
            VrcfQol.RegisterFlipbookPageTool(
                label: "WhyKnot/wk-vrcfury-qol/Preview page",
                action: ShowPagePreview,
                priority: 35
            );
            VrcfQol.RegisterFlipbookBuilderTool(
                label: "WhyKnot/wk-vrcfury-qol/Preview flipbook page...",
                action: ShowFlipbookPagePicker,
                priority: 35
            );
            VrcfQol.RegisterToggleTool(
                label: "WhyKnot/wk-vrcfury-qol/Preview toggle",
                action: ShowTogglePreview,
                priority: 35
            );
        }

        internal static void ShowTogglePreview(Component component)
        {
            if (!TryResolveToggleFromComponent(component, out VrcfQol.ToggleContext ctx, out string? error))
            {
                EditorUtility.DisplayDialog("Preview", error, "OK");
                return;
            }
            ShowTogglePreview(ctx);
        }

        internal static void ToggleTogglePreview(Component component)
        {
            if (IsPreviewingComponent(component))
            {
                StopActivePreview();
                return;
            }
            ShowTogglePreview(component);
        }

        internal static void StopActivePreview()
        {
            StopPreview("stopped");
        }

        [MenuItem("Tools/WhyKnot/wk-vrcfury-qol/Stop previewing", priority = 36)]
        private static void StopPreviewFromMenu()
        {
            StopActivePreview();
        }

        [MenuItem("Tools/WhyKnot/wk-vrcfury-qol/Stop previewing", true)]
        private static bool StopPreviewFromMenuValidate()
        {
            return ActiveSession != null && ActiveSession.CloneRoot != null;
        }

        internal static bool IsPreviewingComponent(Component component)
        {
            return ActiveSession != null &&
                ActiveSession.CloneRoot != null &&
                component != null &&
                ActiveSession.SourceComponent == component;
        }

        internal static bool IsPreviewingPage(VrcfQol.FlipbookContext ctx)
        {
            return IsPreviewing(ctx.VrcfComponent, ctx.PageIndex);
        }

        private static void ShowTogglePreview(VrcfQol.ToggleContext ctx)
        {
            if (ctx.FlipbookAction != null)
            {
                VrcfQol.ReflectionCache r = VrcfQol.Reflection;
                if (!r.TryEnsure(out _) || r.PagesField.GetValue(ctx.FlipbookAction) is not IList pages || pages.Count == 0)
                {
                    EditorUtility.DisplayDialog("Preview", "This flipbook has no pages to preview.", "OK");
                    return;
                }
                ShowPageMenu(ctx.VrcfComponent, ctx.ToggleName, pages);
                return;
            }

            StartPreview(ctx.VrcfComponent, ctx.Actions, PreviewName("Toggle", ctx.ToggleName), "toggle", -1);
        }

        private static void ShowFlipbookPagePicker(VrcfQol.FlipbookContext ctx)
        {
            if (ctx.Pages == null || ctx.Pages.Count == 0)
            {
                EditorUtility.DisplayDialog("Preview", "This flipbook has no pages to preview.", "OK");
                return;
            }
            ShowPageMenu(ctx.VrcfComponent, ctx.ToggleName, ctx.Pages);
        }

        private static void ShowPagePreview(VrcfQol.FlipbookContext ctx)
        {
            if (ctx.Pages == null || ctx.PageIndex < 0 || ctx.PageIndex >= ctx.Pages.Count)
            {
                EditorUtility.DisplayDialog("Preview", $"Page #{ctx.PageIndex + 1} was not found.", "OK");
                return;
            }
            StartPreview(
                ctx.VrcfComponent,
                GetActionsFromPage(ctx.Pages[ctx.PageIndex]),
                $"{PreviewName("Flipbook", ctx.ToggleName)} - Page #{ctx.PageIndex + 1}",
                $"page #{ctx.PageIndex + 1}",
                ctx.PageIndex);
        }

        private static void TogglePagePreview(VrcfQol.FlipbookContext ctx)
        {
            if (IsPreviewingPage(ctx))
            {
                StopActivePreview();
                return;
            }
            ShowPagePreview(ctx);
        }

        private static void ShowPageMenu(Component component, string toggleName, IList pages)
        {
            var menu = new GenericMenu();
            for (int i = 0; i < pages.Count; i++)
            {
                int pageIndex = i;
                menu.AddItem(new GUIContent($"Page {i + 1}"), on: false, () =>
                {
                    StartPreview(
                        component,
                        GetActionsFromPage(pages[pageIndex]),
                        $"{PreviewName("Flipbook", toggleName)} - Page #{pageIndex + 1}",
                        $"page #{pageIndex + 1}",
                        pageIndex);
                });
            }
            menu.ShowAsContext();
        }

        private static void StartPreview(Component sourceComponent, IList? actions, string title, string shortLabel, int pageIndex)
        {
            if (sourceComponent == null)
            {
                EditorUtility.DisplayDialog("Preview", "Could not resolve the VRCFury component.", "OK");
                return;
            }
            if (actions == null || actions.Count == 0)
            {
                EditorUtility.DisplayDialog("Preview", "There are no actions on this toggle/page to preview.", "OK");
                return;
            }

            StopPreview("replaced");

            GameObject? sourceRoot = FindAvatarRoot(sourceComponent);
            if (sourceRoot == null)
            {
                EditorUtility.DisplayDialog("Preview", "Could not find the avatar root to duplicate.", "OK");
                return;
            }

            GameObject? clone = null;
            bool settled = false;
            try
            {
                clone = CreatePreviewClone(sourceRoot);
                PreviewSession session = BeginSession(sourceComponent, sourceRoot, clone, title, shortLabel, pageIndex);
                int applied = ApplyActionsToClone(actions, session);
                settled = true;
                if (applied == 0)
                {
                    ReportEmptyPreview();
                    return;
                }

                HideSourceAvatar(sourceRoot);
                VrcfQolLogger.Instance.Info($"Started preview '{title}' on temporary clone '{clone.name}' ({applied} action(s) applied).");
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                VrcfQolLogger.Instance.Exception(ex);
                AbandonPreview(clone);
                settled = true;
                EditorUtility.DisplayDialog("Preview", "Preview failed. See Console.\n\n" + ex.Message, "OK");
            }
            finally
            {
                if (!settled)
                {
                    AbandonPreview(clone);
                }
            }
        }

        private static PreviewSession BeginSession(
            Component sourceComponent,
            GameObject sourceRoot,
            GameObject clone,
            string title,
            string shortLabel,
            int pageIndex)
        {
            var session = new PreviewSession(sourceComponent, sourceRoot, clone, title, shortLabel)
            {
                PageIndex = pageIndex,
                SourceWasHidden = IsSceneHidden(sourceRoot),
                PreviousSelection = Selection.objects,
            };
            ActiveSession = session;
            RememberPreview(session);
            return session;
        }

        private static void ReportEmptyPreview()
        {
            StopPreview("empty");
            EditorUtility.DisplayDialog("Preview",
                "None of the actions on this toggle/page could be applied to a temporary preview copy.",
                "OK");
        }

        private static GameObject CreatePreviewClone(GameObject sourceRoot)
        {
            GameObject clone = Object.Instantiate(sourceRoot, sourceRoot.transform.parent);
            clone.name = $"{PreviewPrefix} {sourceRoot.name}";
            clone.transform.SetSiblingIndex(sourceRoot.transform.GetSiblingIndex() + 1);
            MarkHierarchyTemporary(clone);
            AlignCloneWithSource(sourceRoot, clone);
            StripPreviewComponents(clone);
            return clone;
        }

        private static int ApplyActionsToClone(IList actions, PreviewSession session)
        {
            int applied = 0;
            foreach (object? action in actions)
            {
                if (action != null && ApplyActionToClone(action, session))
                {
                    applied++;
                }
            }

            return applied;
        }

        private static void AbandonPreview(GameObject? clone)
        {
            if (clone != null)
            {
                Object.DestroyImmediate(clone);
            }

            ActiveSession = null;
            ForgetPreview();
        }
    }
}
