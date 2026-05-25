// WkAvatarPipelineNdmf.cs
//
// NDMF-backed bridge for WkAvatarPipeline. Compiles in only when the
// asmdef's versionDefines sets WK_NDMF (i.e. when nadena.dev.ndmf is
// installed). Translates a wk Phase into NDMF's BuildPhase, wraps each
// registered WkAvatarPass<TSession> in an NDMF Plugin<T> + Pass<T> pair
// so the pass shows up in NDMF's pipeline alongside Modular Avatar /
// AvatarOptimizer / any other NDMF-using package.
//
// Each registration creates a NEW closed generic Plugin<T> type tagged
// with the TPass type parameter so multiple registered passes don't
// collide on the static Lazy<T>.Instance NDMF's Plugin<T> uses. The
// generic instantiation does the type-tagging automatically.

#if WK_NDMF
using System;
using nadena.dev.ndmf;
using UnityEngine;

namespace UmeVrcfQol.Internal.Pipeline {

    internal static class WkAvatarPipelineNdmfBridge {

        public static void Register<TPass, TSession>(WkAvatarPass<TSession> prototype)
            where TPass : WkAvatarPass<TSession>, new()
            where TSession : class {
            // Touching .Instance triggers NDMF's lazy plugin construction
            // which calls Configure() which registers the pass with the
            // build pipeline. Idempotent -- repeated Register calls just
            // return the same instance.
            _ = WkNdmfBridgePlugin<TPass, TSession>.Instance;
        }

        internal static BuildPhase MapPhase(WkBuildPhase phase) {
            switch (phase) {
                case WkBuildPhase.Resolving:    return BuildPhase.Resolving;
                case WkBuildPhase.Generating:   return BuildPhase.Generating;
                case WkBuildPhase.Transforming: return BuildPhase.Transforming;
                case WkBuildPhase.Optimizing:   return BuildPhase.Optimizing;
                default:                        return BuildPhase.Transforming;
            }
        }
    }

    /// <summary>
    /// NDMF plugin wrapper for a single WkAvatarPass type. The generic
    /// type parameters make each registration its own closed Plugin<>
    /// type so NDMF's static instance machinery doesn't collapse them.
    /// </summary>
    internal sealed class WkNdmfBridgePlugin<TPass, TSession>
        : Plugin<WkNdmfBridgePlugin<TPass, TSession>>
        where TPass : WkAvatarPass<TSession>, new()
        where TSession : class {

        private static readonly TPass _prototype = new TPass();

        public override string QualifiedName => typeof(TPass).FullName;
        public override string DisplayName   => _prototype.DisplayName;

        protected override void Configure() {
            InPhase(WkAvatarPipelineNdmfBridge.MapPhase(_prototype.Phase))
                .Run(typeof(TPass).FullName, ctx => {
                    var pass = new TPass();
                    var wkCtx = new WkBuildContext(
                        ctx.AvatarRootObject,
                        _prototype.Phase,
                        WkBuildMode.Upload,
                        underlyingNdmfContext: ctx);
                    pass.Execute(wkCtx);
                });
        }
    }
}
#endif
