// MeshUtility.cs
//
// Generic Mesh + SkinnedMeshRenderer helpers that aren't tied to a
// specific avatar tooling concern. These are the pieces every mesh-aware
// tool reinvents: bindpose -> world-space math for visualisations,
// in-memory clone helpers for non-destructive previews, and mirror-axis
// math for left/right vertex pairs.
//
// All math operates in mesh-local space until the final TransformPoint;
// callers can stay in local space when comparing two meshes that share
// the same root, and only convert to world for gizmos / handles.

using UnityEngine;

namespace UmeVrcfQol.Internal.Utilities {

    public static class MeshUtility {

        /// <summary>
        /// Convert a mesh-local vertex position into world space using the
        /// renderer's bindpose for <paramref name="bone"/>. This is the
        /// canonical path for "where is this vertex right now" when the
        /// renderer is skinned -- multiplying by the renderer's transform
        /// produces wrong answers when the SkinnedMeshRenderer's root bone
        /// has been re-parented at edit time. Bindpose math doesn't care.
        /// </summary>
        public static Vector3 BoneLocalToWorld(Matrix4x4 bindpose, Transform bone, Vector3 meshLocalVert) {
            if (bone == null) return meshLocalVert;
            var boneLocal = bindpose.MultiplyPoint3x4(meshLocalVert);
            return bone.TransformPoint(boneLocal);
        }

        /// <summary>
        /// Mirror a mesh-local vertex across the X = 0 plane of
        /// <paramref name="mirrorRoot"/> (typically the avatar's Hips bone),
        /// using <paramref name="bindpose"/> + <paramref name="bone"/> to
        /// move into and back out of world space. Returns a mesh-local
        /// position the caller can hand straight to vertex-painting code.
        /// </summary>
        public static Vector3 MirrorVertexAcrossLocalX(Matrix4x4 bindpose, Transform bone, Vector3 meshLocalVert, Transform mirrorRoot) {
            if (bone == null || mirrorRoot == null) return meshLocalVert;
            var world = BoneLocalToWorld(bindpose, bone, meshLocalVert);
            var rootLocal = mirrorRoot.InverseTransformPoint(world);
            rootLocal.x = -rootLocal.x;
            var mirroredWorld = mirrorRoot.TransformPoint(rootLocal);
            var boneLocal = bone.InverseTransformPoint(mirroredWorld);
            return bindpose.inverse.MultiplyPoint3x4(boneLocal);
        }

        /// <summary>
        /// Clone <paramref name="source"/> as a runtime-only Mesh (no asset
        /// on disk) and mark it <c>HideFlags.DontSave</c>. Used by preview
        /// pipelines that want to mutate a copy without touching the
        /// original. The caller is responsible for <c>Object.DestroyImmediate</c>
        /// when done -- DontSave-flagged objects persist across scene loads
        /// otherwise.
        /// </summary>
        public static Mesh CloneInMemory(Mesh source, string suffix = null) {
            if (source == null) return null;
            var clone = Object.Instantiate(source);
            clone.name = string.IsNullOrEmpty(suffix) ? source.name : (source.name + "_" + suffix);
            clone.hideFlags = HideFlags.DontSave;
            return clone;
        }

        /// <summary>
        /// Axis-aligned bounding box of <paramref name="mesh"/>'s vertices
        /// after each is moved into world space via its primary bone's
        /// bindpose. Useful for sanity-checking that a SkinnedMeshRenderer's
        /// authored mesh matches the avatar it's attached to -- a mismatched
        /// bind shows up as an oversized or misplaced box.
        /// </summary>
        public static Bounds ComputeBindposeBounds(Mesh mesh, Transform[] bones, Matrix4x4[] bindposes) {
            if (mesh == null || bones == null || bindposes == null) return new Bounds();
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            if (vertices.Length == 0 || weights.Length != vertices.Length) return new Bounds();

            var bounds = new Bounds();
            bool initialised = false;
            for (int i = 0; i < vertices.Length; i++) {
                int boneIdx = weights[i].boneIndex0;
                if (boneIdx < 0 || boneIdx >= bones.Length || boneIdx >= bindposes.Length) continue;
                var bone = bones[boneIdx];
                if (bone == null) continue;
                var world = BoneLocalToWorld(bindposes[boneIdx], bone, vertices[i]);
                if (!initialised) {
                    bounds = new Bounds(world, Vector3.zero);
                    initialised = true;
                } else {
                    bounds.Encapsulate(world);
                }
            }
            return bounds;
        }
    }
}
