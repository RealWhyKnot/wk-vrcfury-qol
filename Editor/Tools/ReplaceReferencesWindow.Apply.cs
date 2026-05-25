// ReplaceReferencesWindow.Apply.cs

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;

namespace UmeVrcfQol.Tools {

    internal sealed partial class ReplaceReferencesWindow {

        // ---------------- Apply --------------------------------------------

        private void Apply() {
            var queuedGroups = _groups.Where(g => g.HasReplacement).ToList();
            if (queuedGroups.Count == 0) return;

            // Flatten to (component, propertyPath, expectedCurrent, replacement)
            // tuples grouped by component for one SerializedObject per component.
            var flat = queuedGroups
                .SelectMany(g => g.Sites.Select(s => (Site: s, Replacement: g.Replacement)))
                .Where(x => x.Site.VrcfComponent != null)
                .ToList();

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VRCF QoL: Replace VRCFury references");

            int applied = 0;
            int skipped = 0;
            try {
                foreach (var byComp in flat.GroupBy(x => x.Site.VrcfComponent)) {
                    using (var so = new SerializedObject(byComp.Key)) {
                        bool anyChanged = false;
                        foreach (var x in byComp) {
                            var prop = so.FindProperty(x.Site.PropertyPath);
                            if (prop == null) { skipped++; continue; }
                            if (prop.propertyType != SerializedPropertyType.ObjectReference) {
                                skipped++; continue;
                            }
                            // Snapshot guard: if the current value drifted between
                            // scan and apply, refuse rather than overwrite a
                            // newly changed reference.
                            if (prop.objectReferenceValue != x.Site.CurrentValue) { skipped++; continue; }
                            prop.objectReferenceValue = x.Replacement;
                            applied++;
                            anyChanged = true;
                        }
                        if (anyChanged) so.ApplyModifiedProperties();
                    }
                }
                Undo.CollapseUndoOperations(group);
            } catch (System.Exception ex) {
                Undo.RevertAllInCurrentGroup();
                VrcfQolLogger.Instance.Exception(ex);
                EditorUtility.DisplayDialog("Replace References",
                    "Apply failed; changes reverted.\n\n" + ex.Message, "OK");
                return;
            }

            string skipNote = skipped > 0 ? $" Skipped {skipped} stale entr{(skipped == 1 ? "y" : "ies")}." : "";
            VrcfQolLogger.Instance.Info($"Replaced {applied} reference(s) across {queuedGroups.Count} unique object(s)." + skipNote);

            // Re-scan: groups that were fully applied disappear (their old refs
            // no longer exist); the new value may itself become a group if it
            // overlaps with anything else on the scan roots.
            Rescan();
        }
    }
}
