// PathUtility.cs
//
// Generic Transform / GameObject path helpers used across the WhyKnot
// Editor tools.

using System.Collections.Generic;
using UnityEngine;

namespace UmeVrcfQol.Internal.Utilities {

    public static class PathUtility {

        /// <summary>
        /// Slash-joined hierarchy path from the scene root down to this
        /// GameObject. Returns "(null)" if the argument is null.
        /// </summary>
        public static string GetGameObjectPath(GameObject go) {
            if (go == null) return "(null)";
            var t = go.transform;
            var path = t.name;
            while (t.parent != null) {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// Slash-joined path from <paramref name="from"/> down to
        /// <paramref name="descendant"/>. Returns null if
        /// <paramref name="descendant"/> is not actually nested under
        /// <paramref name="from"/>, or if either argument is null. When
        /// <paramref name="descendant"/> equals <paramref name="from"/> the
        /// result is the empty string, matching AnimationClip / serialized
        /// "relative-path" conventions.
        /// </summary>
        public static string GetRelativePath(Transform from, Transform descendant) {
            if (from == null || descendant == null) return null;
            if (descendant == from) return string.Empty;
            var parts = new Stack<string>();
            var cursor = descendant;
            while (cursor != null && cursor != from) {
                parts.Push(cursor.name);
                cursor = cursor.parent;
            }
            if (cursor != from) return null;   // descendant is not under from
            return string.Join("/", parts.ToArray());
        }

        /// <summary>GameObject overload of <see cref="GetRelativePath(Transform, Transform)"/>.</summary>
        public static string GetRelativePath(GameObject from, GameObject descendant) {
            if (from == null || descendant == null) return null;
            return GetRelativePath(from.transform, descendant.transform);
        }
    }
}
