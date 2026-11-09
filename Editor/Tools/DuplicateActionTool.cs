#nullable enable

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    [InitializeOnLoad]
    internal static class DuplicateActionTool
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex ActionTailRegex = new(
            @"\.actions\.Array\.data\[(?<index>\d+)\]$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            RegexTimeout);

        private static readonly Regex FlipbookPageActionRegex = new(
            @"\.pages\.Array\.data\[(?<page>\d+)\]\.state\.actions\.Array\.data\[(?<action>\d+)\]$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            RegexTimeout);

        private static readonly Regex ArrayDataRegex = new(
            @"\.Array\.data\[(?<index>\d+)\]",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            RegexTimeout);

        private static readonly Regex IndexedNameRegex = new(
            @"^(?<name>.+?)\[(?<index>\d+)\]$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            RegexTimeout);

        private const BindingFlags AnyInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [StructLayout(LayoutKind.Auto)]
        internal struct FlipbookActionCopyInfo
        {
            public int SourcePageIndex;
            public int ActionIndex;
            public int PageCount;
        }

        static DuplicateActionTool()
        {
            VrcfQol.RegisterPropertyTool(
                label: "WhyKnot/wk-vrcfury-qol/Duplicate this action",
                match: prop => prop != null
                    && prop.propertyType == SerializedPropertyType.ManagedReference
                    && !string.IsNullOrEmpty(prop.propertyPath)
                    && ActionTailRegex.IsMatch(prop.propertyPath),
                action: Run,
                priority: 15
            );
        }

        private static void Run(SerializedProperty prop)
        {
            var component = prop.serializedObject?.targetObject as Component;
            if (!TryDuplicate(component, prop.propertyPath, out string? error))
            {
                EditorUtility.DisplayDialog("Duplicate Action", error, "OK");
            }
        }

        internal static bool TryDuplicate(Component? component, string propertyPath, out string? error)
        {
            error = null;
            if (!VrcfQol.Reflection.TryEnsure(out string? err))
            {
                error = err;
                return false;
            }
            if (component == null)
            {
                error = "Could not resolve the parent VRCFury component.";
                return false;
            }
            if (!TryResolveActionList(component, propertyPath, out IList? list, out int index))
            {
                error = "Could not resolve the actions list. The VRCFury layout may have changed.";
                return false;
            }
            object src = list[index];
            if (src == null)
            {
                error = "The selected action is null and can't be duplicated.";
                return false;
            }

            try
            {
                Undo.RegisterCompleteObjectUndo(component, "Duplicate VRCFury action");
                object clone = CloneAction(src);
                list.Insert(index + 1, clone);
                VrcfQol.MarkModified(component);
                VrcfQolLogger.Instance.Info($"Duplicated action #{index + 1} ({src.GetType().Name}) " +
                          $"in place (now at #{index + 2}).");
                return true;
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                VrcfQolLogger.Instance.Exception(ex);
                error = "Duplication failed. See Console.\n\n" + ex.Message;
                return false;
            }
        }

        internal static bool TryGetFlipbookActionCopyInfo(
            Component component,
            string propertyPath,
            out FlipbookActionCopyInfo info,
            out string? error)
        {
            info = default;
            if (!VrcfQol.Reflection.TryEnsure(out string? err))
            {
                error = err;
                return false;
            }
            if (!TryResolveFlipbookAction(component, propertyPath,
                    out _, out int actionIndex, out IList? pages, out int sourcePageIndex, out error))
            {
                return false;
            }

            info = new FlipbookActionCopyInfo
            {
                SourcePageIndex = sourcePageIndex,
                ActionIndex = actionIndex,
                PageCount = pages.Count,
            };
            return true;
        }

        internal static bool TryDuplicateToFlipbookPage(
            Component component,
            string propertyPath,
            int targetPageIndex,
            out string? error)
        {
            error = null;
            if (!VrcfQol.Reflection.TryEnsure(out string? err))
            {
                error = err;
                return false;
            }
            if (!TryResolveFlipbookAction(component, propertyPath,
                    out IList? sourceActions, out int actionIndex, out IList? pages, out int sourcePageIndex, out error))
            {
                return false;
            }
            if (targetPageIndex < 0 || targetPageIndex >= pages.Count)
            {
                error = $"Page #{targetPageIndex + 1} was not found.";
                return false;
            }

            object src = sourceActions[actionIndex];
            if (src == null)
            {
                error = "The selected action is null and can't be duplicated.";
                return false;
            }

            try
            {
                IList? targetActions = EnsureActionListOnPage(pages[targetPageIndex]);
                if (targetActions == null)
                {
                    error = $"Could not resolve the action list on page #{targetPageIndex + 1}.";
                    return false;
                }

                Undo.RegisterCompleteObjectUndo(component, "Duplicate VRCFury action to flipbook page");
                object clone = CloneAction(src);
                targetActions.Add(clone);
                VrcfQol.MarkModified(component);
                VrcfQolLogger.Instance.Info($"Duplicated action #{actionIndex + 1} ({src.GetType().Name}) " +
                          $"from page #{sourcePageIndex + 1} to page #{targetPageIndex + 1}.");
                return true;
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                VrcfQolLogger.Instance.Exception(ex);
                error = "Duplication failed. See Console.\n\n" + ex.Message;
                return false;
            }
        }

        private static bool TryResolveActionList(
            Component component,
            string propertyPath,
            [NotNullWhen(true)] out IList? list,
            out int index)
        {
            list = null;
            index = -1;
            if (string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            Match tail = ActionTailRegex.Match(propertyPath);
            if (!tail.Success)
            {
                return false;
            }

            if (!int.TryParse(tail.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return false;
            }

            string listPath = propertyPath[..tail.Index] + ".actions";
            object? resolved = Walk(component, listPath.TrimStart('.'));
            list = resolved as IList;
            if (list == null || index < 0 || index >= list.Count)
            {
                list = null;
                index = -1;
                return false;
            }
            return true;
        }

        private static bool TryResolveFlipbookAction(
            Component component,
            string propertyPath,
            [NotNullWhen(true)] out IList? sourceActions,
            out int actionIndex,
            [NotNullWhen(true)] out IList? pages,
            out int sourcePageIndex,
            out string? error)
        {
            sourceActions = null;
            actionIndex = -1;
            pages = null;
            sourcePageIndex = -1;
            error = null;

            if (component == null)
            {
                error = "Could not resolve the parent VRCFury component.";
                return false;
            }
            if (string.IsNullOrEmpty(propertyPath))
            {
                error = "Could not resolve this action row.";
                return false;
            }

            Match pageMatch = FlipbookPageActionRegex.Match(propertyPath);
            if (!pageMatch.Success ||
                !int.TryParse(pageMatch.Groups["page"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sourcePageIndex) ||
                !int.TryParse(pageMatch.Groups["action"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out actionIndex))
            {
                error = "This action is not inside a flipbook page.";
                return false;
            }

            if (!TryResolveActionList(component, propertyPath, out sourceActions, out actionIndex))
            {
                error = "Could not resolve the source action list. The VRCFury layout may have changed.";
                return false;
            }

            string flipbookPath = propertyPath[..pageMatch.Index];
            string pagesPath = (flipbookPath + ".pages").TrimStart('.');
            pages = Walk(component, pagesPath) as IList;
            if (pages == null || sourcePageIndex < 0 || sourcePageIndex >= pages.Count)
            {
                pages = null;
                error = "Could not resolve the flipbook pages list. The VRCFury layout may have changed.";
                return false;
            }

            return true;
        }

        private static IList? EnsureActionListOnPage(object page)
        {
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (page == null || !r.TryEnsure(out _))
            {
                return null;
            }

            object state = r.PageStateField.GetValue(page);
            if (state == null)
            {
                state = Activator.CreateInstance(r.StateType);
                r.PageStateField.SetValue(page, state);
            }

            if (r.StateActionsField.GetValue(state) is not IList actions)
            {
                actions = (IList)Activator.CreateInstance(r.StateActionsField.FieldType);
                r.StateActionsField.SetValue(state, actions);
            }
            return actions;
        }

        private static object CloneAction(object src)
        {
            string json = JsonUtility.ToJson(src);
            return JsonUtility.FromJson(json, src.GetType());
        }

        internal static object? Walk(object root, string path)
        {
            if (root == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(path))
            {
                return root;
            }

            string normalised = ArrayDataRegex.Replace(path, "[${index}]");
            object current = root;
            foreach (string raw in normalised.Split('.'))
            {
                if (string.IsNullOrEmpty(raw) || current == null)
                {
                    continue;
                }

                string name;
                int idx;
                Match m = IndexedNameRegex.Match(raw);
                if (m.Success)
                {
                    name = m.Groups["name"].Value;
                    idx = int.Parse(m.Groups["index"].Value, CultureInfo.InvariantCulture);
                }
                else
                {
                    name = raw;
                    idx = -1;
                }
                FieldInfo? field = FindFieldInHierarchy(current.GetType(), name);
                if (field == null)
                {
                    return null;
                }

                current = field.GetValue(current);
                if (idx >= 0)
                {
                    if (current is not IList list)
                    {
                        return null;
                    }

                    if (idx < 0 || idx >= list.Count)
                    {
                        return null;
                    }

                    current = list[idx];
                }
            }
            return current;
        }

        private static FieldInfo? FindFieldInHierarchy(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo f = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (f != null)
                {
                    return f;
                }

                type = type.BaseType;
            }
            return null;
        }
    }
}
