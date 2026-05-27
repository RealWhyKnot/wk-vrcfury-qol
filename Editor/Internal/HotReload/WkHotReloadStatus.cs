// WkHotReloadStatus.cs
//
// EditorWindow exposing the live state of EditorHotReload: refresh
// counter, last compile result, last 50 file events, log file path,
// and an "Open in Explorer" jump. Built in IMGUI deliberately -- the
// hot-reload view stays dependency-light so it remains useful while
// debugging editor startup and refresh issues.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Styling;

namespace UmeVrcfQol.Internal.HotReload {

    public sealed class WkHotReloadStatus : EditorWindow {

        // No [MenuItem] here -- the downstream wires its own menu path
        // (Window/WhyKnot/<DisplayName>/Hot Reload Status) from its
        // non-synced code so two synced copies of this file don't race
        // for the same menu path when both downstreams are installed.
        public static void Open() {
            var w = GetWindow<WkHotReloadStatus>(false, "Hot Reload Status");
            w.titleContent = WkStyles.TitleContent("Hot Reload Status");
            w.minSize = new Vector2(460, 360);
            w.Show();
        }

        private Vector2 _scroll;
        private Vector2 _bodyScroll;

        private void OnEnable() {
            // Keep the view fresh while it's visible. Hot-reload events
            // arrive on background-thread FSW callbacks; the static state
            // in EditorHotReload is updated under a lock, so a polling
            // repaint at 4 Hz is enough to feel live.
            EditorApplication.update += BumpRepaint;
        }

        private void OnDisable() {
            EditorApplication.update -= BumpRepaint;
        }

        private double _nextRepaint;
        private void BumpRepaint() {
            if (EditorApplication.timeSinceStartup < _nextRepaint) return;
            _nextRepaint = EditorApplication.timeSinceStartup + 0.25;
            Repaint();
        }

        private void OnGUI() {
            using var _wkTheme = WkStyles.Scope(WkTheme.WhyKnot);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))) {
                WkStyles.TitleBar("Hot Reload Status");
                WkStyles.AnimatedAccentLine();

                using (var s = new EditorGUILayout.ScrollViewScope(
                        _bodyScroll, false, false,
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true))) {
                    _bodyScroll = s.scrollPosition;
                    DrawSummary();
                    EditorGUILayout.Space();
                    DrawRecentEvents();
                }

                WkStyles.Divider();
                DrawFooter();
                WkStyles.BrandFooter();
            }
        }

        private void DrawSummary() {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField("Refresh count",     EditorHotReload.RefreshCount.ToString());
                EditorGUILayout.LabelField("Shader reimports",  EditorHotReload.ShaderReimportCount.ToString());
                EditorGUILayout.LabelField("Last compile",      EditorHotReload.LastCompileResult);
                EditorGUILayout.LabelField("Log file",          EditorHotReload.LogFilePath ?? "(not initialised)");
            }
        }

        private void DrawRecentEvents() {
            EditorGUILayout.LabelField("Recent file events (newest first)", EditorStyles.boldLabel);
            using (var s = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MinHeight(160), GUILayout.MaxHeight(220))) {
                _scroll = s.scrollPosition;
                var events = EditorHotReload.RecentEvents;
                if (events == null || events.Count == 0) {
                    EditorGUILayout.LabelField("(no events yet)", EditorStyles.miniLabel);
                    return;
                }
                // Render newest-first.
                for (int i = events.Count - 1; i >= 0; i--) {
                    var e = events[i];
                    EditorGUILayout.LabelField($"{e.When:HH:mm:ss.fff}  {e.Kind,-9}  {e.Path}", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawFooter() {
            using (new EditorGUILayout.HorizontalScope()) {
                var path = EditorHotReload.LogFilePath;
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path))) {
                    if (GUILayout.Button(
                            new GUIContent("Open Log in Explorer", "Reveal the current hot-reload log file."),
                            GUILayout.Height(22))) {
                        EditorUtility.RevealInFinder(path);
                    }
                }
                GUILayout.FlexibleSpace();
                var enabled = EditorPrefs.GetBool(EditorHotReload.HotReloadEnabledPrefsKey, true);
                var next = GUILayout.Toggle(enabled, "Watcher enabled (next launch)", GUILayout.Height(22));
                if (next != enabled) {
                    EditorPrefs.SetBool(EditorHotReload.HotReloadEnabledPrefsKey, next);
                }
            }
        }
    }
}
