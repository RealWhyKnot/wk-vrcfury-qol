#nullable enable

using UmeVrcfQol.Internal.HotReload;
using UmeVrcfQol.Internal.Logging;
using UmeVrcfQol.Internal.Settings;
using UnityEditor;

namespace UmeVrcfQol
{
    internal static class VrcfQolMenus
    {
        [MenuItem("Window/WhyKnot/VRCFury QoL/Logs")]
        public static void OpenLogViewer()
        {
            WkLogViewerWindow.Open();
        }

        [MenuItem("Window/WhyKnot/VRCFury QoL/Hot Reload Status")]
        public static void OpenHotReloadStatus()
        {
            WkHotReloadStatus.Open();
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettings()
        {
            return WkSettingsProvider.Build("WhyKnot/VRCFury QoL");
        }
    }
}
