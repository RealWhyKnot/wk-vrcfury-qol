// WkHotReloadStatus.cs
//
// EditorWindow exposing the live state of EditorHotReload: refresh
// counter, last compile result, last 50 file events, log file path,
// and an "Open in Explorer" jump. Built in IMGUI deliberately -- the
// HotReload assembly has no reference to wk-core's main Editor
// assembly (that isolation is the whole point of the separate
// asmdef), so we can't reach for WkStyles primitives here. Keep this
// view dependency-free.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.HotReload {

    public sealed class WkHotReloadStatus : EditorWindow {

        [MenuItem("Window/WhyKnot/Hot Reload Status")]
        public static void Open() {
            var w = GetWindow<WkHotReloadStatus>(false, "Hot Reload Status");
            w.minSize = new Vector2(460, 360);
            w.Show();
        }

        private Vector2 _scroll;

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
            EditorGUILayout.LabelField("Hot Reload Status", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawSummary();
            EditorGUILayout.Space();
            DrawRecentEvents();
            EditorGUILayout.Space();
            DrawFooter();
        }

        private void DrawSummary() {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField("Refresh count",     EditorHotReload.RefreshCount.ToString());
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
                    if (GUILayout.Button("Open Log in Explorer", GUILayout.Height(22))) {
                        EditorUtility.RevealInFinder(path);
                    }
                }
                GUILayout.FlexibleSpace();
                var enabled = EditorPrefs.GetBool("dev.whyknot.core.settings.hot-reload-enabled", true);
                var next = GUILayout.Toggle(enabled, "Watcher enabled (next launch)", GUILayout.Height(22));
                if (next != enabled) {
                    EditorPrefs.SetBool("dev.whyknot.core.settings.hot-reload-enabled", next);
                }
            }
        }
    }
}
