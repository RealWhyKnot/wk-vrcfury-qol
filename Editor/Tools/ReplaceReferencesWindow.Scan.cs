// ReplaceReferencesWindow.Scan.cs

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;

namespace UmeVrcfQol.Tools {

    internal sealed partial class ReplaceReferencesWindow {

        // ---------------- Scan ---------------------------------------------

        private void Rescan() {
            _groups.Clear();
            if (_searchRoots.Count == 0) { _scanSummary = ""; return; }

            if (!VrcfQol.Reflection.TryEnsure(out var error)) {
                _scanSummary = error;
                return;
            }
            var r = VrcfQol.Reflection;

            int componentsScanned = 0;
            int rootsActive = 0;
            var seenComponents = new HashSet<Component>();
            // Buffer all sites first, then bucket by CurrentValue. A dict
            // keyed by Object identity would be cleaner, but Unity's Object
            // overrides == in ways that make dict-keys fragile across
            // destruction; running a linear search at scan time is fine -
            // counts are tiny relative to property iteration cost.
            var sites = new List<RefSite>();
            foreach (var entry in _searchRoots) {
                var root = entry.GameObject;
                if (root == null) continue;
                rootsActive++;
                Component[] components;
                if (entry.IncludeChildren) {
                    components = root.GetComponentsInChildren(r.VRCFuryType, true);
                } else {
                    components = root.GetComponents(r.VRCFuryType);
                }
                foreach (var c in components) {
                    if (c == null || !seenComponents.Add(c)) continue;
                    componentsScanned++;
                    ScanComponent(c, sites);
                }
            }

            // Group by CurrentValue. Use SequenceEqual via reference identity
            // (Object's == handles the destroyed-object case).
            foreach (var s in sites) {
                var existing = _groups.FirstOrDefault(g => g.CurrentValue == s.CurrentValue);
                if (existing == null) {
                    existing = new RefGroup { CurrentValue = s.CurrentValue };
                    _groups.Add(existing);
                }
                existing.Sites.Add(s);
            }

            // Stable, predictable ordering: by current value type, then name,
            // then deterministic site path so two identical-looking groups
            // still sort the same way across rescans.
            _groups.Sort((a, b) => {
                int t = string.Compare(
                    a.CurrentValue == null ? "" : a.CurrentValue.GetType().Name,
                    b.CurrentValue == null ? "" : b.CurrentValue.GetType().Name,
                    System.StringComparison.Ordinal);
                if (t != 0) return t;
                int n = string.Compare(
                    a.CurrentValue == null ? "" : a.CurrentValue.name,
                    b.CurrentValue == null ? "" : b.CurrentValue.name,
                    System.StringComparison.Ordinal);
                if (n != 0) return n;
                return string.Compare(
                    a.Sites.Count > 0 ? a.Sites[0].PropertyPath : "",
                    b.Sites.Count > 0 ? b.Sites[0].PropertyPath : "",
                    System.StringComparison.Ordinal);
            });
            foreach (var g in _groups) {
                g.Sites.Sort((x, y) => string.Compare(x.PropertyPath, y.PropertyPath, System.StringComparison.Ordinal));
            }

            _scanSummary = $"Scanned {componentsScanned} VRCFury component(s) across {rootsActive} root(s).";
        }

        private static void ScanComponent(Component vrcf, List<RefSite> output) {
            using (var so = new SerializedObject(vrcf)) {
                var iter = so.GetIterator();
                if (!iter.NextVisible(true)) return;
                do {
                    if (iter.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var current = iter.objectReferenceValue;
                    if (current == null) continue;
                    if (iter.propertyPath == "m_Script") continue;

                    output.Add(new RefSite {
                        VrcfComponent  = vrcf,
                        GameObjectPath = PathUtility.GetGameObjectPath(vrcf.gameObject),
                        FeatureType    = GetEnclosingFeatureTypeName(so, iter.propertyPath),
                        PropertyPath   = iter.propertyPath,
                        CurrentValue   = current,
                    });
                } while (iter.NextVisible(true));
            }
        }

        private static string GetEnclosingFeatureTypeName(SerializedObject so, string propertyPath) {
            string parent = propertyPath;
            while (true) {
                int dot = parent.LastIndexOf('.');
                if (dot < 0) break;
                parent = parent.Substring(0, dot);
                var p = so.FindProperty(parent);
                if (p == null) continue;
                if (p.propertyType == SerializedPropertyType.ManagedReference) {
                    return ShortenManagedReferenceTypeName(p.managedReferenceFullTypename);
                }
            }
            return "VRCFury";
        }

        private static string ShortenManagedReferenceTypeName(string fullName) {
            if (string.IsNullOrEmpty(fullName)) return "VRCFury";
            int space = fullName.LastIndexOf(' ');
            string typeName = space >= 0 ? fullName.Substring(space + 1) : fullName;
            int lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 ? typeName.Substring(lastDot + 1) : typeName;
        }
    }
}
