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
// When this package's package.json version changes, bump the Version
// constant below so the session header reports the right value.

using UnityEditor;
using WhyKnot.Core.Logging;

namespace UmeVrcfQol {

    [InitializeOnLoad]
    public static class VrcfQolLogger {

        public const string PackageId = "dev.whyknot.vrcfury-qol";
        public const string DisplayName = "VRCFury QoL";
        public const string Version = "1.1.0-beta.3";

        public static readonly WkLogger Instance = new WkLogger(PackageId, DisplayName, Version);

        static VrcfQolLogger() {
            // The field initializer above runs before this body. The
            // [InitializeOnLoad] anchor is on this static ctor so Unity
            // forces the type to load on Editor startup.
        }
    }
}
