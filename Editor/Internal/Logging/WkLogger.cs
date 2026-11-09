#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Logging
{
    public sealed class WkLogger : IDisposable
    {
        public const int MaxSessionsPerPackage = 3;

        private const string RootFolderName = "WhyKnot";
        private const string LogsFolderName = "Logs";
        private const string SessionFilePrefix = "session-";
        private const string SessionFileExtension = ".log";

        private readonly string _packageVersion;
        private readonly object _writeLock = new();
        private readonly StringBuilder _builder = new(256);

        private StreamWriter? _writer;
        private bool _writerOpen;
        private bool _writeFailureReported;

        public string PackageId { get; }
        public string DisplayName { get; }
        public string LogFilePath { get; }
        public string LogDirectory { get; }

        public bool MirrorDebugToConsole { get; set; }

        public bool MirrorInfoToConsole { get; set; } = true;

        public bool MirrorWarningToConsole { get; set; } = true;

        public bool MirrorErrorToConsole { get; set; } = true;

        public bool MirrorExceptionToConsole { get; set; } = true;

        public WkLogger(string packageId, string displayName, string packageVersion)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException("packageId is required", nameof(packageId));
            }

            if (string.IsNullOrEmpty(displayName))
            {
                throw new ArgumentException("displayName is required", nameof(displayName));
            }

            PackageId = packageId;
            DisplayName = displayName;
            _packageVersion = packageVersion ?? "(unknown)";

            LogDirectory = ResolveLogDirectory(PackageId);
            int cullFailures = 0;
            try
            {
                Directory.CreateDirectory(LogDirectory);
                cullFailures = CullOldSessions(LogDirectory, MaxSessionsPerPackage - 1);
            }
            catch (Exception ex) when (IsFileAccessFailure(ex))
            {
                UnityEngine.Debug.LogWarning($"[{DisplayName}] WkLogger failed to prepare log directory '{LogDirectory}': {ex.Message}");
            }

            LogFilePath = Path.Combine(LogDirectory, BuildSessionFileName(DateTime.Now));
            OpenWriter();
            WriteSessionHeader();
            if (cullFailures > 0)
            {
                WriteLineToFile($"Could not delete {cullFailures} old session log(s).");
            }

            Flush();
            WkLoggerRegistry.Register(this);
            SubscribeLifecycle();
        }

        public void Dispose()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            FlushAndCloseWriter();
        }

        private static bool IsFileAccessFailure(Exception ex)
        {
            return ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException
                or ArgumentException or ObjectDisposedException;
        }

        private void ReportWriteFailure(Exception ex)
        {
            if (_writeFailureReported)
            {
                return;
            }

            _writeFailureReported = true;
            UnityEngine.Debug.LogWarning($"[{DisplayName}] WkLogger could not write to '{LogFilePath}': {ex.Message}");
        }

        private void OpenWriter()
        {
            lock (_writeLock)
            {
                if (_writerOpen)
                {
                    return;
                }

                try
                {
                    var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(stream) { AutoFlush = false };
                    _writerOpen = true;
                }
                catch (Exception ex) when (IsFileAccessFailure(ex))
                {
                    _writer = null;
                    _writerOpen = false;
                    ReportWriteFailure(ex);
                }
            }
        }

        private void FlushAndCloseWriter()
        {
            lock (_writeLock)
            {
                StreamWriter? writer = _writer;
                bool wasOpen = _writerOpen;
                _writer = null;
                _writerOpen = false;
                if (!wasOpen || writer == null)
                {
                    return;
                }

                try
                {
                    writer.Dispose();
                }
                catch (Exception ex) when (IsFileAccessFailure(ex))
                {
                    ReportWriteFailure(ex);
                }
            }
        }

        private void SubscribeLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnBeforeAssemblyReload()
        {
            FlushAndCloseWriter();
        }

        private void OnEditorQuitting()
        {
            FlushAndCloseWriter();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    FlushAndCloseWriter();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    OpenWriter();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                case PlayModeStateChange.ExitingPlayMode:
                default:
                    break;
            }
        }

        public void Debug(string message,
                          [CallerMemberName] string member = "",
                          [CallerFilePath] string file = "",
                          [CallerLineNumber] int line = 0)
        {
            Write(WkLogLevel.Debug, message, member, file, line);
        }

        public void Info(string message,
                         [CallerMemberName] string member = "",
                         [CallerFilePath] string file = "",
                         [CallerLineNumber] int line = 0)
        {
            Write(WkLogLevel.Info, message, member, file, line);
        }

        public void Warning(string message,
                            [CallerMemberName] string member = "",
                            [CallerFilePath] string file = "",
                            [CallerLineNumber] int line = 0)
        {
            Write(WkLogLevel.Warning, message, member, file, line);
        }

        public void Error(string message,
                          [CallerMemberName] string member = "",
                          [CallerFilePath] string file = "",
                          [CallerLineNumber] int line = 0)
        {
            Write(WkLogLevel.Error, message, member, file, line);
        }

        public void Exception(Exception ex, string? context = null,
                              [CallerMemberName] string member = "",
                              [CallerFilePath] string file = "",
                              [CallerLineNumber] int line = 0)
        {
            if (ex == null)
            {
                return;
            }

            string headline = string.IsNullOrEmpty(context)
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{context} -- {ex.GetType().Name}: {ex.Message}";
            Write(WkLogLevel.Error, headline, member, file, line);
            WriteLineToFile(ex.StackTrace ?? "(no stack trace)");
            WriteLineToFile("", flushImmediately: true);
            if (MirrorExceptionToConsole)
            {
                UnityEngine.Object? contextObject = WkLogContext.CurrentContextObject;
                if (contextObject != null)
                {
                    UnityEngine.Debug.LogException(ex, contextObject);
                }
                else
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        public void Raw(string line)
        {
            WriteLineToFile(line);
        }

        public IDisposable BeginTask(string label, WkLogLevel completionLevel = WkLogLevel.Info)
        {
            return new TaskScope(this, label, completionLevel);
        }

        public void InfoBlock(string header, IEnumerable<string> lines,
                              [CallerMemberName] string member = "",
                              [CallerFilePath] string file = "",
                              [CallerLineNumber] int line = 0)
        {
            WriteBlock(WkLogLevel.Info, header, lines, member, file, line);
        }

        public void WarningBlock(string header, IEnumerable<string> lines,
                                 [CallerMemberName] string member = "",
                                 [CallerFilePath] string file = "",
                                 [CallerLineNumber] int line = 0)
        {
            WriteBlock(WkLogLevel.Warning, header, lines, member, file, line);
        }

        public void ErrorBlock(string header, IEnumerable<string> lines,
                               [CallerMemberName] string member = "",
                               [CallerFilePath] string file = "",
                               [CallerLineNumber] int line = 0)
        {
            WriteBlock(WkLogLevel.Error, header, lines, member, file, line);
        }

        private void WriteBlock(WkLogLevel level, string header, IEnumerable<string> lines,
                                string member, string file, int lineNumber)
        {
            Write(level, header ?? "", member, file, lineNumber);
            if (lines == null)
            {
                return;
            }

            foreach (string lineText in lines)
            {
                if (lineText == null)
                {
                    continue;
                }

                WriteContinuationLine(level, "  " + lineText);
            }
        }

        private void WriteContinuationLine(WkLogLevel level, string message)
        {
            string formatted;
            lock (_builder)
            {
                _builder.Clear();
                _builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                _builder.Append(" [").Append(LevelTag(level)).Append(']');
                _builder.Append(' ').Append(message);
                formatted = _builder.ToString();
            }
            WriteLineToFile(formatted);
        }

        private sealed class TaskScope : IDisposable
        {
            private readonly WkLogger _logger;
            private readonly string _label;
            private readonly WkLogLevel _completionLevel;
            private readonly IDisposable _scope;
            private readonly double _startTimeRealtime;
            private bool _disposed;

            public TaskScope(WkLogger logger, string label, WkLogLevel completionLevel)
            {
                _logger = logger;
                _label = label ?? "task";
                _completionLevel = completionLevel;
                _scope = WkLogContext.Scope(_label);
                _startTimeRealtime = Time.realtimeSinceStartupAsDouble;
                _logger.Debug($"{_label} starting");
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                double elapsedMs = (Time.realtimeSinceStartupAsDouble - _startTimeRealtime) * 1000.0;
                string msg = $"{_label} finished in {elapsedMs:F1} ms";
                switch (_completionLevel)
                {
                    case WkLogLevel.Debug: _logger.Debug(msg); break;
                    case WkLogLevel.Warning: _logger.Warning(msg); break;
                    case WkLogLevel.Error: _logger.Error(msg); break;
                    case WkLogLevel.Info:
                    default: _logger.Info(msg); break;
                }
                _scope.Dispose();
            }
        }

        private void Write(WkLogLevel level, string message, string member, string file, int lineNumber)
        {
            string? fileName = string.IsNullOrEmpty(file) ? null : Path.GetFileName(file);
            string source = (fileName != null && lineNumber > 0) ? $" {fileName}:{lineNumber}" : "";
            string memberTag = string.IsNullOrEmpty(member) ? "" : $" ({member})";
            string scopePrefix = WkLogContext.FormatScopePrefix();
            string formatted;
            lock (_builder)
            {
                _builder.Clear();
                _builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                _builder.Append(" [").Append(LevelTag(level)).Append(']');
                _builder.Append(source).Append(memberTag);
                _builder.Append(' ').Append(scopePrefix).Append(message);
                formatted = _builder.ToString();
            }
            bool flushNow = level is WkLogLevel.Warning or WkLogLevel.Error;
            WriteLineToFile(formatted, flushNow);
            MirrorToConsole(level, message);
        }

        private static string LevelTag(WkLogLevel level)
        {
            return level switch
            {
                WkLogLevel.Debug => "DEBUG",
                WkLogLevel.Info => "INFO ",
                WkLogLevel.Warning => "WARN ",
                WkLogLevel.Error => "ERROR",
                _ => "?????",
            };
        }

        private bool IsMirrored(WkLogLevel level)
        {
            return level switch
            {
                WkLogLevel.Debug => MirrorDebugToConsole,
                WkLogLevel.Info => MirrorInfoToConsole,
                WkLogLevel.Warning => MirrorWarningToConsole,
                WkLogLevel.Error => MirrorErrorToConsole,
                _ => false,
            };
        }

        private void MirrorToConsole(WkLogLevel level, string message)
        {
            if (!IsMirrored(level))
            {
                return;
            }

            string prefixed = $"[{DisplayName}] {message}";
            UnityEngine.Object? contextObject = WkLogContext.CurrentContextObject;
            switch (level)
            {
                case WkLogLevel.Warning:
                    UnityEngine.Debug.LogWarning(prefixed, contextObject);
                    break;
                case WkLogLevel.Error:
                    UnityEngine.Debug.LogError(prefixed, contextObject);
                    break;
                case WkLogLevel.Debug:
                case WkLogLevel.Info:
                default:
                    UnityEngine.Debug.Log(prefixed, contextObject);
                    break;
            }
        }

        private void WriteLineToFile(string line)
        {
            WriteLineToFile(line, flushImmediately: false);
        }

        private void WriteLineToFile(string line, bool flushImmediately)
        {
            lock (_writeLock)
            {
                try
                {
                    if (_writerOpen && _writer != null)
                    {
                        _writer.WriteLine(line);
                        if (flushImmediately)
                        {
                            _writer.Flush();
                        }

                        return;
                    }
                    using var fs = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(fs);
                    writer.WriteLine(line);
                }
                catch (Exception ex) when (IsFileAccessFailure(ex))
                {
                    ReportWriteFailure(ex);
                }
            }
        }

        public void Flush()
        {
            lock (_writeLock)
            {
                if (!_writerOpen || _writer == null)
                {
                    return;
                }

                try
                {
                    _writer.Flush();
                }
                catch (Exception ex) when (IsFileAccessFailure(ex))
                {
                    ReportWriteFailure(ex);
                }
            }
        }

        private void WriteSessionHeader()
        {
            WriteLineToFile("================================================================");
            WriteLineToFile($"WhyKnot {DisplayName} ({PackageId}) v{_packageVersion}");
            WriteLineToFile($"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            WriteLineToFile($"Unity:    {Application.unityVersion}");
            WriteLineToFile($"Platform: {Application.platform} ({SystemInfo.operatingSystem})");
            WriteLineToFile($"Project:  {ResolveProjectPath()}");
            WriteLineToFile($"Machine:  {Environment.MachineName}");
            WriteLineToFile($"User:     {Environment.UserName}");
            WriteLineToFile($"BatchMode:{Application.isBatchMode}");
            WriteLineToFile("================================================================");
        }

        private static string ResolveProjectPath()
        {
            try
            {
                return Directory.GetParent(Application.dataPath)?.FullName ?? "(unknown)";
            }
            catch (Exception ex) when (ex is UnityException || IsFileAccessFailure(ex))
            {
                return "(unknown)";
            }
        }

        private static string ResolveLogDirectory(string packageId)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root))
            {
                try
                {
                    root = Application.persistentDataPath;
                }
                catch (UnityException)
                {
                    root = "";
                }

                if (string.IsNullOrEmpty(root))
                {
                    root = Environment.CurrentDirectory;
                }
            }
            return Path.Combine(root, RootFolderName, LogsFolderName, packageId);
        }

        private static string BuildSessionFileName(DateTime when)
        {
            return $"{SessionFilePrefix}{when:yyyy-MM-dd_HHmmss}{SessionFileExtension}";
        }

        internal static int CullOldSessions(string directory, int keep)
        {
            if (keep < 0)
            {
                keep = 0;
            }

            if (!Directory.Exists(directory))
            {
                return 0;
            }

            string searchPattern = SessionFilePrefix + "*" + SessionFileExtension;
            FileInfo[] all;
            try
            {
                all = new DirectoryInfo(directory).GetFiles(searchPattern);
            }
            catch (Exception ex) when (IsFileAccessFailure(ex))
            {
                return 0;
            }
            FileInfo[] sorted = all.OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
            int failures = 0;
            for (int i = keep; i < sorted.Length; i++)
            {
                try
                {
                    sorted[i].Delete();
                }
                catch (Exception ex) when (IsFileAccessFailure(ex))
                {
                    failures++;
                }
            }

            return failures;
        }
    }
}
