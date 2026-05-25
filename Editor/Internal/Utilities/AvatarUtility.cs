// AvatarUtility.cs
//
// Helpers for resolving "the avatar root" from any component nested under
// it, plus a small set of convenience lookups every avatar tool needs.
// The Animator-based heuristic is the dominant lookup in this stack but
// the additional helpers absorb the GetComponentsInChildren<...>(true)
// + null-filter + de-dup boilerplate that drifts across tools.
//
// IsAvatarRoot probes for VRC's avatar descriptor via reflection so this
// utility stays inside the SDK-free wk-core main assembly; downstream
// packages that already reference the VRC SDK can still call it without
// pulling SDK types into wk-core.

using System.Collections.Generic;
using UnityEngine;

namespace UmeVrcfQol.Internal.Utilities {

    public static class AvatarUtility {

        /// <summary>
        /// Walk up the parent chain from <paramref name="component"/> looking
        /// for an Animator; if none is found, return the scene-root GameObject
        /// the component is nested under. Inactive parents are searched.
        /// Returns null only when <paramref name="component"/> itself is null.
        /// </summary>
        public static GameObject FindAvatarRoot(Component component) {
            if (component == null) return null;
            var animator = component.GetComponentInParent<Animator>(true);
            if (animator != null) return animator.gameObject;
            var t = component.transform;
            while (t.parent != null) t = t.parent;
            return t.gameObject;
        }

        /// <summary>
        /// Walk up to the hierarchy root from <paramref name="descendant"/>,
        /// ignoring Animator presence. Returns the descendant itself when
        /// it has no parent. Returns null only when the argument is null.
        /// </summary>
        public static Transform TopLevel(Transform descendant) {
            if (descendant == null) return null;
            var cursor = descendant;
            while (cursor.parent != null) cursor = cursor.parent;
            return cursor;
        }

        /// <summary>
        /// All SkinnedMeshRenderers underneath <paramref name="avatarRoot"/>,
        /// including those on inactive GameObjects by default. Returns an
        /// empty enumerable on null input.
        /// </summary>
        public static IEnumerable<SkinnedMeshRenderer> GetSkinnedMeshes(GameObject avatarRoot, bool includeInactive = true) {
            if (avatarRoot == null) yield break;
            var found = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive);
            foreach (var smr in found) {
                if (smr != null) yield return smr;
            }
        }

        /// <summary>
        /// First Animator found by walking <paramref name="avatarRoot"/>'s
        /// own components then its descendants. Returns null on null input
        /// or when no Animator exists.
        /// </summary>
        public static Animator GetAvatarAnimator(GameObject avatarRoot) {
            if (avatarRoot == null) return null;
            return avatarRoot.GetComponentInChildren<Animator>(true);
        }

        /// <summary>
        /// True when <paramref name="candidate"/> looks like an avatar root:
        /// it carries a VRCAvatarDescriptor component (resolved via runtime
        /// reflection so wk-core does not take a compile-time dependency on
        /// the VRC SDK), or it has an Animator on the same GameObject and
        /// no parent.
        /// </summary>
        public static bool IsAvatarRoot(GameObject candidate) {
            if (candidate == null) return false;

            // Reflection probe for VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.
            // Cache the resolved Type once -- TypeCache.GetTypesDerivedFrom
            // is fast but a static-field cache avoids the lookup per call.
            EnsureDescriptorTypeProbed();
            if (_descriptorType != null) {
                var component = candidate.GetComponent(_descriptorType);
                if (component != null) return true;
            }

            // Fallback: Animator on the root with no parent. Matches the
            // common case for raw-Unity avatars or test fixtures.
            return candidate.transform.parent == null && candidate.GetComponent<Animator>() != null;
        }

        private static System.Type _descriptorType;
        private static bool _descriptorProbed;

        private static void EnsureDescriptorTypeProbed() {
            if (_descriptorProbed) return;
            _descriptorProbed = true;
            const string typeName = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
                var t = asm.GetType(typeName, false);
                if (t != null) { _descriptorType = t; return; }
            }
        }
    }
}
