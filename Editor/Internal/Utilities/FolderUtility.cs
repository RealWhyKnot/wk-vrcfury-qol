// FolderUtility.cs
//
// AssetDatabase folder creation that walks the path and creates each
// missing segment, instead of failing on the first missing parent. Unity
// only documents the single-segment AssetDatabase.CreateFolder(parent, leaf)
// form; the recursive variant gets reinvented in every tool that writes
// generated assets.

using UnityEditor;

namespace UmeVrcfQol.Internal.Utilities {

    public static class FolderUtility {

        /// <summary>
        /// Ensure <paramref name="assetPath"/> exists as a folder under
        /// Assets/. Creates each missing segment from the deepest existing
        /// ancestor down. Returns the canonical asset path with forward
        /// slashes; returns null/empty unchanged. Paths that do not start
        /// with "Assets" are returned unchanged without any filesystem
        /// effect -- this helper is for editor-time AssetDatabase folders,
        /// not arbitrary filesystem paths.
        /// </summary>
        public static string EnsureFolder(string assetPath) {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath)) return assetPath;

            var parts = assetPath.Split('/');
            if (parts.Length < 2 || parts[0] != "Assets") return assetPath;

            var current = "Assets";
            for (int i = 1; i < parts.Length; i++) {
                if (string.IsNullOrEmpty(parts[i])) continue;
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
            return current;
        }
    }
}
