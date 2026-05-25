// WkAac.cs
//
// Public facade for the animator-builder. Returns an IWkAnimatorBuilder
// backed by the in-house implementation in WkAacImpl.cs. The name is
// historical -- the surface mirrors AnimatorAsCode's fluent shape but
// the actual code is wk-owned to avoid a third-party VPM dependency.
//
// Example usage:
//   using var scope = new WkGeneratedAssetScope(WkGeneratedAssetTier.Temporary, "dev.whyknot.avatar-qol", "Outfits");
//   var b = WkAac.For("Outfits", controller, scope);
//   b.NewLayer("Outfit/Shirt").DefaultState("Off").State("On", offClip).Build();
//   b.Build();

using System;
using UnityEditor.Animations;
using UnityEngine;
using UmeVrcfQol.Internal.Pipeline;

namespace UmeVrcfQol.Internal.Animators {

    public static class WkAac {

        public static IWkAnimatorBuilder For(string systemName, AnimatorController controller, WkGeneratedAssetScope scope) {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (scope == null)      throw new ArgumentNullException(nameof(scope));
            return new WkAnimatorBuilderImpl(systemName, controller, scope);
        }

        /// <summary>
        /// Convenience: construct a fresh AnimatorController under the
        /// scope's asset folder, then return a builder over it. The
        /// caller saves the controller (it's already in AssetDatabase)
        /// and references it from the avatar.
        /// </summary>
        public static IWkAnimatorBuilder NewController(string systemName, WkGeneratedAssetScope scope) {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            var controller = new AnimatorController { name = systemName };
            var path = scope.SaveAsset(controller, systemName);
            // Reload through AssetDatabase so subsequent SaveAssets-as-AddObjectToAsset work.
            var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            return For(systemName, loaded ?? controller, scope);
        }
    }
}
