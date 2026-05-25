// EditorHotReload.cs
//
// Lives in its OWN assembly (`dev.whyknot.core.HotReload.Editor`) with
// zero references to the rest of wk-core. That isolation is the whole
// point: if the main `dev.whyknot.core.Editor` assembly fails to
// compile, HotReload still loads, the watcher still fires
// AssetDatabase.Refresh() on each save, and the per-session log file
// still captures the compile errors so the next iteration can read
// them without the user copy-pasting console output.
//
// All diagnostic output goes to:
//   %LocalAppData%/WhyKnot/Logs/dev.whyknot.core.hotreload/session-<timestamp>.log
//
// Same 3-session retention as the main WkLogger sessions, but this is
// a separate directory -- HotReload is wk-core's internal tooling, not
// a "package log" in the user-facing sense. With three packages keeping
// 3 logs each plus this HotReload group, the user has at most 12 log
// files system-wide.
//
// Behaviour:
//   * Two FileSystemWatchers (recursive, *.cs) cover both roots Unity
//     treats as live script source: `<project>/Assets/` (the legacy
//     drop-scripts-anywhere workflow) and `<project>/Packages/` (where
//     local-deployed packages like dev.whyknot.* iterate). Either watcher
//     flipping the flag triggers a refresh. Library/PackageCache/ is
//     deliberately not watched because read-only VPM installs don't
//     iterate and the cache churns on its own.
//   * EditorApplication.update debounces the flag (~0.4 s) and then
//     calls AssetDatabase.Refresh(). Unity happily refreshes from a
//     scripted call even when the editor window isn't focused, which
//     is the whole point.
//   * CompilationPipeline.assemblyCompilationFinished records a
//     one-line summary per assembly plus one line per compile error.
//     Errors mirror to the Unity Console so the user notices; per-
//     assembly summaries and per-file change events stay file-only.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UmeVrcfQol.Internal.HotReload {

    [InitializeOnLoad]
    internal static class EditorHotReload {
        private const double DebounceSeconds = 0.4;
        private const int MaxSessions = 3;
        // Derived once from the executing assembly so a copy of this file
        // bundled into a downstream package writes to a per-package log
        // directory automatically. The wk-core copy logs under
        // "WhyKnot/Logs/dev.whyknot.core.hotreload"; a copy inside
        // dev.whyknot.wk-vrc-qol logs under that package's name instead.
        // No need to rewrite the constant when the sync script copies
        // this file into a downstream.
        private static readonly string LogSubpath = "WhyKnot/Logs/" + ResolveAsmIdentity() + ".hotreload";

        private static string ResolveAsmIdentity() {
            try {
                return typeof(EditorHotReload).Assembly.GetName().Name;
            } catch {
                return "dev.whyknot.core";
            }
        }

        private static readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private static volatile bool _pendingRefresh;
        private static double _refreshDueAt;
        private static int _refreshCounter;
        private static string _logPath;
        private static readonly object _writeLock = new object();

        static EditorHotReload() {
            InitLogFile();
            LogInfo("EditorHotReload starting");

            string dataPath;
            try {
                dataPath = Application.dataPath;
            } catch (Exception ex) {
                LogException(ex, "Could not resolve Application.dataPath");
                return;
            }
            if (string.IsNullOrEmpty(dataPath)) {
                LogError("Application.dataPath was empty; watchers not started");
                return;
            }

            StartWatcher(dataPath, "Assets");

            // Sibling Packages/ directory: where local-deployed packages
            // (e.g. scripts/deploy-to-local.ps1 mirroring into an avatar
            // project) actually land. Without this watcher, iterating on a
            // deployed package only refreshes when the user clicks Unity
            // back into focus -- which defeats the point of the local-deploy
            // workflow.
            try {
                var projectRoot = Path.GetDirectoryName(dataPath);
                if (!string.IsNullOrEmpty(projectRoot)) {
                    var packagesPath = Path.Combine(projectRoot, "Packages");
                    if (Directory.Exists(packagesPath)) StartWatcher(packagesPath, "Packages");
                    else LogDebug($"[Watcher] No Packages directory at {packagesPath}; skipped.");
                }
            } catch (Exception ex) {
                LogException(ex, "[Watcher] Failed to derive Packages path");
            }

            EditorApplication.update += Tick;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationStarted += OnCompileStarted;
            CompilationPipeline.compilationFinished += OnCompileFinished;
            AssemblyReloadEvents.beforeAssemblyReload += () => LogDebug("[Reload] beforeAssemblyReload");
            AssemblyReloadEvents.afterAssemblyReload  += () => LogDebug("[Reload] afterAssemblyReload");
        }

        private static void StartWatcher(string root, string label) {
            try {
                var w = new FileSystemWatcher(root) {
                    IncludeSubdirectories = true,
                    Filter = "*.cs",
                    NotifyFilter = NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.Size
                                 | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                };
                w.Changed += OnChange;
                w.Created += OnChange;
                w.Deleted += OnChange;
                w.Renamed += OnRename;
                w.Error   += (s, e) => LogWarn($"[Watcher:{label}] Error: {e.GetException()?.Message}");
                _watchers.Add(w);
                LogInfo($"[Watcher] Active on {root} ({label})");
            } catch (Exception ex) {
                LogException(ex, $"[Watcher] Failed to start on {root}");
            }
        }

        // ----- change detection ------------------------------------------

        private static void OnChange(object s, FileSystemEventArgs e) {
            if (ShouldIgnore(e.FullPath)) return;
            _pendingRefresh = true;
            _refreshDueAt = EditorApplication.timeSinceStartup + DebounceSeconds;
            LogDebug($"[Watcher] {e.ChangeType} {TrimPath(e.FullPath)}");
        }

        private static void OnRename(object s, RenamedEventArgs e) {
            if (ShouldIgnore(e.FullPath) && ShouldIgnore(e.OldFullPath)) return;
            _pendingRefresh = true;
            _refreshDueAt = EditorApplication.timeSinceStartup + DebounceSeconds;
            LogDebug($"[Watcher] Renamed {TrimPath(e.OldFullPath)} -> {TrimPath(e.FullPath)}");
        }

        private static bool ShouldIgnore(string path) {
            if (string.IsNullOrEmpty(path)) return true;
            var name = Path.GetFileName(path);
            // Unity's own temp artefacts.
            if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith(".TMP", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("~", StringComparison.Ordinal)) return true;
            return false;
        }

        private static void Tick() {
            if (!_pendingRefresh) return;
            if (EditorApplication.timeSinceStartup < _refreshDueAt) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _pendingRefresh = false;
            _refreshCounter++;
            LogDebug($"[Refresh] AssetDatabase.Refresh() #{_refreshCounter}");
            try { AssetDatabase.Refresh(); }
            catch (Exception ex) { LogException(ex, "[Refresh] AssetDatabase.Refresh() failed"); }
        }

        // ----- compile result logging ------------------------------------

        private static void OnCompileStarted(object ctx) {
            LogDebug("[Compile] Started");
        }

        private static void OnCompileFinished(object ctx) {
            LogDebug("[Compile] Finished");
        }

        private static void OnAssemblyCompiled(string asmPath, CompilerMessage[] messages) {
            int errors = 0, warnings = 0;
            if (messages != null) {
                for (int i = 0; i < messages.Length; i++) {
                    if (messages[i].type == CompilerMessageType.Error) errors++;
                    else if (messages[i].type == CompilerMessageType.Warning) warnings++;
                }
            }
            var asmName = Path.GetFileName(asmPath ?? "(unknown)");
            if (errors > 0) {
                LogWarn($"[Asm] {asmName} errors={errors} warnings={warnings}");
            } else {
                LogDebug($"[Asm] {asmName} errors={errors} warnings={warnings}");
            }

            if (messages == null) return;
            foreach (var m in messages) {
                if (m.type != CompilerMessageType.Error) continue;
                var where = string.IsNullOrEmpty(m.file) ? "" : $" {TrimPath(m.file)}({m.line},{m.column})";
                LogError($"[Compile]{where}: {m.message}");
            }
        }

        // ----- inline file logger (no dependency on the rest of wk-core) -

        private static void InitLogFile() {
            try {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(root)) {
                    try { root = Application.persistentDataPath; } catch { /* ignore */ }
                    if (string.IsNullOrEmpty(root)) root = Environment.CurrentDirectory;
                }
                var dir = Path.Combine(root, LogSubpath);
                Directory.CreateDirectory(dir);

                // Cull older sessions so the directory holds at most
                // MaxSessions files once the new one is created below.
                try {
                    var old = new DirectoryInfo(dir).GetFiles("session-*.log")
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .Skip(MaxSessions - 1)
                        .ToList();
                    foreach (var f in old) {
                        try { f.Delete(); } catch { /* ignore */ }
                    }
                } catch { /* ignore */ }

                _logPath = Path.Combine(dir, $"session-{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
                WriteHeader();
            } catch (Exception ex) {
                _logPath = null;
                UnityEngine.Debug.LogWarning($"[wk-core HotReload] Failed to init log file: {ex.Message}");
            }
        }

        private static void WriteHeader() {
            WriteRaw("================================================================");
            WriteRaw("WhyKnot Core HotReload (dev.whyknot.core.HotReload.Editor)");
            WriteRaw($"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            WriteRaw($"Unity:    {Application.unityVersion}");
            WriteRaw($"Platform: {Application.platform} ({SystemInfo.operatingSystem})");
            try { WriteRaw($"Project:  {Directory.GetParent(Application.dataPath)?.FullName}"); } catch { }
            WriteRaw($"Machine:  {Environment.MachineName}");
            WriteRaw($"User:     {Environment.UserName}");
            WriteRaw($"BatchMode:{Application.isBatchMode}");
            WriteRaw("================================================================");
        }

        private static void LogInfo(string msg)  => WriteLevel("INFO ", msg);
        private static void LogDebug(string msg) => WriteLevel("DEBUG", msg);
        private static void LogWarn(string msg) {
            WriteLevel("WARN ", msg);
            // Surface in Unity Console too -- the watcher is plumbing the
            // user might not think to grep the log file for.
            UnityEngine.Debug.LogWarning($"[wk-core HotReload] {msg}");
        }
        private static void LogError(string msg) {
            WriteLevel("ERROR", msg);
            UnityEngine.Debug.LogError($"[wk-core HotReload] {msg}");
        }
        private static void LogException(Exception ex, string context = null) {
            if (ex == null) return;
            var head = string.IsNullOrEmpty(context)
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{context} -- {ex.GetType().Name}: {ex.Message}";
            WriteLevel("ERROR", head);
            WriteRaw(ex.StackTrace ?? "(no stack)");
            WriteRaw("");
            UnityEngine.Debug.LogException(ex);
        }

        private static void WriteLevel(string level, string msg) {
            WriteRaw($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}");
        }

        private static void WriteRaw(string line) {
            if (_logPath == null) return;
            lock (_writeLock) {
                try {
                    using (var fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var sw = new StreamWriter(fs)) {
                        sw.WriteLine(line);
                    }
                } catch { /* never let logging throw */ }
            }
        }

        private static string TrimPath(string p) {
            if (string.IsNullOrEmpty(p)) return p;
            try {
                var root = Directory.GetParent(Application.dataPath)?.FullName;
                if (!string.IsNullOrEmpty(root) && p.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                    return p.Substring(root.Length).TrimStart('/', '\\');
                }
            } catch { /* ignore */ }
            return p;
        }
    }
}
