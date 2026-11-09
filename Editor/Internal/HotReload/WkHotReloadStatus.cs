#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using UmeVrcfQol.Internal.Styling;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.HotReload
{
    public sealed class WkHotReloadStatus : EditorWindow
    {
        public static void Open()
        {
            WkHotReloadStatus w = GetWindow<WkHotReloadStatus>(utility: false, "Hot Reload Status");
            w.titleContent = WkStyles.TitleContent("Hot Reload Status");
            w.minSize = new Vector2(460, 360);
            w.Show();
        }

        private Vector2 _scroll;
        private Vector2 _bodyScroll;
        private string? _autoSizeSignature;

        private void OnEnable()
        {
            EditorApplication.update += BumpRepaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= BumpRepaint;
        }

        private double _nextRepaint;
        private void BumpRepaint()
        {
            if (EditorApplication.timeSinceStartup < _nextRepaint)
            {
                return;
            }

            _nextRepaint = EditorApplication.timeSinceStartup + 0.25;
            Repaint();
        }

        private void OnGUI()
        {
            using IDisposable themeScope = WkStyles.Scope(WkTheme.WhyKnot);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(expand: true), GUILayout.ExpandHeight(expand: true)))
            {
                WkStyles.TitleBar("Hot Reload Status");

                using (var s = new EditorGUILayout.ScrollViewScope(
                        _bodyScroll, alwaysShowHorizontal: false, alwaysShowVertical: false,
                        GUILayout.ExpandWidth(expand: true),
                        GUILayout.ExpandHeight(expand: true)))
                {
                    _bodyScroll = s.scrollPosition;
                    DrawSummary();
                    EditorGUILayout.Space();
                    DrawRecentEvents();
                }

                WkStyles.Divider();
                DrawFooter();
                WkStyles.BrandFooter();
            }
            RequestAutoSize();
        }

        private static void DrawSummary()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Refresh count", EditorHotReload.RefreshCount.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Shader reimports", EditorHotReload.ShaderReimportCount.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Last compile", EditorHotReload.LastCompileResult);
                EditorGUILayout.LabelField("Log file", EditorHotReload.LogFilePath ?? "(not initialised)");
            }
        }

        private void DrawRecentEvents()
        {
            EditorGUILayout.LabelField("Recent file events (newest first)", EditorStyles.boldLabel);
            using var s = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MinHeight(160), GUILayout.MaxHeight(220));
            _scroll = s.scrollPosition;
            IReadOnlyList<EditorHotReload.RecentEvent> events = EditorHotReload.RecentEvents;
            if (events.Count == 0)
            {
                EditorGUILayout.LabelField("(no events yet)", EditorStyles.miniLabel);
                return;
            }
            for (int i = events.Count - 1; i >= 0; i--)
            {
                EditorHotReload.RecentEvent e = events[i];
                EditorGUILayout.LabelField($"{e.When:HH:mm:ss.fff}  {e.Kind,-9}  {e.Path}", EditorStyles.miniLabel);
            }
        }

        private static void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string path = EditorHotReload.LogFilePath ?? "";
                using (new EditorGUI.DisabledScope(path.Length == 0))
                {
                    if (GUILayout.Button(
                            new GUIContent("Open Log in Explorer", "Reveal the current hot-reload log file."),
                            GUILayout.Height(22)))
                    {
                        EditorUtility.RevealInFinder(path);
                    }
                }
                GUILayout.FlexibleSpace();
                bool enabled = EditorPrefs.GetBool(EditorHotReload.HotReloadEnabledPrefsKey, defaultValue: true);
                bool next = GUILayout.Toggle(enabled, "Watcher enabled (next launch)", GUILayout.Height(22));
                if (next != enabled)
                {
                    EditorPrefs.SetBool(EditorHotReload.HotReloadEnabledPrefsKey, next);
                }
            }
        }

        private void RequestAutoSize()
        {
            IReadOnlyList<EditorHotReload.RecentEvent> events = EditorHotReload.RecentEvents;
            int count = events.Count;
            string signature = $"{count}|{EditorHotReload.RefreshCount}|{EditorHotReload.ShaderReimportCount}|{EditorHotReload.LastCompileResult}|{EditorHotReload.LogFilePath}";
            var preferred = new Vector2(620f, 280f + WkStyles.CappedListHeight(count, 18f, 120f, 220f));
            WkStyles.AutoSizeWindow(
                this,
                ref _autoSizeSignature,
                signature,
                new Vector2(460f, 360f),
                preferred,
                new Vector2(760f, 680f));
        }
    }
}
