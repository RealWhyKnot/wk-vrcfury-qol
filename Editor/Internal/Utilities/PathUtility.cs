// PathUtility.cs
//
// Generic Transform / GameObject path helpers used across the WhyKnot
// Editor tools.

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
    }
}
