#nullable enable

using System;
using System.Linq;
using UmeVrcfQol.Internal.Logging;
using UmeVrcfQol.Internal.Styling;
using UmeVrcfQol.Internal.Utilities;
using UnityEditor;

namespace UmeVrcfQol.Internal.Settings
{
    public static class WkSettingsProvider
    {
        public const string DefaultPath = "WhyKnot/VRCFury QoL";
        public static readonly string PrefsPackage = ResolvePrefsPackage();
        private static readonly string[] ThemeNames = { "WhyKnot", "VRCFury" };

        private static string ResolvePrefsPackage()
        {
            string? assemblyName = typeof(WkSettingsProvider).Assembly.GetName().Name;
            if (assemblyName == null || assemblyName.Length == 0)
            {
                return "dev.whyknot.wk-vrcfury-qol.settings";
            }

            if (assemblyName.EndsWith(".HotReload.Editor", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = assemblyName[..^".HotReload.Editor".Length];
            }
            else if (assemblyName.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = assemblyName[..^".Editor".Length];
            }

            return assemblyName + ".settings";
        }

        public static SettingsProvider Build(string path = DefaultPath)
        {
            return new SettingsProvider(path, SettingsScope.Project)
            {
                label = "WhyKnot Tools",
                keywords = new[] { "WhyKnot", "log", "theme", "wk" },
                guiHandler = _ => DrawGui(),
            };
        }

        private static void DrawGui()
        {
            using (WkStyles.Scope(WkTheme.WhyKnot))
            {
                EditorGUILayout.LabelField("Console mirror toggles per registered package", WkStyles.SubsectionTitle);
                DrawLoggerToggles();
                WkStyles.Divider();

                EditorGUILayout.LabelField("Theme", WkStyles.SubsectionTitle);
                DrawThemeOverride();
                WkStyles.Divider();

                EditorGUILayout.LabelField("Hot-reload watcher", WkStyles.SubsectionTitle);
                DrawHotReloadToggle();
            }
        }

        private static void DrawLoggerToggles()
        {
            WkLogger[] all = WkLoggerRegistry.All().OrderBy(l => l.PackageId, StringComparer.Ordinal).ToArray();
            if (all.Length == 0)
            {
                EditorGUILayout.HelpBox("No WkLogger instances are registered.", MessageType.Info);
                return;
            }
            foreach (WkLogger? logger in all)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(logger.DisplayName + "  " + logger.PackageId, WkStyles.Body);
                    logger.MirrorDebugToConsole = MirrorRow("Debug", logger.PackageId, "MirrorDebug", logger.MirrorDebugToConsole, defaultValue: false);
                    logger.MirrorInfoToConsole = MirrorRow("Info", logger.PackageId, "MirrorInfo", logger.MirrorInfoToConsole, defaultValue: true);
                    logger.MirrorWarningToConsole = MirrorRow("Warning", logger.PackageId, "MirrorWarning", logger.MirrorWarningToConsole, defaultValue: true);
                    logger.MirrorErrorToConsole = MirrorRow("Error", logger.PackageId, "MirrorError", logger.MirrorErrorToConsole, defaultValue: true);
                    logger.MirrorExceptionToConsole = MirrorRow("Exception", logger.PackageId, "MirrorException", logger.MirrorExceptionToConsole, defaultValue: true);
                }
            }
        }

        private static bool MirrorRow(string label, string packageId, string suffix, bool currentValue, bool defaultValue)
        {
            string key = "mirror." + packageId + "." + suffix;
            bool stored = WkEditorPrefs.GetBool(PrefsPackage, key, defaultValue);
            bool displayed = currentValue;
            bool next = EditorGUILayout.Toggle("  " + label, displayed);
            if (next != stored)
            {
                WkEditorPrefs.SetBool(PrefsPackage, key, next);
            }
            return next;
        }

        private static void DrawThemeOverride()
        {
            string current = WkEditorPrefs.GetString(PrefsPackage, "default-theme", "WhyKnot");
            int index = string.Equals(current, "VRCFury", StringComparison.Ordinal) ? 1 : 0;
            int next = EditorGUILayout.Popup("Default theme", index, ThemeNames);
            string nextName = next == 1 ? "VRCFury" : "WhyKnot";
            if (!string.Equals(nextName, current, StringComparison.Ordinal))
            {
                WkEditorPrefs.SetString(PrefsPackage, "default-theme", nextName);
                WkStyles.DefaultTheme = string.Equals(nextName, "VRCFury", StringComparison.Ordinal) ? WkTheme.VRCFury : WkTheme.WhyKnot;
            }
        }

        private static void DrawHotReloadToggle()
        {
            bool current = WkEditorPrefs.GetBool(PrefsPackage, "hot-reload-enabled", fallback: true);
            bool next = EditorGUILayout.Toggle("Enable hot-reload watcher", current);
            if (next != current)
            {
                WkEditorPrefs.SetBool(PrefsPackage, "hot-reload-enabled", next);
            }
            EditorGUILayout.HelpBox(
                "Takes effect on the next Editor restart. The watcher subscribes during [InitializeOnLoad].",
                MessageType.Info);
        }
    }
}
