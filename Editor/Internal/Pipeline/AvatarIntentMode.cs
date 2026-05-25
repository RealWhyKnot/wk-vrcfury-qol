// AvatarIntentMode.cs
//
// Promoted from dev.whyknot.avatar-qol's Editor/Common/AvatarIntentMode.cs.
// Tags why an avatar build hook is firing -- the same hook code paths
// usually behave slightly differently between an in-editor preview, a
// play-mode invocation, and a publish-time upload.
//
// Kept distinct from WkBuildMode (in WkAvatarPipelineTypes.cs) because
// downstream code that uses the AvatarIntent pattern hasn't migrated to
// the WkAvatarPipeline yet -- the two enums will likely converge in a
// future release.

namespace UmeVrcfQol.Internal.Pipeline {

    public enum AvatarIntentMode {
        Preview,
        PlayMode,
        Upload,
    }
}
