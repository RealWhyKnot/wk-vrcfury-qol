// WkLogger.cs
//
// Per-package file logger plus optional Unity-console mirror. Each
// WhyKnot package constructs one of these in its [InitializeOnLoad]
// static class; the constructor self-registers with WkLoggerRegistry
// so anywhere in the editor can fetch the right instance by packageId.
//
// Output goes to a machine-wide directory under
// %LocalAppData%/WhyKnot/Logs/<package-id>/ on Windows (and the
// equivalent SpecialFolder.LocalApplicationData on other platforms).
// One file per Unity launch named session-<timestamp>.log. The
// constructor culls older sessions so the directory carries at most
// MaxSessionsPerPackage files, including the new one -- with three
// WhyKnot packages and three sessions each, the user keeps a rolling
// nine log files on their machine regardless of how many Unity projects
// they switch between.
//
// Logging captures: ISO-ish timestamp, level, calling source file +
// line, calling member, and the message. Exceptions also dump the
// stack to the file. The console mirror is configurable per level so
// Debug noise can stay file-only while warnings and errors surface in
// the Console as well.
//
// Lock-protected writes are safe across threads; FileSystemWatcher
// callbacks and EditorApplication.update both end up in here from
// EditorHotReload.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Logging {

    public enum WkLogLevel {
        Debug,
        Info,
        Warning,
        Error,
    }

    public sealed class WkLogger {

        /// <summary>How many session log files we keep per package directory.</summary>
        public const int MaxSessionsPerPackage = 3;

        private const string RootFolderName = "WhyKnot";
        private const string LogsFolderName = "Logs";
        private const string SessionFilePrefix = "session-";
        private const string SessionFileExtension = ".log";

        private readonly string _packageId;
        private readonly string _displayName;
        private readonly string _packageVersion;
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _writeLock = new object();
        private readonly StringBuilder _builder = new StringBuilder(256);

        public string PackageId => _packageId;
        public string DisplayName => _displayName;
        public string LogFilePath => _logFilePath;
        public string LogDirectory => _logDirectory;

        /// <summary>Mirror Debug-level lines to UnityEngine.Debug.Log. Off by default to keep the console quiet.</summary>
        public bool MirrorDebugToConsole { get; set; } = false;

        /// <summary>Mirror Info-level lines to UnityEngine.Debug.Log. On by default -- matches the previous "[VRCF QoL] ..." bracketed-prefix convention.</summary>
        public bool MirrorInfoToConsole { get; set; } = true;

        /// <summary>Mirror Warning-level lines to UnityEngine.Debug.LogWarning.</summary>
        public bool MirrorWarningToConsole { get; set; } = true;

        /// <summary>Mirror Error-level lines to UnityEngine.Debug.LogError.</summary>
        public bool MirrorErrorToConsole { get; set; } = true;

        /// <summary>Mirror Exception logs to UnityEngine.Debug.LogException so Unity's stack-trace renderer fires.</summary>
        public bool MirrorExceptionToConsole { get; set; } = true;

        public WkLogger(string packageId, string displayName, string packageVersion) {
            if (string.IsNullOrEmpty(packageId)) throw new ArgumentException("packageId is required", nameof(packageId));
            if (string.IsNullOrEmpty(displayName)) throw new ArgumentException("displayName is required", nameof(displayName));
            _packageId = packageId;
            _displayName = displayName;
            _packageVersion = packageVersion ?? "(unknown)";

            _logDirectory = ResolveLogDirectory(_packageId);
            try {
                Directory.CreateDirectory(_logDirectory);
                CullOldSessions(_logDirectory, MaxSessionsPerPackage - 1);
            } catch (Exception ex) {
                // If we can't even prepare the directory we still want the
                // registry entry so callers don't blow up; subsequent writes
                // will be no-ops thanks to the try-catch in WriteLineToFile.
                UnityEngine.Debug.LogWarning($"[{_displayName}] WkLogger failed to prepare log directory '{_logDirectory}': {ex.Message}");
            }

            _logFilePath = Path.Combine(_logDirectory, BuildSessionFileName(DateTime.Now));
            WriteSessionHeader();
            WkLoggerRegistry.Register(this);
        }

        // ---- level entry points -------------------------------------

        public void Debug(string message,
                          [CallerMemberName] string member = "",
                          [CallerFilePath]   string file = "",
                          [CallerLineNumber] int    line = 0)
            => Write(WkLogLevel.Debug, message, member, file, line);

        public void Info(string message,
                         [CallerMemberName] string member = "",
                         [CallerFilePath]   string file = "",
                         [CallerLineNumber] int    line = 0)
            => Write(WkLogLevel.Info, message, member, file, line);

        public void Warning(string message,
                            [CallerMemberName] string member = "",
                            [CallerFilePath]   string file = "",
                            [CallerLineNumber] int    line = 0)
            => Write(WkLogLevel.Warning, message, member, file, line);

        public void Error(string message,
                          [CallerMemberName] string member = "",
                          [CallerFilePath]   string file = "",
                          [CallerLineNumber] int    line = 0)
            => Write(WkLogLevel.Error, message, member, file, line);

        public void Exception(Exception ex, string context = null,
                              [CallerMemberName] string member = "",
                              [CallerFilePath]   string file = "",
                              [CallerLineNumber] int    line = 0) {
            if (ex == null) return;
            var headline = string.IsNullOrEmpty(context)
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{context} -- {ex.GetType().Name}: {ex.Message}";
            Write(WkLogLevel.Error, headline, member, file, line);
            WriteLineToFile(ex.StackTrace ?? "(no stack trace)");
            WriteLineToFile("");  // blank line separator after stack
            if (MirrorExceptionToConsole) {
                var contextObject = WkLogContext.CurrentContextObject;
                if (contextObject != null) UnityEngine.Debug.LogException(ex, contextObject);
                else                       UnityEngine.Debug.LogException(ex);
            }
        }

        /// <summary>Write a raw line to the log file with no level/prefix. Useful for headers and separators.</summary>
        public void Raw(string line) {
            WriteLineToFile(line);
        }

        // ---- scoped operations --------------------------------------

        /// <summary>
        /// Push a <see cref="WkLogContext.Scope"/> labelled
        /// <paramref name="label"/>, write a "<label> starting" line at
        /// Debug level, and on dispose write a "<label> finished in Xms"
        /// line at <paramref name="completionLevel"/>. Usage:
        ///   using (logger.BeginTask("BoneMerger scan")) { ... }
        /// </summary>
        public IDisposable BeginTask(string label, WkLogLevel completionLevel = WkLogLevel.Info) {
            return new TaskScope(this, label, completionLevel);
        }

        /// <summary>
        /// Write a multi-line block to the file with each subsequent line
        /// indented by two spaces. Same level mirror behaviour as a single
        /// <see cref="Info"/> call but the indented lines stay file-only
        /// to avoid spamming the Unity Console.
        /// </summary>
        public void InfoBlock(string header, IEnumerable<string> lines,
                              [CallerMemberName] string member = "",
                              [CallerFilePath]   string file = "",
                              [CallerLineNumber] int    line = 0)
            => WriteBlock(WkLogLevel.Info, header, lines, member, file, line);

        public void WarningBlock(string header, IEnumerable<string> lines,
                                 [CallerMemberName] string member = "",
                                 [CallerFilePath]   string file = "",
                                 [CallerLineNumber] int    line = 0)
            => WriteBlock(WkLogLevel.Warning, header, lines, member, file, line);

        public void ErrorBlock(string header, IEnumerable<string> lines,
                               [CallerMemberName] string member = "",
                               [CallerFilePath]   string file = "",
                               [CallerLineNumber] int    line = 0)
            => WriteBlock(WkLogLevel.Error, header, lines, member, file, line);

        private void WriteBlock(WkLogLevel level, string header, IEnumerable<string> lines,
                                string member, string file, int lineNumber) {
            Write(level, header ?? "", member, file, lineNumber);
            if (lines == null) return;
            foreach (var lineText in lines) {
                if (lineText == null) continue;
                // Indent the continuation lines for readability; the prefix
                // and timestamps stay so a grep on the level tag still
                // catches them. Continuation lines do not mirror to the
                // console -- the header already did.
                WriteContinuationLine(level, "  " + lineText);
            }
        }

        private void WriteContinuationLine(WkLogLevel level, string message) {
            string formatted;
            lock (_builder) {
                _builder.Clear();
                _builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                _builder.Append(" [").Append(LevelTag(level)).Append(']');
                _builder.Append(' ').Append(message);
                formatted = _builder.ToString();
            }
            WriteLineToFile(formatted);
        }

        private sealed class TaskScope : IDisposable {
            private readonly WkLogger _logger;
            private readonly string _label;
            private readonly WkLogLevel _completionLevel;
            private readonly IDisposable _scope;
            private readonly double _startTimeRealtime;
            private bool _disposed;

            public TaskScope(WkLogger logger, string label, WkLogLevel completionLevel) {
                _logger = logger;
                _label = label ?? "task";
                _completionLevel = completionLevel;
                _scope = WkLogContext.Scope(_label);
                _startTimeRealtime = Time.realtimeSinceStartupAsDouble;
                _logger.Debug($"{_label} starting");
            }

            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                var elapsedMs = (Time.realtimeSinceStartupAsDouble - _startTimeRealtime) * 1000.0;
                var msg = $"{_label} finished in {elapsedMs:F1} ms";
                switch (_completionLevel) {
                    case WkLogLevel.Debug:   _logger.Debug(msg);   break;
                    case WkLogLevel.Warning: _logger.Warning(msg); break;
                    case WkLogLevel.Error:   _logger.Error(msg);   break;
                    default:                 _logger.Info(msg);    break;
                }
                _scope.Dispose();
            }
        }

        // ---- formatting ---------------------------------------------

        private void Write(WkLogLevel level, string message, string member, string file, int lineNumber) {
            string fileName = string.IsNullOrEmpty(file) ? null : Path.GetFileName(file);
            string source = (fileName != null && lineNumber > 0) ? $" {fileName}:{lineNumber}" : "";
            string memberTag = string.IsNullOrEmpty(member) ? "" : $" ({member})";
            string scopePrefix = WkLogContext.FormatScopePrefix();
            string formatted;
            lock (_builder) {
                _builder.Clear();
                _builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                _builder.Append(" [").Append(LevelTag(level)).Append(']');
                _builder.Append(source).Append(memberTag);
                _builder.Append(' ').Append(scopePrefix).Append(message);
                formatted = _builder.ToString();
            }
            WriteLineToFile(formatted);
            MirrorToConsole(level, message);
        }

        private static string LevelTag(WkLogLevel level) {
            switch (level) {
                case WkLogLevel.Debug:   return "DEBUG";
                case WkLogLevel.Info:    return "INFO ";
                case WkLogLevel.Warning: return "WARN ";
                case WkLogLevel.Error:   return "ERROR";
                default:                 return "?????";
            }
        }

        private void MirrorToConsole(WkLogLevel level, string message) {
            var prefixed = $"[{_displayName}] {message}";
            // Attach the current context object to the console mirror so
            // clicking the line in the Console pings the object in the
            // Hierarchy. Console mirror omits the file-line scope prefix
            // -- the prefix is for grep, not Unity's UI.
            var contextObject = WkLogContext.CurrentContextObject;
            switch (level) {
                case WkLogLevel.Debug:
                    if (!MirrorDebugToConsole) break;
                    if (contextObject != null) UnityEngine.Debug.Log(prefixed, contextObject);
                    else                       UnityEngine.Debug.Log(prefixed);
                    break;
                case WkLogLevel.Info:
                    if (!MirrorInfoToConsole) break;
                    if (contextObject != null) UnityEngine.Debug.Log(prefixed, contextObject);
                    else                       UnityEngine.Debug.Log(prefixed);
                    break;
                case WkLogLevel.Warning:
                    if (!MirrorWarningToConsole) break;
                    if (contextObject != null) UnityEngine.Debug.LogWarning(prefixed, contextObject);
                    else                       UnityEngine.Debug.LogWarning(prefixed);
                    break;
                case WkLogLevel.Error:
                    if (!MirrorErrorToConsole) break;
                    if (contextObject != null) UnityEngine.Debug.LogError(prefixed, contextObject);
                    else                       UnityEngine.Debug.LogError(prefixed);
                    break;
            }
        }

        private void WriteLineToFile(string line) {
            lock (_writeLock) {
                try {
                    using (var fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(fs)) {
                        writer.WriteLine(line);
                    }
                } catch {
                    // Never let logging throw -- if the disk is full or
                    // the file is locked, we'd rather drop a line than
                    // crash the editor.
                }
            }
        }

        private void WriteSessionHeader() {
            WriteLineToFile("================================================================");
            WriteLineToFile($"WhyKnot {_displayName} ({_packageId}) v{_packageVersion}");
            WriteLineToFile($"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            WriteLineToFile($"Unity:    {Application.unityVersion}");
            WriteLineToFile($"Platform: {Application.platform} ({SystemInfo.operatingSystem})");
            WriteLineToFile($"Project:  {ResolveProjectPath()}");
            WriteLineToFile($"Machine:  {Environment.MachineName}");
            WriteLineToFile($"User:     {Environment.UserName}");
            WriteLineToFile($"BatchMode:{Application.isBatchMode}");
            WriteLineToFile("================================================================");
        }

        private static string ResolveProjectPath() {
            try {
                return Directory.GetParent(Application.dataPath)?.FullName ?? "(unknown)";
            } catch {
                return "(unknown)";
            }
        }

        // ---- session culling ----------------------------------------

        private static string ResolveLogDirectory(string packageId) {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root)) {
                // Fall back to the Unity persistent data path, then to the
                // working directory if even that's unavailable. Either way
                // the user still gets a log; it's just less centralized.
                try { root = Application.persistentDataPath; } catch { /* ignore */ }
                if (string.IsNullOrEmpty(root)) root = Environment.CurrentDirectory;
            }
            return Path.Combine(root, RootFolderName, LogsFolderName, packageId);
        }

        private static string BuildSessionFileName(DateTime when) {
            return $"{SessionFilePrefix}{when:yyyy-MM-dd_HHmmss}{SessionFileExtension}";
        }

        /// <summary>
        /// Delete oldest session files in <paramref name="directory"/> until at
        /// most <paramref name="keep"/> remain. Called before the new session
        /// file is created so the post-create count is exactly keep + 1, which
        /// is what callers want when they pass keep = MaxSessionsPerPackage - 1.
        /// </summary>
        internal static void CullOldSessions(string directory, int keep) {
            if (keep < 0) keep = 0;
            if (!Directory.Exists(directory)) return;
            string searchPattern = SessionFilePrefix + "*" + SessionFileExtension;
            FileInfo[] all;
            try {
                all = new DirectoryInfo(directory).GetFiles(searchPattern);
            } catch {
                return;
            }
            var sorted = all.OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
            for (int i = keep; i < sorted.Length; i++) {
                try { sorted[i].Delete(); } catch { /* ignore */ }
            }
        }
    }
}
