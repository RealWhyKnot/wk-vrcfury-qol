// FbxMeshUtility.cs
//
// When a SkinnedMeshRenderer's sharedMesh is a sub-asset of an imported
// model file (FBX/OBJ/DAE/glTF/GLB), the importer owns it and any in-place
// edit is overwritten on the next re-import. Editor tools that want to
// modify a mesh must first clone it to a standalone .asset so the edit
// survives.
//
// ResolveEditableMesh detects an importer sub-asset, clones it to a
// caller-specified folder (with a caller-supplied filename suffix), and
// rewires the renderer to the clone. Both the create-asset and the
// renderer-property change are registered for Undo so Ctrl+Z restores
// everything -- the clone file is deleted from disk on undo, and the
// renderer's sharedMesh reverts to the importer sub-asset.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Utilities {

    public static class FbxMeshUtility {

        /// <summary>
        /// Outcome of a ResolveEditableMesh call. <see cref="Mesh"/> is the
        /// mesh the caller should now mutate; <see cref="WasCloned"/> tells
        /// the caller whether a new asset was created; <see cref="ClonedPath"/>
        /// is the asset path of the clone (null if no clone happened).
        /// </summary>
        public struct ResolveResult {
            public Mesh Mesh;
            public bool WasCloned;
            public string ClonedPath;
        }

        /// <summary>
        /// If <paramref name="sharedMesh"/> is a sub-asset of an imported
        /// model, clone it to <paramref name="generatedFolder"/> and rewire
        /// <paramref name="renderer"/> to the clone. Otherwise return the
        /// mesh unchanged. The generated folder is created if missing.
        /// </summary>
        /// <param name="renderer">The renderer to rewire on clone.</param>
        /// <param name="sharedMesh">The mesh to resolve.</param>
        /// <param name="cloneSuffix">Appended to the original mesh name on
        ///   the clone (e.g. "(Fixed)", "(Merged)"). Surfaces in the
        ///   Hierarchy and the generated .asset filename.</param>
        /// <param name="undoLabel">Label registered for Undo on both the
        ///   created asset and the renderer's sharedMesh change.</param>
        /// <param name="generatedFolder">Asset folder for the clone, e.g.
        ///   "Assets/AvatarQol Generated". Must live under "Assets/".</param>
        public static ResolveResult ResolveEditableMesh(
            SkinnedMeshRenderer renderer,
            Mesh sharedMesh,
            string cloneSuffix,
            string undoLabel,
            string generatedFolder) {

            var result = new ResolveResult { Mesh = sharedMesh, WasCloned = false, ClonedPath = null };
            if (sharedMesh == null) return result;

            var path = AssetDatabase.GetAssetPath(sharedMesh);
            bool isImporterSubAsset = !string.IsNullOrEmpty(path)
                && (path.EndsWith(".fbx",  System.StringComparison.OrdinalIgnoreCase)
                 || path.EndsWith(".obj",  System.StringComparison.OrdinalIgnoreCase)
                 || path.EndsWith(".dae",  System.StringComparison.OrdinalIgnoreCase)
                 || path.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase)
                 || path.EndsWith(".glb",  System.StringComparison.OrdinalIgnoreCase));
            if (!isImporterSubAsset) return result;

            EnsureFolder(generatedFolder);
            var clone = Object.Instantiate(sharedMesh);
            clone.name = sharedMesh.name + " " + cloneSuffix;
            var sanitizedSuffix = SanitizeFileName(cloneSuffix);
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{generatedFolder}/{SanitizeFileName(sharedMesh.name)}_{sanitizedSuffix}.asset");
            AssetDatabase.CreateAsset(clone, targetPath);
            Undo.RegisterCreatedObjectUndo(clone, undoLabel);

            Undo.RecordObject(renderer, undoLabel);
            renderer.sharedMesh = clone;
            EditorUtility.SetDirty(renderer);

            result.Mesh = clone;
            result.WasCloned = true;
            result.ClonedPath = targetPath;
            return result;
        }

        private static void EnsureFolder(string folder) {
            if (string.IsNullOrEmpty(folder)) return;
            if (AssetDatabase.IsValidFolder(folder)) return;
            // Walk up to the deepest existing ancestor and create the rest.
            var parts = folder.Split('/');
            if (parts.Length < 2 || parts[0] != "Assets") return;
            var current = "Assets";
            for (int i = 1; i < parts.Length; i++) {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static string SanitizeFileName(string name) {
            if (string.IsNullOrEmpty(name)) return "mesh";
            foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            // The clone suffix arrives like "(Fixed)"; brackets aren't invalid
            // on Windows but they make for ugly asset filenames. Strip.
            name = name.Replace("(", "").Replace(")", "").Trim();
            if (string.IsNullOrEmpty(name)) return "mesh";
            return name;
        }
    }
}
