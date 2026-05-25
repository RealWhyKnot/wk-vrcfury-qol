// WkAvatarPipeline.cs
//
// Public facade for pass registration. Callers invoke
//   WkAvatarPipeline.Register&lt;BoneMergerPass, BoneMergerSession&gt;();
// from an [InitializeOnLoadMethod]. Whether the pass runs through NDMF
// or the raw SDK depends on which versionDefine symbol is set on the
// declaring assembly:
//
//   WK_NDMF defined         -> NDMF Plugin&lt;T&gt; bridge (WkAvatarPipelineNdmf.cs)
//   WK_VRC_SDK_AVATARS only -> IVRCSDKPreprocessAvatarCallback fallback (WkAvatarPipelineFallback.cs)
//   neither                 -> Register throws NotSupportedException
//
// The branching happens at compile time -- the active implementation file
// is selected by the #if directives in WkAvatarPipelineNdmf.cs /
// WkAvatarPipelineFallback.cs.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UmeVrcfQol.Internal.Pipeline {

    public static class WkAvatarPipeline {

        private static readonly List<IWkAvatarPass> _registered = new List<IWkAvatarPass>();

        /// <summary>Snapshot of every registered pass. Order is registration order.</summary>
        public static IReadOnlyList<IWkAvatarPass> Registered => _registered;

        /// <summary>
        /// Register a pass type with the pipeline. The pass class must be
        /// concrete and parameterless-constructible. Idempotent: registering
        /// the same type twice is a no-op.
        /// </summary>
        public static void Register<TPass, TSession>()
            where TPass : WkAvatarPass<TSession>, new()
            where TSession : class {

            // Use a prototype instance to read DisplayName / Phase /
            // FallbackCallbackOrder. The bridge implementations create a
            // fresh instance per Execute call.
            var prototype = new TPass();
            foreach (var existing in _registered) {
                if (existing.GetType() == prototype.GetType()) return;
            }
            _registered.Add(prototype);

            try {
                RegisterImpl<TPass, TSession>(prototype);
            } catch (Exception ex) {
                _registered.Remove(prototype);
                Debug.LogException(ex);
                throw;
            }
        }

        // ---- impl wiring ----------------------------------------------

        private static void RegisterImpl<TPass, TSession>(WkAvatarPass<TSession> prototype)
            where TPass : WkAvatarPass<TSession>, new()
            where TSession : class {

#if WK_NDMF
            WkAvatarPipelineNdmfBridge.Register<TPass, TSession>(prototype);
#elif WK_VRC_SDK_AVATARS
            WkAvatarPipelineFallback.Register<TPass, TSession>(prototype);
#else
            throw new NotSupportedException(
                "WkAvatarPipeline.Register requires either NDMF (nadena.dev.ndmf) " +
                "or the VRChat Avatars SDK to be installed. Install one in this " +
                "Unity project and add WK_NDMF or WK_VRC_SDK_AVATARS to this " +
                "assembly's asmdef versionDefines.");
#endif
        }
    }
}
