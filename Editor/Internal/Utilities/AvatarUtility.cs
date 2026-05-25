// AvatarUtility.cs
//
// Helpers for resolving "the avatar root" from any component nested under
// it. Tools that operate on avatars almost always need this and the
// inline GetComponentInParent<Animator>() lookups drift apart over time
// when each tool reinvents the fallback path.

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
    }
}
