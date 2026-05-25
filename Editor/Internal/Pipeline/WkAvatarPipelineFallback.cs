// WkAvatarPipelineFallback.cs
//
// Raw-SDK fallback when NDMF is not installed. Wraps registered
// WkAvatarPass<TSession> types in four phase-bucket
// IVRCSDKPreprocessAvatarCallback implementations -- one per
// WkBuildPhase, each at a hardcoded callbackOrder that matches the
// phase's default. Passes within a bucket run in FallbackCallbackOrder
// order.
//
// Limitation vs the NDMF path: cross-plugin ordering (BeforePlugin /
// AfterPlugin) is not available. Callers needing precise interleaving
// with Modular Avatar / AvatarOptimizer / etc. should install NDMF so
// the WK_NDMF code path becomes active. Same FallbackCallbackOrder
// integer wins inside a phase.

#if !WK_NDMF && WK_VRC_SDK_AVATARS
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace UmeVrcfQol.Internal.Pipeline {

    internal static class WkAvatarPipelineFallback {

        public static void Register<TPass, TSession>(WkAvatarPass<TSession> prototype)
            where TPass : WkAvatarPass<TSession>, new()
            where TSession : class {
            // Registration is just appending to WkAvatarPipeline.Registered;
            // the four bucket classes below enumerate that list at preprocess
            // time. No per-pass class generation needed.
        }

        internal static bool DispatchPhase(WkBuildPhase phase, GameObject avatarRoot) {
            var passes = WkAvatarPipeline.Registered
                .Where(p => p.Phase == phase)
                .OrderBy(p => p.FallbackCallbackOrder)
                .ToList();
            foreach (var pass in passes) {
                try {
                    var ctx = new WkBuildContext(avatarRoot, phase, WkBuildMode.Upload);
                    pass.Execute(ctx);
                } catch (Exception ex) {
                    Debug.LogException(ex);
                    return false;
                }
            }
            return true;
        }
    }

    // ---- Phase buckets ----------------------------------------------
    // Four non-generic IVRCSDKPreprocessAvatarCallback implementations
    // so VRC SDK's assembly-scan picks them up. callbackOrder values
    // mirror WkAvatarPass<TSession>.DefaultFallbackOrder so phase order
    // is preserved against other packages.

    internal sealed class WkPipelineFallbackResolving : IVRCSDKPreprocessAvatarCallback {
        public int callbackOrder => -10000;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
            => WkAvatarPipelineFallback.DispatchPhase(WkBuildPhase.Resolving, avatarGameObject);
    }

    internal sealed class WkPipelineFallbackGenerating : IVRCSDKPreprocessAvatarCallback {
        public int callbackOrder => -5000;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
            => WkAvatarPipelineFallback.DispatchPhase(WkBuildPhase.Generating, avatarGameObject);
    }

    internal sealed class WkPipelineFallbackTransforming : IVRCSDKPreprocessAvatarCallback {
        public int callbackOrder => 0;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
            => WkAvatarPipelineFallback.DispatchPhase(WkBuildPhase.Transforming, avatarGameObject);
    }

    internal sealed class WkPipelineFallbackOptimizing : IVRCSDKPreprocessAvatarCallback {
        public int callbackOrder => 5000;
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
            => WkAvatarPipelineFallback.DispatchPhase(WkBuildPhase.Optimizing, avatarGameObject);
    }
}
#endif
