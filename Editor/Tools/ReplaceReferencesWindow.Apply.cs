#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    internal sealed partial class ReplaceReferencesWindow
    {
        private void Apply()
        {
            var queuedGroups = _groups.Where(g => g.HasReplacement).ToList();
            if (queuedGroups.Count == 0)
            {
                return;
            }

            var flat = queuedGroups
                .SelectMany(g => g.Sites.Select(s => (Site: s, g.Replacement)))
                .Where(x => x.Site.VrcfComponent != null)
                .ToList();

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VRCF QoL: Replace VRCFury references");

            int applied = 0;
            int skipped = 0;
            bool settled = false;
            try
            {
                foreach (IGrouping<Component, (RefSite Site, Object? Replacement)> byComp in flat.GroupBy(x => x.Site.VrcfComponent))
                {
                    ApplySites(byComp.Key, byComp, ref applied, ref skipped);
                }

                Undo.CollapseUndoOperations(group);
                settled = true;
            }
            catch (Exception ex) when (VrcfQol.IsToolFailure(ex))
            {
                Undo.RevertAllInCurrentGroup();
                settled = true;
                VrcfQolLogger.Instance.Exception(ex);
                EditorUtility.DisplayDialog("Replace References",
                    "Apply failed; changes reverted.\n\n" + ex.Message, "OK");
                return;
            }
            finally
            {
                if (!settled)
                {
                    Undo.RevertAllInCurrentGroup();
                }
            }

            string skipNote = skipped > 0 ? $" Skipped {skipped} stale entr{(skipped == 1 ? "y" : "ies")}." : "";
            VrcfQolLogger.Instance.Info($"Replaced {applied} reference(s) across {queuedGroups.Count} unique object(s)." + skipNote);

            Rescan();
        }

        private static void ApplySites(
            Component component,
            IEnumerable<(RefSite Site, Object? Replacement)> sites,
            ref int applied,
            ref int skipped)
        {
            using var so = new SerializedObject(component);
            bool anyChanged = false;
            foreach ((RefSite site, Object? replacement) in sites)
            {
                SerializedProperty prop = so.FindProperty(site.PropertyPath);
                if (prop == null
                    || prop.propertyType != SerializedPropertyType.ObjectReference
                    || prop.objectReferenceValue != site.CurrentValue)
                {
                    skipped++;
                    continue;
                }

                prop.objectReferenceValue = replacement;
                applied++;
                anyChanged = true;
            }

            if (anyChanged)
            {
                so.ApplyModifiedProperties();
            }
        }
    }
}
