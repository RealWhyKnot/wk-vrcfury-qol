// AutoGlobalParameterTool.cs
//
// Problem this solves:
//   VRCFury generates an internal parameter name for each Toggle. When you
//   update an avatar (e.g., regenerate), VRCFury is free to reshuffle those
//   internal names -- which silently wipes any customisations you've pinned to
//   that parameter (animator constraints, external OSC clients, VRChat's
//   per-avatar parameter memory, etc.).
//
//   The workaround VRCFury itself provides is the "Use Global Parameter"
//   checkbox plus a user-supplied parameter name. As long as you set that
//   string yourself, VRCFury is forced to keep using it.
//
// What this tool does:
//   For every VRCFury Toggle in open scenes it enforces, every ~0.5 s:
//       useGlobalParam = true
//       globalParam    = toggle.name   (i.e. the menu path)
//
//   This is the sensible default, because matching the menu path means the
//   parameter name is already meaningful AND already unique within the menu.
//
//   When two Toggles on the same avatar share a menu path (Ctrl+D duplicates,
//   or two submenus that happen to end in the same leaf), naively writing
//   globalParam = name would produce a collision and VRCFury would error at
//   build. To avoid that, the tool computes per-avatar assignments: the first
//   Toggle (by GlobalObjectId order) keeps the bare menu-path name, later
//   Toggles get " 2", " 3", ... suffixes. The choice of who-wins is stable
//   across editor restarts because GlobalObjectId is stable.
//
//   It's NOT forced blindly: each toggle can opt out via:
//       - Right-click -> "WhyKnot / wk-vrcfury-qol / Disable global parameter sync",
//       - or the inline "Disable" button on the green banner at the top of the
//         Toggle inspector (see VrcfQolInspectorOverlay).
//
//   Opted-out Toggles' current globalParam values seed the "taken" set, so
//   the auto-disambiguation routes around them.
//
//   The opt-out is stored in EditorPrefs keyed by the component's
//   GlobalObjectId, so it survives editor restarts on this machine. (It does
//   NOT travel across machines/users -- that's intentional; opting out is a
//   local preference for a local customisation.)
//
// Non-goals / things this tool deliberately does NOT do:
//   - It does not wrap each sync in Undo. An auto-applied sync on every tick
//     would pollute the Undo stack with noise; instead we just SetDirty so the
//     scene saves. Undo still works normally for the user's direct edits to
//     either field.
//   - It does not touch assets on disk -- only scene/prefab-stage components.
//   - It does not fight the user: if VRCFury removes or renames `globalParam`
//     in a future version, the Reflection cache returns null and this tool
//     becomes a no-op with a visible explanation in the inspector banner.
//   - It only manages Toggle features. Other VRCFury feature types that may
//     declare global parameters (Full Controller, ParamLerper, etc.) are not
//     enumerated here. If a user has those and they collide with a Toggle's
//     name, the user is on their own -- the auto-fix only owns the Toggle
//     side of the collision domain.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("dev.whyknot.wk-vrcfury-qol.Tests.Editor")]

namespace UmeVrcfQol.Tools {

    /// <summary>
    /// Plain data carried into <see cref="AutoGlobalParameterTool.ComputeAssignments"/>.
    /// Kept Unity-free so the algorithm is unit-testable.
    /// </summary>
    internal struct ToggleInfo {
        public string GlobalObjectId;
        public string MenuPath;
        public string CurrentGlobalParam;
        public bool IsOptedOut;
    }

    [InitializeOnLoad]
    internal static class AutoGlobalParameterTool {
        // Keyed per-component via GlobalObjectId.
        private const string OptOutPrefKey = "VrcfQol.AutoUpdateParam.OptOut.";

        // Polling cadence. Cheap -- one reflection read per Toggle per tick.
        private const double TickSeconds = 0.5;
        private static double _nextTick;

        // Tracks the last value we wrote for each component so we only log on
        // transitions (suffix appearing / changing / disappearing), never on
        // every tick of a stable disambiguation.
        private static readonly Dictionary<string, string> _lastWritten =
            new Dictionary<string, string>();

        static AutoGlobalParameterTool() {
            EditorApplication.update += Tick;

            // Right-click menu items for manual control from the Toggle inspector.
            VrcfQol.RegisterToggleTool(
                label: "WhyKnot/wk-vrcfury-qol/Disable global parameter sync",
                action: ctx => SetOptedOut(ctx.vrcfComponent, true),
                priority: 6,
                enabled: ctx => !IsOptedOut(ctx.vrcfComponent)
            );
            VrcfQol.RegisterToggleTool(
                label: "WhyKnot/wk-vrcfury-qol/Enable global parameter sync",
                action: ctx => {
                    SetOptedOut(ctx.vrcfComponent, false);
                    ApplyTo(ctx.vrcfComponent, force: true);
                },
                priority: 6,
                enabled: ctx => IsOptedOut(ctx.vrcfComponent)
            );
            VrcfQol.RegisterToggleTool(
                label: "WhyKnot/wk-vrcfury-qol/Sync global parameter now",
                action: ctx => ApplyTo(ctx.vrcfComponent, force: true),
                priority: 5
            );
        }

        // ------------------------------------------------------------------
        // Opt-out storage
        // ------------------------------------------------------------------

        internal static string GetOptOutKey(Component c) {
            if (c == null) return null;
            // GetGlobalObjectIdSlow returns a string that encodes scene/asset + fileID.
            // Stable enough for per-component preferences on this machine.
            var id = GlobalObjectId.GetGlobalObjectIdSlow(c);
            return OptOutPrefKey + id.ToString();
        }

        internal static bool IsOptedOut(Component c) {
            var k = GetOptOutKey(c);
            return k != null && EditorPrefs.GetBool(k, false);
        }

        internal static void SetOptedOut(Component c, bool value) {
            var k = GetOptOutKey(c);
            if (k == null) return;
            if (value) {
                EditorPrefs.SetBool(k, true);
                VrcfQolLogger.Instance.Info($"Global-parameter auto-update DISABLED for '{c.name}'. " +
                          $"You can re-enable it from the green banner on the Toggle inspector.");
            } else {
                EditorPrefs.DeleteKey(k);
                VrcfQolLogger.Instance.Info($"Global-parameter auto-update re-enabled for '{c.name}'.");
            }
        }

        // ------------------------------------------------------------------
        // Background sync
        // ------------------------------------------------------------------

        private static void Tick() {
            if (EditorApplication.timeSinceStartup < _nextTick) return;
            _nextTick = EditorApplication.timeSinceStartup + TickSeconds;
            try { SyncAll(); }
            catch (Exception ex) { VrcfQolLogger.Instance.Exception(ex); }
        }

        private static void SyncAll() {
            if (!VrcfQol.Reflection.TryEnsure(out _)) return;
            var r = VrcfQol.Reflection;
            if (r.ToggleUseGlobalParamField == null || r.ToggleGlobalParamField == null) {
                // VRCFury version doesn't expose those fields -- nothing to do.
                return;
            }

            // Includes scene objects AND prefab-stage objects; asset-only objects
            // are filtered below by Scene.IsValid.
            var all = Resources.FindObjectsOfTypeAll(r.VRCFuryType);
            var byAvatar = new Dictionary<GameObject, List<(Component component, ToggleInfo info)>>();

            foreach (var o in all) {
                var c = o as Component;
                if (c == null) continue;
                if ((c.hideFlags & (HideFlags.NotEditable | HideFlags.HideAndDontSave)) != 0) continue;
                if (c.gameObject == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!TryReadToggleInfo(c, out var info)) continue;

                var root = PreviewTool.FindAvatarRoot(c) ?? c.gameObject;
                if (!byAvatar.TryGetValue(root, out var list)) {
                    list = new List<(Component, ToggleInfo)>();
                    byAvatar[root] = list;
                }
                list.Add((c, info));
            }

            foreach (var kv in byAvatar) {
                var infos = new List<ToggleInfo>(kv.Value.Count);
                foreach (var pair in kv.Value) infos.Add(pair.info);
                var assignments = ComputeAssignments(infos);
                foreach (var pair in kv.Value) {
                    if (pair.info.IsOptedOut) continue;
                    if (!assignments.TryGetValue(pair.info.GlobalObjectId, out var desired)) continue;
                    WriteAssignment(pair.component, desired);
                }
            }
        }

        /// <summary>
        /// Force a single component to the value the whole-avatar compute pass
        /// produces for it. Used by the right-click menu items and the inspector
        /// banner's "Sync now" / "Turn on" buttons. Computing the avatar's full
        /// assignment set (rather than re-running the old single-component logic)
        /// keeps the force path and the background tick in agreement.
        /// </summary>
        internal static void ApplyTo(Component c, bool force) {
            if (c == null) return;
            if (!VrcfQol.Reflection.TryEnsure(out _)) return;
            var r = VrcfQol.Reflection;
            if (c.GetType() != r.VRCFuryType) return;
            if (!force && IsOptedOut(c)) return;
            if (r.ToggleUseGlobalParamField == null || r.ToggleGlobalParamField == null) return;
            if (!TryReadToggleInfo(c, out var ownInfo)) return;

            // force=true means "I want this component to be auto-synced, even if
            // it's currently flagged opted-out" (used after clearing the opt-out
            // via banner / right-click). In that case treat it as non-opted-out
            // for this compute pass so it actually gets an assignment.
            if (force) ownInfo.IsOptedOut = false;

            var root = PreviewTool.FindAvatarRoot(c) ?? c.gameObject;
            var infos = CollectAvatarToggles(root);
            // Replace the component's own info with our (possibly force-overridden) copy.
            for (var i = 0; i < infos.Count; i++) {
                if (infos[i].GlobalObjectId == ownInfo.GlobalObjectId) {
                    infos[i] = ownInfo;
                    break;
                }
            }

            var assignments = ComputeAssignments(infos);
            if (!assignments.TryGetValue(ownInfo.GlobalObjectId, out var desired)) return;
            WriteAssignment(c, desired);
        }

        /// <summary>
        /// Enumerates every VRCFury Toggle component sharing the given avatar
        /// root and returns plain <see cref="ToggleInfo"/> data. The banner and
        /// the per-component force-apply path both call this.
        /// </summary>
        internal static List<ToggleInfo> CollectAvatarToggles(GameObject avatarRoot) {
            var infos = new List<ToggleInfo>();
            if (avatarRoot == null) return infos;
            if (!VrcfQol.Reflection.TryEnsure(out _)) return infos;
            var r = VrcfQol.Reflection;

            foreach (var o in Resources.FindObjectsOfTypeAll(r.VRCFuryType)) {
                var other = o as Component;
                if (other == null) continue;
                if ((other.hideFlags & (HideFlags.NotEditable | HideFlags.HideAndDontSave)) != 0) continue;
                if (other.gameObject == null) continue;
                if (!other.gameObject.scene.IsValid()) continue;
                var otherRoot = PreviewTool.FindAvatarRoot(other) ?? other.gameObject;
                if (otherRoot != avatarRoot) continue;
                if (!TryReadToggleInfo(other, out var info)) continue;
                infos.Add(info);
            }
            return infos;
        }

        // ------------------------------------------------------------------
        // Pure algorithm
        // ------------------------------------------------------------------

        /// <summary>
        /// For each non-opted-out Toggle, pick a globalParam that does not
        /// collide with any other Toggle on the same avatar. The first Toggle
        /// (by GlobalObjectId order) keeps the bare menu-path; later ones get
        /// " 2", " 3", ... suffixes. Opted-out Toggles' current globalParam
        /// values seed the taken set; they themselves do not get an assignment
        /// in the returned map.
        /// </summary>
        internal static IReadOnlyDictionary<string, string> ComputeAssignments(
            IReadOnlyList<ToggleInfo> toggles
        ) {
            var result = new Dictionary<string, string>();
            var taken = new HashSet<string>();

            // Seed: opted-out toggles' current values are immutable from this
            // tool's perspective, so we must route around them.
            foreach (var t in toggles) {
                if (t.IsOptedOut && !string.IsNullOrEmpty(t.CurrentGlobalParam)) {
                    taken.Add(t.CurrentGlobalParam);
                }
            }

            // Stable order: GlobalObjectId is stable across editor restarts and
            // does not flip when a sibling component is added/removed (unlike
            // component index on GameObject) or when a parent GameObject is
            // renamed (unlike hierarchy path).
            var ordered = toggles
                .Where(t => !t.IsOptedOut)
                .OrderBy(t => t.GlobalObjectId, StringComparer.Ordinal)
                .ToList();

            foreach (var t in ordered) {
                if (string.IsNullOrWhiteSpace(t.MenuPath)) continue;
                var desired = t.MenuPath;
                if (taken.Contains(desired)) {
                    var k = 2;
                    while (taken.Contains($"{t.MenuPath} {k}")) k++;
                    desired = $"{t.MenuPath} {k}";
                }
                result[t.GlobalObjectId] = desired;
                taken.Add(desired);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Reflection helpers
        // ------------------------------------------------------------------

        private static bool TryReadToggleInfo(Component c, out ToggleInfo info) {
            info = default;
            var r = VrcfQol.Reflection;
            object content;
            try { content = r.ContentField.GetValue(c); }
            catch { return false; }
            if (content == null || content.GetType() != r.ToggleType) return false;

            string menuPath, currentParam;
            try {
                menuPath = (string)r.ToggleNameField.GetValue(content) ?? "";
                currentParam = (string)r.ToggleGlobalParamField.GetValue(content) ?? "";
            } catch { return false; }

            info = new ToggleInfo {
                GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(c).ToString(),
                MenuPath = menuPath,
                CurrentGlobalParam = currentParam,
                IsOptedOut = IsOptedOut(c),
            };
            return true;
        }

        private static void WriteAssignment(Component c, string desired) {
            if (c == null || desired == null) return;
            var r = VrcfQol.Reflection;

            object content;
            try { content = r.ContentField.GetValue(c); }
            catch { return; }
            if (content == null || content.GetType() != r.ToggleType) return;

            var useField = r.ToggleUseGlobalParamField;
            var paramField = r.ToggleGlobalParamField;
            if (useField == null || paramField == null) return;

            bool use;
            string currentParam, name;
            try {
                use = (bool)useField.GetValue(content);
                currentParam = (string)paramField.GetValue(content) ?? "";
                name = (string)r.ToggleNameField.GetValue(content) ?? "";
            } catch { return; }

            // Don't force a name onto an unnamed toggle -- "" isn't a valid
            // parameter name and would only noisy SetDirty.
            if (string.IsNullOrWhiteSpace(name)) return;

            var changed = false;
            if (!use) {
                useField.SetValue(content, true);
                changed = true;
            }
            if (currentParam != desired) {
                paramField.SetValue(content, desired);
                LogIfDisambiguationTransition(c, name, desired);
                changed = true;
            }
            if (changed) {
                EditorUtility.SetDirty(c);
            }
        }

        private static void LogIfDisambiguationTransition(Component c, string menuPath, string newWritten) {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(c).ToString();
            _lastWritten.TryGetValue(id, out var last);
            // Log when the disambiguated value actually changes AND the new
            // value differs from the menu path (i.e. a suffix is involved).
            if (newWritten != last && newWritten != menuPath) {
                VrcfQolLogger.Instance.Info(
                    $"Auto-disambiguated globalParam on '{c.gameObject.name}': " +
                    $"'{menuPath}' already used on this avatar; using '{newWritten}' instead.");
            }
            _lastWritten[id] = newWritten;
        }
    }
}
