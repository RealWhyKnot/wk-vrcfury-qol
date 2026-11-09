#nullable enable

using System;
using System.Collections.Generic;
using UmeVrcfQol.Internal.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    internal sealed partial class ReplaceReferencesWindow
    {
        private void Rescan()
        {
            _groups.Clear();
            if (_searchRoots.Count == 0)
            {
                _scanSummary = "";
                return;
            }

            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            if (!r.TryEnsure(out string? error))
            {
                _scanSummary = error ?? "";
                return;
            }

            List<RefSite> sites = CollectSites(r.VRCFuryType, out int componentsScanned, out int rootsActive);
            foreach (RefSite s in sites)
            {
                RefGroup? existing = _groups.Find(g => g.CurrentValue == s.CurrentValue);
                if (existing == null)
                {
                    existing = new RefGroup(s.CurrentValue);
                    _groups.Add(existing);
                }
                existing.Sites.Add(s);
            }

            _groups.Sort(CompareGroups);
            foreach (RefGroup g in _groups)
            {
                g.Sites.Sort((x, y) => string.Compare(x.PropertyPath, y.PropertyPath, StringComparison.Ordinal));
            }

            _scanSummary = $"Scanned {componentsScanned} VRCFury component(s) across {rootsActive} root(s).";
        }

        private List<RefSite> CollectSites(Type vrcfuryType, out int componentsScanned, out int rootsActive)
        {
            componentsScanned = 0;
            rootsActive = 0;
            var seenComponents = new HashSet<Component>();
            var sites = new List<RefSite>();
            foreach (SearchRoot entry in _searchRoots)
            {
                GameObject? root = entry.GameObject;
                if (root == null)
                {
                    continue;
                }

                rootsActive++;
                Component[] components = entry.IncludeChildren
                    ? root.GetComponentsInChildren(vrcfuryType, includeInactive: true)
                    : root.GetComponents(vrcfuryType);
                foreach (Component c in components)
                {
                    if (c == null || !seenComponents.Add(c))
                    {
                        continue;
                    }

                    componentsScanned++;
                    ScanComponent(c, sites);
                }
            }

            return sites;
        }

        private static int CompareGroups(RefGroup a, RefGroup b)
        {
            int t = string.Compare(
                a.CurrentValue == null ? "" : a.CurrentValue.GetType().Name,
                b.CurrentValue == null ? "" : b.CurrentValue.GetType().Name,
                StringComparison.Ordinal);
            if (t != 0)
            {
                return t;
            }

            int n = string.Compare(
                a.CurrentValue == null ? "" : a.CurrentValue.name,
                b.CurrentValue == null ? "" : b.CurrentValue.name,
                StringComparison.Ordinal);
            return n != 0
                ? n
                : string.Compare(
                    a.Sites.Count > 0 ? a.Sites[0].PropertyPath : "",
                    b.Sites.Count > 0 ? b.Sites[0].PropertyPath : "",
                    StringComparison.Ordinal);
        }

        private static void ScanComponent(Component vrcf, List<RefSite> output)
        {
            using var so = new SerializedObject(vrcf);
            SerializedProperty iter = so.GetIterator();
            if (!iter.NextVisible(enterChildren: true))
            {
                return;
            }

            do
            {
                if (iter.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                Object current = iter.objectReferenceValue;
                if (current == null)
                {
                    continue;
                }

                if (string.Equals(iter.propertyPath, "m_Script", StringComparison.Ordinal))
                {
                    continue;
                }

                output.Add(new RefSite(
                    vrcf,
                    PathUtility.GetGameObjectPath(vrcf.gameObject),
                    GetEnclosingFeatureTypeName(so, iter.propertyPath),
                    iter.propertyPath,
                    current));
            } while (iter.NextVisible(enterChildren: true));
        }

        private static string GetEnclosingFeatureTypeName(SerializedObject so, string propertyPath)
        {
            string parent = propertyPath;
            while (true)
            {
                int dot = parent.LastIndexOf('.');
                if (dot < 0)
                {
                    break;
                }

                parent = parent[..dot];
                SerializedProperty p = so.FindProperty(parent);
                if (p == null)
                {
                    continue;
                }

                if (p.propertyType == SerializedPropertyType.ManagedReference)
                {
                    return ShortenManagedReferenceTypeName(p.managedReferenceFullTypename);
                }
            }
            return "VRCFury";
        }

        private static string ShortenManagedReferenceTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return "VRCFury";
            }

            int space = fullName.LastIndexOf(' ');
            string typeName = space >= 0 ? fullName[(space + 1)..] : fullName;
            int lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
        }
    }
}
