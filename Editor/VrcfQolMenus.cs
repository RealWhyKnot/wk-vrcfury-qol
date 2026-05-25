// VrcfQolMenus.cs
//
// Wires the wk-core 1.2.0 log viewer and Project Settings page into
// per-downstream menu paths. WkLogViewerWindow and WkSettingsProvider
// both ship in the bundled Editor/Internal/ tree but deliberately
// register no menu / settings attribute of their own -- if they did,
// each downstream's synced copy would race for the same menu path.
// Doing the wiring here gives this package its own Window/WhyKnot/VRCFury QoL/Logs
// menu item and its own WhyKnot/VRCFury QoL Project Settings page.

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
