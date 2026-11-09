#nullable enable

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;

namespace UmeVrcfQol.Tools
{
    [InitializeOnLoad]
    internal static class DuplicateFlipbookPageTool
    {
        static DuplicateFlipbookPageTool()
        {
            VrcfQol.RegisterFlipbookPageTool(
                label: "WhyKnot/wk-vrcfury-qol/Duplicate page below",
                action: DuplicateBelow,
                priority: 25
            );
            VrcfQol.RegisterFlipbookPageTool(
                label: "WhyKnot/wk-vrcfury-qol/Insert empty page below",
                action: InsertEmptyBelow,
                priority: 24
            );
            VrcfQol.RegisterFlipbookPageTool(
                label: "WhyKnot/wk-vrcfury-qol/Duplicate page to end",
                action: DuplicateToEnd,
                priority: 20
            );

            VrcfQol.RegisterFlipbookPageButton(
                text: "Duplicate",
                tooltip: "Clone this flipbook page and insert the copy directly below it.",
                onClick: DuplicateBelow,
                order: 0
            );
            VrcfQol.RegisterFlipbookPageButton(
                text: "Insert blank",
                tooltip: "Insert a new empty flipbook page directly below this one.",
                onClick: InsertEmptyBelow,
                order: 1
            );
        }

        private static void DuplicateBelow(VrcfQol.FlipbookContext ctx)
        {
            if (!TryGetPages(ctx, "Duplicate Page Below", out IList? pages))
            {
                return;
            }

            try
            {
                Undo.RegisterCompleteObjectUndo(ctx.VrcfComponent,
                    $"Duplicate flipbook page #{ctx.PageIndex + 1} below");
                object clone = VrcfQol.DeepClonePage(pages[ctx.PageIndex]);
                pages.Insert(ctx.PageIndex + 1, clone);
                VrcfQol.MarkModified(ctx.VrcfComponent);
                VrcfQolLogger.Instance.Info($"Duplicated flipbook page #{ctx.PageIndex + 1} " +
                          $"as new page #{ctx.PageIndex + 2}.");
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                Fail("Duplicate Page Below", ex);
            }
        }

        private static void InsertEmptyBelow(VrcfQol.FlipbookContext ctx)
        {
            if (!TryGetPages(ctx, "Insert Empty Page Below", out IList? pages))
            {
                return;
            }

            try
            {
                Undo.RegisterCompleteObjectUndo(ctx.VrcfComponent,
                    $"Insert empty flipbook page after #{ctx.PageIndex + 1}");
                object? blank = CreateEmptyPage();
                if (blank == null)
                {
                    EditorUtility.DisplayDialog("Insert Empty Page Below",
                        "Could not construct a fresh FlipBookPage. The VRCFury API may have changed.", "OK");
                    return;
                }
                pages.Insert(ctx.PageIndex + 1, blank);
                VrcfQol.MarkModified(ctx.VrcfComponent);
                VrcfQolLogger.Instance.Info($"Inserted empty flipbook page at #{ctx.PageIndex + 2}.");
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                Fail("Insert Empty Page Below", ex);
            }
        }

        private static void DuplicateToEnd(VrcfQol.FlipbookContext ctx)
        {
            if (!TryGetPages(ctx, "Duplicate Page", out IList? pages))
            {
                return;
            }

            try
            {
                Undo.RegisterCompleteObjectUndo(ctx.VrcfComponent,
                    $"Duplicate flipbook page #{ctx.PageIndex + 1}");
                object clone = VrcfQol.DeepClonePage(pages[ctx.PageIndex]);
                pages.Add(clone);
                VrcfQol.MarkModified(ctx.VrcfComponent);
                VrcfQolLogger.Instance.Info($"Duplicated flipbook page #{ctx.PageIndex + 1} " +
                          $"as page #{pages.Count}.");
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                Fail("Duplicate Page", ex);
            }
        }

        private static bool TryGetPages(VrcfQol.FlipbookContext ctx, string title, [NotNullWhen(true)] out IList? pages)
        {
            pages = ctx.Pages;
            if (pages == null || ctx.PageIndex < 0 || ctx.PageIndex >= pages.Count)
            {
                pages = null;
                EditorUtility.DisplayDialog(title, $"Page #{ctx.PageIndex + 1} not found.", "OK");
                return false;
            }
            return true;
        }

        private static object? CreateEmptyPage()
        {
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out _))
            {
                return null;
            }

            try
            {
                object newState = Activator.CreateInstance(r.StateType);
                object actionsList = Activator.CreateInstance(r.StateActionsField.FieldType);
                r.StateActionsField.SetValue(newState, actionsList);
                object page = Activator.CreateInstance(r.FlipbookPageType);
                r.PageStateField.SetValue(page, newState);
                return page;
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                VrcfQolLogger.Instance.Exception(ex);
                return null;
            }
        }

        private static void Fail(string title, Exception ex)
        {
            VrcfQolLogger.Instance.Exception(ex);
            EditorUtility.DisplayDialog(title, "Operation failed. See Console.\n\n" + ex.Message, "OK");
        }
    }
}
