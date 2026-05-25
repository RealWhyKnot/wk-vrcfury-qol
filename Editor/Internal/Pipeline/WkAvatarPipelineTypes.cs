// WkAvatarPipelineTypes.cs
//
// Public surface for the build-pipeline abstraction. Everything in this
// file is reference-clean -- no NDMF, no VRC SDK, no AAC types. The
// implementation files (WkAvatarPipelineNdmf.cs, WkAvatarPipelineFallback.cs)
// translate between this surface and the underlying library when the
// matching versionDefine symbol is set.
//
// Phase mapping mirrors NDMF's BuildPhase so subclasses can declare their
// place in the pipeline using semantic phase names instead of raw integer
// callback orders. When NDMF isn't installed, the fallback uses
// FallbackCallbackOrder as the raw IOrderedCallback.callbackOrder; passes
// in the same phase get a phase-shifted base order so phase ordering is
// approximated even without NDMF's sequencer.

using System;
using UnityEngine;

namespace UmeVrcfQol.Internal.Pipeline {

    /// <summary>
    /// Phase a <see cref="WkAvatarPass{TSession}"/> wants to run in.
    /// Mirrors NDMF's BuildPhase. Lower-numbered phases run earlier.
    /// </summary>
    public enum WkBuildPhase {
        Resolving,
        Generating,
        Transforming,
        Optimizing,
    }

    /// <summary>
    /// Why the pass is running. Upload = SDK preprocess; PlayMode = on
    /// entering play mode; Preview = explicit preview invocation.
    /// </summary>
    public enum WkBuildMode {
        Upload,
        PlayMode,
        Preview,
    }

    /// <summary>
    /// Context handed to a pass. Wraps the avatar root + phase + mode plus
    /// (when NDMF-backed) the underlying NDMF BuildContext. Callers pull
    /// the avatar root out and operate on it; the underlying NDMF context
    /// is exposed as <see cref="object"/> so this class can compile in
    /// projects without NDMF installed.
    /// </summary>
    public sealed class WkBuildContext {

        public GameObject AvatarRootObject { get; }
        public WkBuildPhase Phase { get; }
        public WkBuildMode Mode { get; }

        /// <summary>
        /// NDMF's <c>BuildContext</c> when the pipeline runs via the NDMF
        /// bridge; null on the raw-SDK fallback path. Cast to
        /// <c>nadena.dev.ndmf.BuildContext</c> inside <c>#if WK_NDMF</c>
        /// blocks if you need direct NDMF integration -- the cast is safe
        /// because the field is only set by the NDMF bridge.
        /// </summary>
        public object UnderlyingNdmfContext { get; }

        public WkBuildContext(GameObject avatarRoot, WkBuildPhase phase, WkBuildMode mode, object underlyingNdmfContext = null) {
            AvatarRootObject = avatarRoot;
            Phase = phase;
            Mode = mode;
            UnderlyingNdmfContext = underlyingNdmfContext;
        }
    }

    /// <summary>
    /// Non-generic marker for any avatar pass registered with
    /// <see cref="WkAvatarPipeline"/>. Lets the pipeline plumbing keep a
    /// type-erased list of registered passes.
    /// </summary>
    public interface IWkAvatarPass {
        string DisplayName { get; }
        WkBuildPhase Phase { get; }
        int FallbackCallbackOrder { get; }
        void Execute(WkBuildContext ctx);
    }

    /// <summary>
    /// Abstract base for an avatar pass. Subclasses declare a phase, a
    /// display name, an optional fallback callback order, and provide
    /// CreateSession / RunOnAvatar / DisposeSession overrides. Execute
    /// is the unified entry point both the NDMF bridge and the raw-SDK
    /// fallback call.
    ///
    /// Example:
    ///   internal sealed class BoneMergerPass : WkAvatarPass&lt;BoneMergerSession&gt; {
    ///       public override string DisplayName => "Bone Merger";
    ///       public override WkBuildPhase Phase => WkBuildPhase.Transforming;
    ///       protected override BoneMergerSession CreateSession(WkBuildContext ctx) => new(ctx.AvatarRootObject);
    ///       protected override void RunOnAvatar(WkBuildContext ctx, BoneMergerSession s) => s.Apply();
    ///       protected override void DisposeSession(BoneMergerSession s) => s.Dispose();
    ///   }
    /// </summary>
    public abstract class WkAvatarPass<TSession> : IWkAvatarPass where TSession : class {

        public abstract string DisplayName { get; }
        public abstract WkBuildPhase Phase { get; }

        /// <summary>
        /// IOrderedCallback.callbackOrder value to use when the pipeline
        /// runs via the raw-SDK fallback. Defaults to a phase-shifted
        /// base so passes in earlier phases run earlier without each
        /// caller having to remember the bands.
        /// </summary>
        public virtual int FallbackCallbackOrder => DefaultFallbackOrder(Phase);

        protected abstract TSession CreateSession(WkBuildContext ctx);
        protected abstract void RunOnAvatar(WkBuildContext ctx, TSession session);
        protected virtual void DisposeSession(TSession session) { }

        /// <summary>
        /// Unified entry point. Creates a session, runs the pass, disposes
        /// the session regardless of whether the body throws. Both the
        /// NDMF bridge and the raw-SDK fallback call this method.
        /// </summary>
        public void Execute(WkBuildContext ctx) {
            TSession session = null;
            try {
                session = CreateSession(ctx);
                RunOnAvatar(ctx, session);
            } finally {
                if (session != null) DisposeSession(session);
            }
        }

        /// <summary>
        /// Phase-shifted default callback order so the raw-SDK fallback
        /// roughly preserves phase ordering. Passes that need a specific
        /// integer override FallbackCallbackOrder directly.
        /// </summary>
        protected static int DefaultFallbackOrder(WkBuildPhase phase) {
            switch (phase) {
                case WkBuildPhase.Resolving:    return -10000;
                case WkBuildPhase.Generating:   return -5000;
                case WkBuildPhase.Transforming: return 0;
                case WkBuildPhase.Optimizing:   return 5000;
                default:                        return 0;
            }
        }
    }
}
