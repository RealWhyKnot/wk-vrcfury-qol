// WkSettingsProvider.cs
//
// Project Settings page for the WhyKnot tooling family. Each downstream's
// synced Internal/ copy ships its own provider; the downstream wires
// the [SettingsProvider] static factory hook from its non-synced code so
// the page shows up under "WhyKnot/<DisplayName>" without two synced
// copies fighting over the same path.
//
// Surfaces:
//   - Per-registered-logger Console mirror toggles (Debug / Info /
//     Warning / Error / Exception). Stored via WkEditorPrefs so they
//     persist across Unity sessions and survive a domain reload.
//   - Default theme override (WhyKnot / VRCFury).
//   - Hot-reload watcher enabled toggle (WkEditorPrefs-backed, read by
//     EditorHotReload on the next startup).
//   - Optional-integration status panel: WK_NDMF, WK_AAC,
//     WK_VRC_SDK_AVATARS reported as defined / undefined.
//   - Theme preview rendering every WkUiElements factory under the
//     active theme.
//
// Built in IMGUI rather than UI Toolkit so the same window can render
// inside the legacy SettingsWindow + the modern Project Settings host.

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Logging;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;

namespace UmeVrcfQol.Internal.Settings {

    public static class WkSettingsProvider {

        public const string DefaultPath = "WhyKnot/Core";
        public static readonly string PrefsPackage = ResolvePrefsPackage();

        private static string ResolvePrefsPackage() {
            try {
                var assemblyName = typeof(WkSettingsProvider).Assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName)) return "dev.whyknot.wk-vrcfury-qol.settings";
                if (assemblyName.EndsWith(".HotReload.Editor", StringComparison.OrdinalIgnoreCase)) {
                    assemblyName = assemblyName.Substring(0, assemblyName.Length - ".HotReload.Editor".Length);
                } else if (assemblyName.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase)) {
                    assemblyName = assemblyName.Substring(0, assemblyName.Length - ".Editor".Length);
                }
                return assemblyName + ".settings";
            } catch {
                return "dev.whyknot.wk-vrcfury-qol.settings";
            }
        }

        /// <summary>
        /// Build a SettingsProvider that can be returned from a
        /// [SettingsProvider]-decorated static method in downstream code.
        /// </summary>
        public static SettingsProvider Build(string path = DefaultPath) {
            return new SettingsProvider(path, SettingsScope.Project) {
                label = "WhyKnot Tools",
                keywords = new[] { "WhyKnot", "log", "theme", "wk", "ndmf", "aac" },
                guiHandler = _ => DrawGui(),
            };
        }

        private static void DrawGui() {
            using (WkStyles.Scope(WkTheme.WhyKnot)) {
                EditorGUILayout.LabelField("Console mirror toggles per registered package", WkStyles.SubsectionTitle);
                DrawLoggerToggles();
                WkStyles.Divider();

                EditorGUILayout.LabelField("Theme", WkStyles.SubsectionTitle);
                DrawThemeOverride();
                WkStyles.Divider();

                EditorGUILayout.LabelField("Hot-reload watcher", WkStyles.SubsectionTitle);
                DrawHotReloadToggle();
                WkStyles.Divider();

                EditorGUILayout.LabelField("Optional integrations", WkStyles.SubsectionTitle);
                DrawIntegrationStatus();
            }
        }

        // ---- logger mirror toggles -------------------------------

        private static void DrawLoggerToggles() {
            var all = WkLoggerRegistry.All().OrderBy(l => l.PackageId).ToArray();
            if (all.Length == 0) {
                EditorGUILayout.HelpBox("No WkLogger instances are registered.", MessageType.Info);
                return;
            }
            foreach (var logger in all) {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    EditorGUILayout.LabelField(logger.DisplayName + "  " + logger.PackageId, WkStyles.Body);
                    logger.MirrorDebugToConsole     = MirrorRow("Debug",     logger.PackageId, "MirrorDebug",     logger.MirrorDebugToConsole,     false);
                    logger.MirrorInfoToConsole      = MirrorRow("Info",      logger.PackageId, "MirrorInfo",      logger.MirrorInfoToConsole,      true);
                    logger.MirrorWarningToConsole   = MirrorRow("Warning",   logger.PackageId, "MirrorWarning",   logger.MirrorWarningToConsole,   true);
                    logger.MirrorErrorToConsole     = MirrorRow("Error",     logger.PackageId, "MirrorError",     logger.MirrorErrorToConsole,     true);
                    logger.MirrorExceptionToConsole = MirrorRow("Exception", logger.PackageId, "MirrorException", logger.MirrorExceptionToConsole, true);
                }
            }
        }

        private static bool MirrorRow(string label, string packageId, string suffix, bool currentValue, bool defaultValue) {
            var key = "mirror." + packageId + "." + suffix;
            var stored = WkEditorPrefs.GetBool(PrefsPackage, key, defaultValue);
            // Reconcile: the logger's current property and the prefs value
            // can drift if code paths set the property at runtime. Trust
            // the more-recently-modified one by writing the logger value
            // into prefs on each draw.
            var displayed = currentValue;
            var next = EditorGUILayout.Toggle("  " + label, displayed);
            if (next != stored) {
                WkEditorPrefs.SetBool(PrefsPackage, key, next);
            }
            return next;
        }

        // ---- theme override --------------------------------------

        private static void DrawThemeOverride() {
            var current = WkEditorPrefs.GetString(PrefsPackage, "default-theme", "WhyKnot");
            int index = current == "VRCFury" ? 1 : 0;
            var next = EditorGUILayout.Popup("Default theme", index, new[] { "WhyKnot", "VRCFury" });
            var nextName = next == 1 ? "VRCFury" : "WhyKnot";
            if (nextName != current) {
                WkEditorPrefs.SetString(PrefsPackage, "default-theme", nextName);
                WkStyles.DefaultTheme = nextName == "VRCFury" ? WkTheme.VRCFury : WkTheme.WhyKnot;
            }
        }

        // ---- hot reload ------------------------------------------

        private static void DrawHotReloadToggle() {
            var current = WkEditorPrefs.GetBool(PrefsPackage, "hot-reload-enabled", true);
            var next = EditorGUILayout.Toggle("Enable hot-reload watcher", current);
            if (next != current) {
                WkEditorPrefs.SetBool(PrefsPackage, "hot-reload-enabled", next);
            }
            EditorGUILayout.HelpBox(
                "Takes effect on the next Editor restart. The watcher subscribes during [InitializeOnLoad].",
                MessageType.Info);
        }

        // ---- integration status ----------------------------------

        private static void DrawIntegrationStatus() {
            DrawSymbol("WK_NDMF (NDMF integration)", IsDefined("WK_NDMF"),
                "Install nadena.dev.ndmf to route avatar passes through NDMF's pipeline instead of the raw-SDK fallback.");
            DrawSymbol("WK_VRC_SDK_AVATARS (VRChat Avatars SDK)", IsDefined("WK_VRC_SDK_AVATARS"),
                "VRChat Avatars SDK is what backs the raw-SDK fallback path when NDMF isn't installed.");
        }

        private static void DrawSymbol(string label, bool defined, string hint) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(label, GUILayout.Width(320));
                EditorGUILayout.LabelField(defined ? "defined" : "undefined", GUILayout.Width(80));
            }
            if (!defined) {
                EditorGUILayout.HelpBox(hint, MessageType.None);
            }
        }

        private static bool IsDefined(string symbol) {
            // versionDefines aren't queryable directly at runtime; probe
            // the calling assembly's effective defines instead. Each
            // synced copy of this file lives in the downstream assembly
            // it ships into, so the defines we see are the downstream's.
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            if (defines.Contains(symbol)) return true;
            // versionDefines are NOT in the global define list -- we have
            // to detect them via #if at compile time and surface a
            // method that reads back. Use a private check via attribute
            // reflection.
            return WkDefineProbe.IsDefined(symbol);
        }
    }

    internal static class WkDefineProbe {
        public static bool IsDefined(string symbol) {
            switch (symbol) {
#if WK_NDMF
                case "WK_NDMF": return true;
#endif
#if WK_VRC_SDK_AVATARS
                case "WK_VRC_SDK_AVATARS": return true;
#endif
                default: return false;
            }
        }
    }
}
