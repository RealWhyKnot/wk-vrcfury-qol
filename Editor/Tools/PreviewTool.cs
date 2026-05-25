// PreviewTool.cs
//
// Non-destructive scene preview for VRCFury toggles and flipbook pages.
// Preview never mutates the original avatar. It creates a temporary clone,
// applies the selected VRCFury state actions to that clone, hides the source
// avatar while previewing, then restores it when previewing stops.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools {

    [InitializeOnLoad]
    internal static partial class PreviewTool {
        private const BindingFlags AnyInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const string PreviewPrefix = "[VRCF QoL Preview]";
        private const string SessionPreviewId = "WhyKnot.VrcfQol.Preview.CloneId";
        private const string SessionSourceId = "WhyKnot.VrcfQol.Preview.SourceId";
        private const string SessionSourceWasHidden = "WhyKnot.VrcfQol.Preview.SourceWasHidden";
        private const string PrefsSourceGlobalId = "WhyKnot.VrcfQol.Preview.SourceGlobalId";
        private const string PrefsSourceWasHidden = "WhyKnot.VrcfQol.Preview.SourceWasHidden";

        private static PreviewSession _active;

        static PreviewTool() {
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

        internal static void ShowTogglePreview(Component component) {
            if (!TryResolveToggleFromComponent(component, out var ctx, out var error)) {
                EditorUtility.DisplayDialog("Preview", error, "OK");
                return;
            }
            ShowTogglePreview(ctx);
        }

        internal static void ToggleTogglePreview(Component component) {
            if (IsPreviewingComponent(component)) {
                StopActivePreview();
                return;
            }
            ShowTogglePreview(component);
        }

        internal static void StopActivePreview() {
            StopPreview("stopped");
        }

        [MenuItem("Tools/WhyKnot/wk-vrcfury-qol/Stop previewing", priority = 36)]
        private static void StopPreviewFromMenu() {
            StopActivePreview();
        }

        [MenuItem("Tools/WhyKnot/wk-vrcfury-qol/Stop previewing", true)]
        private static bool StopPreviewFromMenuValidate() {
            return _active != null && _active.CloneRoot != null;
        }

        internal static bool IsPreviewingComponent(Component component) {
            return _active != null &&
                _active.CloneRoot != null &&
                component != null &&
                _active.SourceComponent == component;
        }

        internal static bool IsPreviewingPage(VrcfQol.FlipbookContext ctx) {
            return IsPreviewing(ctx.vrcfComponent, ctx.pageIndex);
        }

        private static void ShowTogglePreview(VrcfQol.ToggleContext ctx) {
            if (ctx.flipbookAction != null) {
                var pages = VrcfQol.Reflection.PagesField.GetValue(ctx.flipbookAction) as IList;
                if (pages == null || pages.Count == 0) {
                    EditorUtility.DisplayDialog("Preview", "This flipbook has no pages to preview.", "OK");
                    return;
                }
                ShowPageMenu(ctx.vrcfComponent, ctx.toggleName, pages);
                return;
            }

            StartPreview(ctx.vrcfComponent, ctx.actions, PreviewName("Toggle", ctx.toggleName), "toggle", -1);
        }

        private static void ShowFlipbookPagePicker(VrcfQol.FlipbookContext ctx) {
            if (ctx.pages == null || ctx.pages.Count == 0) {
                EditorUtility.DisplayDialog("Preview", "This flipbook has no pages to preview.", "OK");
                return;
            }
            ShowPageMenu(ctx.vrcfComponent, ctx.toggleName, ctx.pages);
        }

        private static void ShowPagePreview(VrcfQol.FlipbookContext ctx) {
            if (ctx.pages == null || ctx.pageIndex < 0 || ctx.pageIndex >= ctx.pages.Count) {
                EditorUtility.DisplayDialog("Preview", $"Page #{ctx.pageIndex + 1} was not found.", "OK");
                return;
            }
            StartPreview(
                ctx.vrcfComponent,
                GetActionsFromPage(ctx.pages[ctx.pageIndex]),
                $"{PreviewName("Flipbook", ctx.toggleName)} - Page #{ctx.pageIndex + 1}",
                $"page #{ctx.pageIndex + 1}",
                ctx.pageIndex);
        }

        private static void TogglePagePreview(VrcfQol.FlipbookContext ctx) {
            if (IsPreviewingPage(ctx)) {
                StopActivePreview();
                return;
            }
            ShowPagePreview(ctx);
        }

        private static void ShowPageMenu(Component component, string toggleName, IList pages) {
            var menu = new GenericMenu();
            for (int i = 0; i < pages.Count; i++) {
                int pageIndex = i;
                menu.AddItem(new GUIContent($"Page {i + 1}"), false, () => {
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

        private static void StartPreview(Component sourceComponent, IList actions, string title, string shortLabel, int pageIndex) {
            if (sourceComponent == null) {
                EditorUtility.DisplayDialog("Preview", "Could not resolve the VRCFury component.", "OK");
                return;
            }
            if (actions == null || actions.Count == 0) {
                EditorUtility.DisplayDialog("Preview", "There are no actions on this toggle/page to preview.", "OK");
                return;
            }

            StopPreview("replaced");

            var sourceRoot = FindAvatarRoot(sourceComponent);
            if (sourceRoot == null) {
                EditorUtility.DisplayDialog("Preview", "Could not find the avatar root to duplicate.", "OK");
                return;
            }

            GameObject clone = null;
            try {
                clone = Object.Instantiate(sourceRoot, sourceRoot.transform.parent);
                clone.name = $"{PreviewPrefix} {sourceRoot.name}";
                clone.transform.SetSiblingIndex(sourceRoot.transform.GetSiblingIndex() + 1);
                MarkHierarchyTemporary(clone);
                AlignCloneWithSource(sourceRoot, clone);
                StripPreviewComponents(clone);

                var session = new PreviewSession {
                    SourceComponent = sourceComponent,
                    SourceRoot = sourceRoot,
                    CloneRoot = clone,
                    Title = title,
                    ShortLabel = shortLabel,
                    PageIndex = pageIndex,
                    SourceWasHidden = IsSceneHidden(sourceRoot),
                    PreviousSelection = Selection.objects,
                };
                _active = session;
                RememberPreview(session);

                var applied = 0;
                foreach (var action in actions) {
                    if (action == null) continue;
                    if (ApplyActionToClone(action, session)) applied++;
                }

                if (applied == 0) {
                    StopPreview("empty");
                    EditorUtility.DisplayDialog("Preview",
                        "None of the actions on this toggle/page could be applied to a temporary preview copy.",
                        "OK");
                    return;
                }

                HideSourceAvatar(sourceRoot);
                VrcfQolLogger.Instance.Info($"Started preview '{title}' on temporary clone '{clone.name}' ({applied} action(s) applied).");
            } catch (Exception ex) {
                VrcfQolLogger.Instance.Exception(ex);
                if (clone != null) Object.DestroyImmediate(clone);
                _active = null;
                ForgetPreview();
                EditorUtility.DisplayDialog("Preview", "Preview failed. See Console.\n\n" + ex.Message, "OK");
            }
        }
    }
}
