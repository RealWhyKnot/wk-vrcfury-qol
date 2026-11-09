#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("dev.whyknot.wk-vrcfury-qol.Tests.Editor")]

namespace UmeVrcfQol.Tools
{
    [InitializeOnLoad]
    internal static class AutoGlobalParameterTool
    {
        private const string OptOutPrefKey = "VrcfQol.AutoUpdateParam.OptOut.";

        private const double TickSeconds = 0.5;
        private static double NextTick;

        private static readonly Dictionary<string, string> LastWritten =
            new(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> LastSeen =
            new(StringComparer.Ordinal);

        static AutoGlobalParameterTool()
        {
            EditorApplication.update += Tick;

            VrcfQol.RegisterToggleTool(
                label: "WhyKnot/wk-vrcfury-qol/Disable global parameter sync",
                action: ctx => SetOptedOut(ctx.VrcfComponent, value: true),
                priority: 6,
                enabled: ctx => !IsOptedOut(ctx.VrcfComponent)
            );
            VrcfQol.RegisterToggleTool(
                label: "WhyKnot/wk-vrcfury-qol/Enable global parameter sync",
                action: ctx =>
                {
                    SetOptedOut(ctx.VrcfComponent, value: false);
                    ApplyTo(ctx.VrcfComponent, force: true);
                },
                priority: 6,
                enabled: ctx => IsOptedOut(ctx.VrcfComponent)
            );
            VrcfQol.RegisterToggleTool(
                label: "WhyKnot/wk-vrcfury-qol/Sync global parameter now",
                action: ctx => ApplyTo(ctx.VrcfComponent, force: true),
                priority: 5
            );
        }

        internal static string? GetOptOutKey(Component c)
        {
            if (c == null)
            {
                return null;
            }

            var id = GlobalObjectId.GetGlobalObjectIdSlow(c);
            return OptOutPrefKey + id.ToString();
        }

        internal static bool IsOptedOut(Component c)
        {
            string? k = GetOptOutKey(c);
            return k != null && EditorPrefs.GetBool(k, defaultValue: false);
        }

        internal static void SetOptedOut(Component c, bool value)
        {
            string? k = GetOptOutKey(c);
            if (k == null)
            {
                return;
            }

            if (value)
            {
                EditorPrefs.SetBool(k, value: true);
                VrcfQolLogger.Instance.Info($"Global-parameter auto-update DISABLED for '{c.name}'. " +
                          "You can re-enable it from the green banner on the Toggle inspector.");
            }
            else
            {
                EditorPrefs.DeleteKey(k);
                VrcfQolLogger.Instance.Info($"Global-parameter auto-update re-enabled for '{c.name}'.");
            }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < NextTick)
            {
                return;
            }

            NextTick = EditorApplication.timeSinceStartup + TickSeconds;
            try
            {
                SyncAll();
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                VrcfQolLogger.Instance.Exception(ex);
            }
        }

        private static bool IsLiveSceneComponent(Component c)
        {
            return (c.hideFlags & (HideFlags.NotEditable | HideFlags.HideAndDontSave)) == HideFlags.None
                && c.gameObject != null
                && c.gameObject.scene.IsValid();
        }

        internal static GameObject AvatarRootOf(Component c)
        {
            GameObject? root = PreviewTool.FindAvatarRoot(c);
            return root != null ? root : c.gameObject;
        }

        private static Dictionary<GameObject, List<(Component component, ToggleInfo info)>> GroupTogglesByAvatar(Type vrcfuryType)
        {
            var byAvatar = new Dictionary<GameObject, List<(Component component, ToggleInfo info)>>();
            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(vrcfuryType))
            {
                var c = o as Component;
                if (c == null || !IsLiveSceneComponent(c))
                {
                    continue;
                }

                if (!TryReadToggleInfo(c, out ToggleInfo info))
                {
                    continue;
                }

                GameObject root = AvatarRootOf(c);
                if (!byAvatar.TryGetValue(root, out List<(Component component, ToggleInfo info)>? list))
                {
                    list = new List<(Component, ToggleInfo)>();
                    byAvatar[root] = list;
                }
                list.Add((c, info));
            }

            return byAvatar;
        }

        private static void SyncAll()
        {
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out _))
            {
                return;
            }

            if (r.ToggleUseGlobalParamField == null || r.ToggleGlobalParamField == null)
            {
                return;
            }

            foreach (KeyValuePair<GameObject, List<(Component component, ToggleInfo info)>> kv in GroupTogglesByAvatar(r.VRCFuryType))
            {
                var infos = new List<ToggleInfo>(kv.Value.Count);
                foreach ((Component _, ToggleInfo info) in kv.Value)
                {
                    infos.Add(info);
                }

                IReadOnlyDictionary<string, string> assignments = ComputeAssignments(infos);
                foreach ((Component component, ToggleInfo info) in kv.Value)
                {
                    string id = info.GlobalObjectId;
                    LastSeen[id] = !info.IsOptedOut
                        && assignments.TryGetValue(id, out string? desired)
                        && WriteAssignment(component, desired, info)
                        ? desired
                        : info.CurrentGlobalParam;
                }
            }
        }

        internal static void ApplyTo(Component c, bool force)
        {
            if (c == null)
            {
                return;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out _))
            {
                return;
            }

            if (c.GetType() != r.VRCFuryType)
            {
                return;
            }

            if (!force && IsOptedOut(c))
            {
                return;
            }

            if (r.ToggleUseGlobalParamField == null || r.ToggleGlobalParamField == null)
            {
                return;
            }

            if (!TryReadToggleInfo(c, out ToggleInfo ownInfo))
            {
                return;
            }

            if (force)
            {
                ownInfo.IsOptedOut = false;
                ownInfo.UsesGlobalParam = false;
            }

            GameObject root = AvatarRootOf(c);
            List<ToggleInfo> infos = CollectAvatarToggles(root);
            for (int i = 0; i < infos.Count; i++)
            {
                if (string.Equals(infos[i].GlobalObjectId, ownInfo.GlobalObjectId, StringComparison.Ordinal))
                {
                    infos[i] = ownInfo;
                    break;
                }
            }

            IReadOnlyDictionary<string, string> assignments = ComputeAssignments(infos);
            if (!assignments.TryGetValue(ownInfo.GlobalObjectId, out string? desired))
            {
                return;
            }

            if (WriteAssignment(c, desired, ownInfo))
            {
                LastSeen[ownInfo.GlobalObjectId] = desired;
            }
        }

        internal static List<ToggleInfo> CollectAvatarToggles(GameObject avatarRoot)
        {
            var infos = new List<ToggleInfo>();
            if (avatarRoot == null)
            {
                return infos;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out _))
            {
                return infos;
            }

            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(r.VRCFuryType))
            {
                var other = o as Component;
                if (other == null || !IsLiveSceneComponent(other))
                {
                    continue;
                }

                if (AvatarRootOf(other) != avatarRoot)
                {
                    continue;
                }

                if (!TryReadToggleInfo(other, out ToggleInfo info))
                {
                    continue;
                }

                infos.Add(info);
            }
            return infos;
        }

        private static string WantedName(ToggleInfo t)
        {
            return t.IsPinned ? t.CurrentGlobalParam : t.MenuPath;
        }

        private static void ClaimWantedNames(List<ToggleInfo> active, HashSet<string> taken, Dictionary<string, string> result)
        {
            var claimOrder = active
                .OrderBy(t => t.HeldPreviously && string.Equals(WantedName(t), t.CurrentGlobalParam, StringComparison.Ordinal) ? 0 : t.IsPinned ? 1 : 2)
                .ThenBy(t => t.GlobalObjectId, StringComparer.Ordinal)
                .ToList();

            foreach (ToggleInfo t in claimOrder)
            {
                string wanted = WantedName(t);
                if (string.IsNullOrWhiteSpace(wanted) || taken.Contains(wanted))
                {
                    continue;
                }

                result[t.GlobalObjectId] = wanted;
                taken.Add(wanted);
            }
        }

        private static void AssignNumberedNames(List<ToggleInfo> active, HashSet<string> taken, Dictionary<string, string> result)
        {
            foreach (ToggleInfo t in active)
            {
                if (result.ContainsKey(t.GlobalObjectId) || string.IsNullOrWhiteSpace(t.MenuPath))
                {
                    continue;
                }

                string desired = t.MenuPath;
                if (taken.Contains(desired))
                {
                    int k = 2;
                    while (taken.Contains($"{t.MenuPath} {k}"))
                    {
                        k++;
                    }

                    desired = $"{t.MenuPath} {k}";
                }
                result[t.GlobalObjectId] = desired;
                taken.Add(desired);
            }
        }

        internal static IReadOnlyDictionary<string, string> ComputeAssignments(IReadOnlyList<ToggleInfo> toggles)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var taken = new HashSet<string>(StringComparer.Ordinal);

            foreach (ToggleInfo t in toggles)
            {
                if (t.IsOptedOut && !string.IsNullOrEmpty(t.CurrentGlobalParam))
                {
                    taken.Add(t.CurrentGlobalParam);
                }
            }

            var active = toggles
                .Where(t => !t.IsOptedOut)
                .OrderBy(t => t.GlobalObjectId, StringComparer.Ordinal)
                .ToList();

            ClaimWantedNames(active, taken, result);
            AssignNumberedNames(active, taken, result);
            return result;
        }

        private static bool TryGetToggleContent(VrcfQol.ReflectionCache r, Component c, [NotNullWhen(true)] out object? content)
        {
            content = null;
            if (!r.TryEnsure(out _))
            {
                return false;
            }

            try
            {
                content = r.ContentField.GetValue(c);
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                return false;
            }

            return content != null && content.GetType() == r.ToggleType;
        }

        private static bool TryReadToggleInfo(Component c, out ToggleInfo info)
        {
            info = default;
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            FieldInfo? paramField = r.ToggleGlobalParamField;
            if (paramField == null || !r.TryEnsure(out _) || !TryGetToggleContent(r, c, out object? content))
            {
                return false;
            }

            string menuPath, currentParam;
            bool usesGlobalParam;
            try
            {
                menuPath = (string)r.ToggleNameField.GetValue(content) ?? "";
                currentParam = (string)paramField.GetValue(content) ?? "";
                usesGlobalParam = r.ToggleUseGlobalParamField != null
                    && (bool)r.ToggleUseGlobalParamField.GetValue(content);
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                return false;
            }

            string id = GlobalObjectId.GetGlobalObjectIdSlow(c).ToString();
            info = new ToggleInfo
            {
                GlobalObjectId = id,
                MenuPath = menuPath,
                CurrentGlobalParam = currentParam,
                IsOptedOut = IsOptedOut(c),
                UsesGlobalParam = usesGlobalParam,
                AssignedByTool = LastWritten.TryGetValue(id, out string? written) && string.Equals(written, currentParam, StringComparison.Ordinal),
                HeldPreviously = LastSeen.TryGetValue(id, out string? seen) && string.Equals(seen, currentParam, StringComparison.Ordinal),
            };
            return true;
        }

        private static bool WriteAssignment(Component c, string desired, ToggleInfo info)
        {
            if (c == null || desired == null)
            {
                return false;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            FieldInfo? useField = r.ToggleUseGlobalParamField;
            FieldInfo? paramField = r.ToggleGlobalParamField;
            if (!r.TryEnsure(out _) || !TryGetToggleContent(r, c, out object? content) || useField == null || paramField == null)
            {
                return false;
            }

            bool use;
            string currentParam, name;
            try
            {
                use = (bool)useField.GetValue(content);
                currentParam = (string)paramField.GetValue(content) ?? "";
                name = (string)r.ToggleNameField.GetValue(content) ?? "";
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (!use)
            {
                useField.SetValue(content, value: true);
            }

            bool renamed = !string.Equals(currentParam, desired, StringComparison.Ordinal);
            if (renamed)
            {
                paramField.SetValue(content, desired);
                if (use && !info.AssignedByTool && !string.IsNullOrWhiteSpace(currentParam))
                {
                    VrcfQolLogger.Instance.Warning(
                        $"Renamed Global Parameter on '{c.gameObject.name}' from '{currentParam}' to '{desired}'. " +
                        "VRChat treats it as a new parameter, so its saved value resets.");
                }
                else
                {
                    LogIfDisambiguationTransition(c, name, desired);
                }
                LastWritten[GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()] = desired;
            }

            if (!use || renamed)
            {
                VrcfQol.MarkModified(c);
            }
            return true;
        }

        private static void LogIfDisambiguationTransition(Component c, string menuPath, string newWritten)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(c).ToString();
            LastWritten.TryGetValue(id, out string? last);
            if (!string.Equals(newWritten, last, StringComparison.Ordinal) && !string.Equals(newWritten, menuPath, StringComparison.Ordinal))
            {
                VrcfQolLogger.Instance.Info(
                    $"Auto-disambiguated globalParam on '{c.gameObject.name}': " +
                    $"'{menuPath}' already used on this avatar; using '{newWritten}' instead.");
            }
        }
    }
}
