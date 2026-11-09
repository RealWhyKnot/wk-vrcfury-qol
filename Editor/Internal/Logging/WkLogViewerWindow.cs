#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UmeVrcfQol.Internal.Styling;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Logging
{
    public sealed class WkLogViewerWindow : WkToolWindow, IDisposable
    {
        protected override string Title => "WhyKnot Logs";
        protected override Vector2 InitialMinSize => new(540, 360);
        protected override Vector2 PreferredSize => new(760, 560);
        protected override bool ShowScrollView => false;
        protected override string AutoSizeSignature =>
            $"{_packageIds.Length}|{_selectedTab}|{_logContent.Length}|{_searchQuery}|{_showDebug}|{_showInfo}|{_showWarn}|{_showError}";

        public static void Open()
        {
            GetWindow<WkLogViewerWindow>(utility: false, "WhyKnot Logs").Show();
        }

        private string[] _packageIds = Array.Empty<string>();
        private int _selectedTab;
        private string _logContent = "";
        private string _searchQuery = "";
        private Vector2 _scroll;
        private FileSystemWatcher? _watcher;
        private bool _showDebug = true;
        private bool _showInfo = true;
        private bool _showWarn = true;
        private bool _showError = true;

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshLoggers();
            ReloadCurrentLog();
        }

        private void OnDisable()
        {
            DisposeWatcher();
        }

        public void Dispose()
        {
            DisposeWatcher();
        }

        protected override void OnBodyGUI()
        {
            DrawTabs();
            DrawFilterRow();
            WkStyles.Divider();
            DrawBody();
        }

        private void DrawTabs()
        {
            if (_packageIds.Length == 0)
            {
                EditorGUILayout.HelpBox("No WkLogger instances are registered yet.", MessageType.Info);
                return;
            }
            GUIContent[] labels = _packageIds.Select(id => new GUIContent(DisplayNameFor(id), id)).ToArray();
            int next = WkStyles.TabBar(_selectedTab, labels);
            if (next != _selectedTab)
            {
                _selectedTab = next;
                ReloadCurrentLog();
            }
        }

        private void DrawFilterRow()
        {
            bool narrow = position.width < 640f;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        new GUIContent("Refresh", "Reload the registered loggers and current session log."),
                        EditorStyles.miniButton, GUILayout.Width(72)))
                {
                    RefreshLoggers();
                    ReloadCurrentLog();
                }
                GUILayout.Space(6);
                EditorGUILayout.LabelField("Show:", GUILayout.Width(40));
                _showDebug = GUILayout.Toggle(_showDebug, "Debug", EditorStyles.miniButtonLeft, GUILayout.Width(70));
                _showInfo = GUILayout.Toggle(_showInfo, "Info", EditorStyles.miniButtonMid, GUILayout.Width(70));
                _showWarn = GUILayout.Toggle(_showWarn, "Warn", EditorStyles.miniButtonMid, GUILayout.Width(70));
                _showError = GUILayout.Toggle(_showError, "Error", EditorStyles.miniButtonRight, GUILayout.Width(70));
                if (!narrow)
                {
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
                    WkStyles.SearchField(ref _searchQuery, width: 0);
                }
            }
            if (narrow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
                    WkStyles.SearchField(ref _searchQuery, width: 0);
                }
            }
        }

        private void DrawBody()
        {
            using var s = new EditorGUILayout.ScrollViewScope(
                _scroll, alwaysShowHorizontal: false, alwaysShowVertical: false,
                GUILayout.ExpandWidth(expand: true),
                GUILayout.ExpandHeight(expand: true));
            _scroll = s.scrollPosition;
            if (string.IsNullOrEmpty(_logContent))
            {
                EditorGUILayout.LabelField("(empty)", WkStyles.Muted);
                return;
            }
            EditorGUILayout.TextArea(FilteredContent(), WkStyles.Mono, GUILayout.ExpandHeight(expand: true));
        }

        private string FilteredContent()
        {
            if (string.IsNullOrEmpty(_logContent))
            {
                return "";
            }

            string[] lines = _logContent.Split('\n');
            var filtered = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                if (!LineMatchesLevelFilter(line))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_searchQuery) &&
                    line.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                filtered.Add(line);
            }
            return string.Join("\n", filtered);
        }

        private bool LineMatchesLevelFilter(string line)
        {
            return line.Contains("[DEBUG]", StringComparison.Ordinal)
                ? _showDebug
                : line.Contains("[INFO ]", StringComparison.Ordinal)
                    ? _showInfo
                    : line.Contains("[WARN ]", StringComparison.Ordinal)
                        ? _showWarn
                        : !line.Contains("[ERROR]", StringComparison.Ordinal) || _showError;
        }

        protected override void OnFooterGUI()
        {
            WkLogger? logger = CurrentLogger();
            using (new EditorGUI.DisabledScope(logger == null))
            {
                if (GUILayout.Button(
                        new GUIContent("Open in Explorer", "Reveal the current log file."),
                        GUILayout.Height(22), GUILayout.Width(126)))
                {
                    if (logger != null)
                    {
                        EditorUtility.RevealInFinder(logger.LogFilePath);
                    }
                }
                if (GUILayout.Button(
                        new GUIContent("Open Log Folder", "Reveal the folder containing retained log sessions."),
                        GUILayout.Height(22), GUILayout.Width(126)))
                {
                    if (logger != null)
                    {
                        EditorUtility.RevealInFinder(logger.LogDirectory);
                    }
                }
            }
            base.OnFooterGUI();
        }

        private void RefreshLoggers()
        {
            WkLogger[] all = WkLoggerRegistry.All();
            _packageIds = all.Select(l => l.PackageId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (_selectedTab >= _packageIds.Length)
            {
                _selectedTab = 0;
            }
        }

        private WkLogger? CurrentLogger()
        {
            if (_packageIds.Length == 0)
            {
                return null;
            }

            string id = _packageIds[Mathf.Clamp(_selectedTab, 0, _packageIds.Length - 1)];
            return WkLoggerRegistry.IsRegistered(id) ? WkLoggerRegistry.Get(id) : null;
        }

        private static string DisplayNameFor(string packageId)
        {
            return string.IsNullOrEmpty(packageId)
                ? "(none)"
                : WkLoggerRegistry.IsRegistered(packageId)
                    ? WkLoggerRegistry.Get(packageId).DisplayName
                    : packageId;
        }

        private void ReloadCurrentLog()
        {
            DisposeWatcher();
            WkLogger? logger = CurrentLogger();
            if (logger == null)
            {
                _logContent = "";
                return;
            }
            LoadLog(logger.LogFilePath);
            try
            {
                _watcher = new FileSystemWatcher(logger.LogDirectory, "*.log")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                _watcher.Changed += (s, e) => EditorApplication.delayCall += () => LoadLog(logger.LogFilePath);
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Debug.LogException(ex, this);
            }
        }

        private void LoadLog(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    _logContent = reader.ReadToEnd();
                    Repaint();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                _logContent = $"Could not read {path}: {ex.Message}";
                Repaint();
            }
        }

        private void DisposeWatcher()
        {
            FileSystemWatcher? watcher = _watcher;
            _watcher = null;
            if (watcher == null)
            {
                return;
            }

            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or PlatformNotSupportedException)
            {
                Debug.LogException(ex, this);
            }
        }
    }
}
