#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using UmeVrcfQol.Internal.Utilities;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Tools
{
    [InitializeOnLoad]
    internal static class MigrateIntoFlipbookTool
    {
        static MigrateIntoFlipbookTool()
        {
            VrcfQol.RegisterPropertyTool(
                label: "WhyKnot/wk-vrcfury-qol/Migrate child toggles as pages",
                match: IsFlipbookBuilderAction,
                action: Run,
                priority: 10
            );
        }

        private const string DialogTitle = "Migrate Child Toggles";

        private static bool IsFlipbookBuilderAction(SerializedProperty prop)
        {
            if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference)
            {
                return false;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out _))
            {
                return false;
            }

            string expected = r.FlipbookBuilderActionType.FullName;
            string typeName = prop.managedReferenceFullTypename;
            return !string.IsNullOrEmpty(typeName) && typeName.EndsWith(" " + expected, StringComparison.Ordinal);
        }

        private static void Run(SerializedProperty prop)
        {
            var destVrcf = prop.serializedObject?.targetObject as Component;
            if (destVrcf == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Could not resolve the parent VRCFury component.", "OK");
                return;
            }

            if (!TryResolveDestination(destVrcf, out IList? pages, out string destName, out string? problem))
            {
                EditorUtility.DisplayDialog(DialogTitle, problem ?? "Could not resolve the parent VRCFury component.", "OK");
                return;
            }

            List<SourceInfo> sources = CollectSources(destVrcf);
            if (sources.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    $"No non-flipbook VRCFury toggles found under '{destVrcf.gameObject.name}'.", "OK");
                return;
            }

            string preview = BuildPreview(destName, pages.Count, sources);
            if (!EditorUtility.DisplayDialog("Migrate Child Toggles Into Flipbook", preview, "Migrate", "Cancel"))
            {
                return;
            }

            ApplyMigration(destVrcf, pages, sources, destName);
        }

        private static bool TryResolveDestination(
            Component destVrcf,
            [NotNullWhen(true)] out IList? pages,
            out string destName,
            out string? problem)
        {
            pages = null;
            destName = "";
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out problem))
            {
                return false;
            }

            if (destVrcf.GetType() != r.VRCFuryType)
            {
                problem = "Could not resolve the parent VRCFury component.";
                return false;
            }

            object destContent = r.ContentField.GetValue(destVrcf);
            if (destContent == null || destContent.GetType() != r.ToggleType)
            {
                problem = "Parent VRCFury component does not hold a Toggle.";
                return false;
            }

            destName = (string)r.ToggleNameField.GetValue(destContent) ?? "";
            object destState = r.ToggleStateField.GetValue(destContent);
            object? flipbookAction = VrcfQol.FindFlipbookAction(r.StateActionsField.GetValue(destState) as IList);
            if (flipbookAction == null)
            {
                problem = "This Toggle no longer contains a Flipbook Builder action.";
                return false;
            }

            pages = r.PagesField.GetValue(flipbookAction) as IList;
            if (pages == null)
            {
                problem = "Could not read the flipbook's pages. The VRCFury layout may have changed.";
                return false;
            }

            return true;
        }

        private static List<SourceInfo> CollectSources(Component destVrcf)
        {
            var sources = new List<SourceInfo>();
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out _))
            {
                return sources;
            }

            foreach (Component c in destVrcf.gameObject.GetComponentsInChildren(r.VRCFuryType, includeInactive: true))
            {
                if (c == null || c == destVrcf)
                {
                    continue;
                }

                object content = r.ContentField.GetValue(c);
                if (content == null || content.GetType() != r.ToggleType)
                {
                    continue;
                }

                object state = r.ToggleStateField.GetValue(content);
                if (VrcfQol.FindFlipbookAction(r.StateActionsField.GetValue(state) as IList) != null)
                {
                    continue;
                }

                sources.Add(new SourceInfo
                {
                    Component = c,
                    State = state,
                    Name = (string)r.ToggleNameField.GetValue(content) ?? "",
                    LostSettings = SettingsLostInMigration(content),
                });
            }

            return sources;
        }

        private static string BuildPreview(string destName, int pageCount, List<SourceInfo> sources)
        {
            var preview = new StringBuilder();
            preview.AppendLine($"Destination flipbook: {(string.IsNullOrEmpty(destName) ? "(unnamed)" : destName)}");
            preview.AppendLine($"  ({pageCount} existing page(s) — preserved; migrated toggles appended after)");
            preview.AppendLine();
            preview.AppendLine($"{sources.Count} toggle(s) will be migrated in hierarchy order:");
            foreach (SourceInfo s in sources)
            {
                preview.AppendLine("  • " + (string.IsNullOrEmpty(s.Name) ? "(unnamed)" : s.Name) +
                                   "  [" + PathUtility.GetGameObjectPath(s.Component.gameObject) + "]");
                if (s.LostSettings.Count > 0)
                {
                    preview.AppendLine("      Not carried over: " +
                                       string.Join(", ", s.LostSettings.Select(ObjectNames.NicifyVariableName)));
                }
            }
            preview.AppendLine();
            if (sources.Any(s => s.LostSettings.Count > 0))
            {
                preview.AppendLine("A flipbook page keeps only the toggle's actions, so the settings listed above are lost.");
                preview.AppendLine();
            }
            preview.AppendLine("Source VRCFury components will be DELETED (Undo restores everything).");
            return preview.ToString();
        }

        private static void ApplyMigration(Component destVrcf, IList pages, List<SourceInfo> sources, string destName)
        {
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out _))
            {
                return;
            }

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VRCF QoL: Migrate toggles into flipbook");
            bool settled = false;
            try
            {
                Undo.RegisterCompleteObjectUndo(destVrcf, "Append migrated pages to flipbook");
                foreach (SourceInfo s in sources)
                {
                    object newPage = Activator.CreateInstance(r.FlipbookPageType);
                    r.PageStateField.SetValue(newPage, s.State);
                    pages.Add(newPage);
                }
                VrcfQol.MarkModified(destVrcf);
                foreach (SourceInfo s in sources)
                {
                    if (s.Component != null)
                    {
                        Undo.DestroyObjectImmediate(s.Component);
                    }
                }
                Undo.CollapseUndoOperations(group);
                VrcfQolLogger.Instance.Info($"Migrated {sources.Count} toggles into flipbook '{destName}'. " +
                          $"Flipbook now has {pages.Count} page(s).");
                settled = true;
            }
            catch (Exception e) when (VrcfQol.IsToolFailure(e))
            {
                VrcfQolLogger.Instance.Exception(e);
                Undo.RevertAllInCurrentGroup();
                settled = true;
                EditorUtility.DisplayDialog(DialogTitle,
                    "Migration failed; changes reverted.\n\n" + e.Message, "OK");
            }
            finally
            {
                if (!settled)
                {
                    Undo.RevertAllInCurrentGroup();
                }
            }
        }

        private static List<string> SettingsLostInMigration(object toggle)
        {
            try
            {
                object defaults = Activator.CreateInstance(toggle.GetType());
                return ChangedSerializedFields(toggle, defaults, "name", "state");
            }
            catch (Exception e) when (VrcfQol.IsToolFailure(e))
            {
                VrcfQolLogger.Instance.Exception(e);
                return new List<string>();
            }
        }

        internal static List<string> ChangedSerializedFields(object value, object defaults, params string[] skip)
        {
            return ChangedSerializedFields(value, defaults, 0, skip);
        }

        private static List<string> ChangedSerializedFields(object value, object defaults, int depth, string[] skip)
        {
            var changed = new List<string>();
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo field in value.GetType().GetFields(Flags))
            {
                if (skip.Contains(field.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                if (field.IsNotSerialized)
                {
                    continue;
                }

                if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), inherit: true)
                    && !field.IsDefined(typeof(SerializeReference), inherit: true))
                {
                    continue;
                }

                if (field.IsDefined(typeof(ObsoleteAttribute), inherit: true))
                {
                    continue;
                }

                if (!SerializedEquals(field.FieldType, field.GetValue(value), field.GetValue(defaults), depth))
                {
                    changed.Add(field.Name);
                }
            }
            return changed;
        }

        private static bool SerializedEquals(Type type, object? a, object? b, int depth)
        {
            if (type == typeof(string))
            {
                return string.Equals((string?)a ?? "", (string?)b ?? "", StringComparison.Ordinal);
            }

            if (type.IsPrimitive || type.IsEnum)
            {
                return Equals(a, b);
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return (a as UnityEngine.Object) == (b as UnityEngine.Object);
            }

            if (type.IsValueType)
            {
                return Equals(a, b);
            }

            if (depth > 8)
            {
                return true;
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                return ListsEqual(a as IList, b as IList, depth);
            }

            a ??= CreateDefault(type);
            b ??= CreateDefault(type);
            return a == null || b == null
                ? a == b
                : a.GetType() == b.GetType() && ChangedSerializedFields(a, b, depth + 1, Array.Empty<string>()).Count == 0;
        }

        private static bool ListsEqual(IList? listA, IList? listB, int depth)
        {
            int count = listA?.Count ?? 0;
            if (count != (listB?.Count ?? 0))
            {
                return false;
            }

            if (listA == null || listB == null)
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                object? x = listA[i];
                object? y = listB[i];
                if (x == null || y == null)
                {
                    if (x != y)
                    {
                        return false;
                    }

                    continue;
                }

                if (x.GetType() != y.GetType() || !SerializedEquals(x.GetType(), x, y, depth + 1))
                {
                    return false;
                }
            }

            return true;
        }

        private static object? CreateDefault(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                return null;
            }
        }

        private struct SourceInfo
        {
            public Component Component;
            public object State;
            public string Name;
            public List<string> LostSettings;
        }
    }
}
