// VrcfQolMenus.cs
//
// Wires the bundled log viewer and Project Settings page into this
// package's menu paths. WkLogViewerWindow and WkSettingsProvider ship
// in Editor/Internal/ but register no menu / settings attribute of
// their own; the wiring here gives the package its
// Window/WhyKnot/VRCFury QoL/Logs menu item and its
// WhyKnot/VRCFury QoL Project Settings page.

using UnityEditor;
using UmeVrcfQol.Internal.HotReload;
using UmeVrcfQol.Internal.Logging;
using UmeVrcfQol.Internal.Settings;

namespace UmeVrcfQol {

    internal static class VrcfQolMenus {

        [MenuItem("Window/WhyKnot/VRCFury QoL/Logs")]
        public static void OpenLogViewer() => WkLogViewerWindow.Open();

        [MenuItem("Window/WhyKnot/VRCFury QoL/Hot Reload Status")]
        public static void OpenHotReloadStatus() => WkHotReloadStatus.Open();

        [SettingsProvider]
        public static SettingsProvider CreateSettings() => WkSettingsProvider.Build("WhyKnot/VRCFury QoL");
    }
}
