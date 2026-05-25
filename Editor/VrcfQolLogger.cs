// VrcfQolLogger.cs
//
// vrcfury-qol's registered WkLogger instance. Every diagnostic line in
// this package goes through VrcfQolLogger.Instance so the session file
// at %LocalAppData%/WhyKnot/Logs/dev.whyknot.vrcfury-qol/session-*.log
// captures the same content the user sees in the Unity Console -- and
// quite a bit more besides, since Debug-level lines stay file-only.
//
// The [InitializeOnLoad] attribute guarantees the static ctor runs when
// Unity loads this assembly. The WkLogger ctor self-registers with
// WkLoggerRegistry, so anywhere else in the package can also use
// WkLoggerRegistry.Get(PackageId) without caring about load order.
//
// Version resolves from UnityEditor.PackageManager.PackageInfo.FindForAssembly
// so package.json stays the single source of truth -- nothing else to
// bump on release. CI also enforces this via .github/workflows/version-guard.yml.

using UnityEditor;
using UnityEditor.PackageManager;
using WhyKnot.Core.Logging;

namespace UmeVrcfQol {

    [InitializeOnLoad]
    public static class VrcfQolLogger {

        public const string PackageId = "dev.whyknot.vrcfury-qol";
        public const string DisplayName = "VRCFury QoL";

        public static readonly string Version = ResolveVersion();

        public static readonly WkLogger Instance = new WkLogger(PackageId, DisplayName, Version);

        static VrcfQolLogger() {
            // Field initializers above already created and registered the
            // logger. The [InitializeOnLoad] anchor on this static ctor
            // tells Unity to force-load the type at Editor startup.
        }

        private static string ResolveVersion() {
            // FindForAssembly returns null when the package is dropped loose
            // under Assets/ instead of installed via VPM. Fall back to a
            // sentinel rather than throwing -- a missing version label in
            // the log header is recoverable; an Editor-init exception is not.
            var info = PackageInfo.FindForAssembly(typeof(VrcfQolLogger).Assembly);
            return info != null && !string.IsNullOrEmpty(info.version) ? info.version : "unknown";
        }
    }
}
