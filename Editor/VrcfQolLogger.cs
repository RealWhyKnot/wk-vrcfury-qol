#nullable enable

using UmeVrcfQol.Internal.Logging;
using UnityEditor;

namespace UmeVrcfQol
{
    [InitializeOnLoad]
    public static class VrcfQolLogger
    {
        public const string PackageId = "dev.whyknot.wk-vrcfury-qol";
        public const string DisplayName = "VRCFury QoL";

        public static readonly string Version = ResolveVersion();

        public static readonly WkLogger Instance = new(PackageId, DisplayName, Version);

        static VrcfQolLogger()
        {
        }

        private static string ResolveVersion()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VrcfQolLogger).Assembly);
            return info != null && !string.IsNullOrEmpty(info.version) ? info.version : "unknown";
        }
    }
}
