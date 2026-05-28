// WkLogViewerWindow.cs
//
// EditorWindow for browsing per-package session logs. Each downstream's
// synced Internal/ copy carries its own WkLogViewerWindow; the
// downstream wires a [MenuItem] in its non-synced code so the window
// shows up under "Window/WhyKnot/<DisplayName>/Logs" without two
// synced copies fighting over the same menu path.
//
// Window features:
//   - Tab per WkLogger registered with WkLoggerRegistry
//   - Live tail of the current session log via FileSystemWatcher
//   - Level filter chips (Debug / Info / Warning / Error)
//   - Free-text search field
//   - Open log file / open log folder buttons
//   - Previous-session dropdown when the package has rotated logs
//
// Built in IMGUI for simplicity -- the UI Toolkit primitives are
// available, but a single text panel + filter row doesn't justify
// the boilerplate of bringing in the USS theme stylesheets here.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal;
using UmeVrcfQol.Internal.Styling;

namespace UmeVrcfQol.Internal.Logging {

    public sealed class WkLogViewerWindow : WkToolWindow {

        protected override string Title => "WhyKnot Logs";
        protected override Vector2 InitialMinSize => new Vector2(540, 360);
        protected override Vector2 PreferredSize => new Vector2(760, 560);
        protected override bool ShowScrollView => false;
        protected override string AutoSizeSignature =>
            $"{_packageIds?.Length ?? 0}|{_selectedTab}|{_logContent?.Length ?? 0}|{_searchQuery}|{_showDebug}|{_showInfo}|{_showWarn}|{_showError}";

        /// <summary>Open or focus the viewer. Call from downstream [MenuItem] hooks.</summary>
        public static WkLogViewerWindow Open() {
            var window = GetWindow<WkLogViewerWindow>(false, "WhyKnot Logs");
            window.Show();   // inherited EditorWindow.Show() to make the window visible
            return window;
        }

        private string[] _packageIds;
        private int _selectedTab;
        private string _logContent = "";
        private string _searchQuery = "";
        private Vector2 _scroll;
        private FileSystemWatcher _watcher;
        private bool _showDebug = true;
        private bool _showInfo = true;
        private bool _showWarn = true;
        private bool _showError = true;

        protected override void OnEnable() {
            base.OnEnable();
            RefreshLoggers();
            ReloadCurrentLog();
        }

        private void OnDisable() {
            DisposeWatcher();
        }

        protected override void OnBodyGUI() {
            DrawTabs();
            DrawFilterRow();
            WkStyles.Divider();
            DrawBody();
        }

        private void DrawTabs() {
            if (_packageIds == null || _packageIds.Length == 0) {
                EditorGUILayout.HelpBox("No WkLogger instances are registered yet.", MessageType.Info);
                return;
            }
            var labels = _packageIds.Select(id => new GUIContent(DisplayNameFor(id), id)).ToArray();
            var next = WkStyles.TabBar(_selectedTab, labels);
            if (next != _selectedTab) {
                _selectedTab = next;
                ReloadCurrentLog();
            }
        }

        private void DrawFilterRow() {
            var narrow = position.width < 640f;
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(
                        new GUIContent("Refresh", "Reload the registered loggers and current session log."),
                        EditorStyles.miniButton, GUILayout.Width(72))) {
                    RefreshLoggers();
                    ReloadCurrentLog();
                }
                GUILayout.Space(6);
                EditorGUILayout.LabelField("Show:", GUILayout.Width(40));
                _showDebug = GUILayout.Toggle(_showDebug, "Debug", EditorStyles.miniButtonLeft, GUILayout.Width(70));
                _showInfo  = GUILayout.Toggle(_showInfo,  "Info",  EditorStyles.miniButtonMid,  GUILayout.Width(70));
                _showWarn  = GUILayout.Toggle(_showWarn,  "Warn",  EditorStyles.miniButtonMid,  GUILayout.Width(70));
                _showError = GUILayout.Toggle(_showError, "Error", EditorStyles.miniButtonRight, GUILayout.Width(70));
                if (!narrow) {
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
                    WkStyles.SearchField(ref _searchQuery, "Search", width: 0);
                }
            }
            if (narrow) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
                    WkStyles.SearchField(ref _searchQuery, "Search", width: 0);
                }
            }
        }

        // ---- body --------------------------------------------------

        private void DrawBody() {
            using (var s = new EditorGUILayout.ScrollViewScope(
                    _scroll, false, false,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true))) {
                _scroll = s.scrollPosition;
                if (string.IsNullOrEmpty(_logContent)) {
                    EditorGUILayout.LabelField("(empty)", WkStyles.Muted);
                    return;
                }
                EditorGUILayout.TextArea(FilteredContent(), WkStyles.Mono, GUILayout.ExpandHeight(true));
            }
        }

        private string FilteredContent() {
            if (string.IsNullOrEmpty(_logContent)) return "";
            var lines = _logContent.Split('\n');
            var filtered = new List<string>(lines.Length);
            foreach (var line in lines) {
                if (!LineMatchesLevelFilter(line)) continue;
                if (!string.IsNullOrEmpty(_searchQuery) &&
                    line.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;
                filtered.Add(line);
            }
            return string.Join("\n", filtered);
        }

        private bool LineMatchesLevelFilter(string line) {
            if (line.Contains("[DEBUG]")) return _showDebug;
            if (line.Contains("[INFO ]")) return _showInfo;
            if (line.Contains("[WARN ]")) return _showWarn;
            if (line.Contains("[ERROR]")) return _showError;
            return true;   // header / blank lines / stack traces show always
        }

        // ---- footer ------------------------------------------------

        protected override void OnFooterGUI() {
            var logger = CurrentLogger();
            using (new EditorGUI.DisabledScope(logger == null)) {
                if (GUILayout.Button(
                        new GUIContent("Open in Explorer", "Reveal the current log file."),
                        GUILayout.Height(22), GUILayout.Width(126))) {
                    if (logger != null) EditorUtility.RevealInFinder(logger.LogFilePath);
                }
                if (GUILayout.Button(
                        new GUIContent("Open Log Folder", "Reveal the folder containing retained log sessions."),
                        GUILayout.Height(22), GUILayout.Width(126))) {
                    if (logger != null) EditorUtility.RevealInFinder(logger.LogDirectory);
                }
            }
            base.OnFooterGUI();
        }

        // ---- data -------------------------------------------------

        private void RefreshLoggers() {
            var all = WkLoggerRegistry.All();
            _packageIds = all?.Select(l => l.PackageId).OrderBy(id => id).ToArray() ?? Array.Empty<string>();
            if (_selectedTab >= _packageIds.Length) _selectedTab = 0;
        }

        private WkLogger CurrentLogger() {
            if (_packageIds == null || _packageIds.Length == 0) return null;
            var id = _packageIds[Mathf.Clamp(_selectedTab, 0, _packageIds.Length - 1)];
            return WkLoggerRegistry.IsRegistered(id) ? WkLoggerRegistry.Get(id) : null;
        }

        private string DisplayNameFor(string packageId) {
            if (string.IsNullOrEmpty(packageId)) return "(none)";
            return WkLoggerRegistry.IsRegistered(packageId)
                ? WkLoggerRegistry.Get(packageId).DisplayName
                : packageId;
        }

        private void ReloadCurrentLog() {
            DisposeWatcher();
            var logger = CurrentLogger();
            if (logger == null) {
                _logContent = "";
                return;
            }
            LoadLog(logger.LogFilePath);
            try {
                _watcher = new FileSystemWatcher(logger.LogDirectory, "*.log");
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                _watcher.Changed += (s, e) => EditorApplication.delayCall += () => LoadLog(logger.LogFilePath);
                _watcher.EnableRaisingEvents = true;
            } catch {
                // FileSystemWatcher can fail on certain network shares;
                // viewer still works manually via the Refresh button.
            }
        }

        private void LoadLog(string path) {
            try {
                if (File.Exists(path)) {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                        using (var reader = new StreamReader(stream)) {
                            _logContent = reader.ReadToEnd();
                        }
                    }
                    Repaint();
                }
            } catch {
                // Locked or unreadable -- leave previous content visible.
            }
        }

        private void DisposeWatcher() {
            if (_watcher != null) {
                try { _watcher.EnableRaisingEvents = false; _watcher.Dispose(); } catch { /* ignore */ }
                _watcher = null;
            }
        }
    }
}
