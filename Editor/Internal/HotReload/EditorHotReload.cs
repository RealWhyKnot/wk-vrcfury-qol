// EditorHotReload.cs
//
// Lives in a small hot-reload layer that avoids depending on package tool
// code. If the main tool code fails to compile, HotReload can still record
// the compile errors so the next iteration can read them without the user
// copy-pasting console output.
//
// All diagnostic output goes to:
//   %LocalAppData%/WhyKnot/Logs/<assembly-name>.hotreload/session-<timestamp>.log
//
// Same 3-session retention as the main WkLogger sessions, but this is
// a separate directory from the main package log.
//
// Behaviour:
//   * One recursive FileSystemWatcher covers this package's own root only
//     (normally `<project>/Packages/dev.whyknot.wk-vrcfury-qol/`). Edits to
//     other Assets/ or Packages/ content must not cause this package to
//     reload the project.
//   * Tracked extensions: .cs / .asmdef / .asmref for script reload,
//     .shader / .compute / .cginc / .hlsl for shader reload.
//   * EditorApplication.update debounces the flag (~0.4 s) and then
//     calls AssetDatabase.Refresh(). Unity happily refreshes from a
//     scripted call even when the editor window isn't focused, which
//     is the whole point.
//   * Shader sources additionally get AssetDatabase.ImportAsset(...
//     ForceUpdate) after the Refresh: Unity's ShaderCache occasionally
//     fails to invalidate the compiled binary when a .shader source
//     changes (especially edits to pass-scope state directives that
//     don't alter the HLSL the compiler hashes per pass), so a plain
//     Refresh isn't always enough to make a deployed shader actually
//     run with its new contents. .cginc / .hlsl include files trigger
//     the same forced reimport on every .shader file under the same
//     deployed package root, because Unity's dependency-tracked
//     reimport of dependent shaders is subject to the same cache miss.
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
        // bundled into a downstream package writes to a per-assembly log
        // directory automatically.
        private static readonly string AssemblyIdentity = ResolveAsmIdentity();
        private static readonly string PackageId = ResolvePackageIdFromAssemblyName(AssemblyIdentity);
        private static readonly string LogSubpath = "WhyKnot/Logs/" + AssemblyIdentity + ".hotreload";
        internal static readonly string HotReloadEnabledPrefsKey = ResolveHotReloadEnabledPrefsKey(AssemblyIdentity);

        private static string ResolveAsmIdentity() {
            try {
                return typeof(EditorHotReload).Assembly.GetName().Name;
            } catch {
                return "dev.whyknot.wk-vrcfury-qol.Editor";
            }
        }

        internal static string ResolvePackageIdFromAssemblyName(string assemblyName) {
            if (string.IsNullOrEmpty(assemblyName)) return "dev.whyknot.wk-vrcfury-qol";
            if (assemblyName.EndsWith(".HotReload.Editor", StringComparison.OrdinalIgnoreCase)) {
                return assemblyName.Substring(0, assemblyName.Length - ".HotReload.Editor".Length);
            }
            if (assemblyName.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase)) {
                return assemblyName.Substring(0, assemblyName.Length - ".Editor".Length);
            }
            return assemblyName;
        }

        internal static string ResolveHotReloadEnabledPrefsKey(string assemblyName) {
            return ResolvePackageIdFromAssemblyName(assemblyName) + ".settings.hot-reload-enabled";
        }

        private const int WatcherBufferSize = 64 * 1024;   // up from FileSystemWatcher's 8 KB default
        private const int MaxErrorRetries = 3;
        private const double ErrorRetryCooldownSeconds = 60;
        private const int RecentEventBuffer = 50;

        private static readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private static readonly Dictionary<FileSystemWatcher, WatcherState> _watcherState = new Dictionary<FileSystemWatcher, WatcherState>();
        private static volatile bool _pendingRefresh;
        private static double _refreshDueAt;
        private static int _refreshCounter;
        private static int _shaderReimportCounter;
        private static string _logPath;
        private static readonly object _writeLock = new object();

        // Pending shader source paths -- OS paths captured from the watcher,
        // converted to Unity asset paths at Tick time and ForceUpdate'd after
        // the AssetDatabase.Refresh() call.
        private static readonly HashSet<string> _pendingShaderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _pendingShaderPathsLock = new object();

        // ---- diagnostic state exposed to the status viewer ----------

        internal static int RefreshCount => _refreshCounter;
        internal static int ShaderReimportCount => _shaderReimportCounter;
        internal static string LogFilePath => _logPath;
        internal static IReadOnlyList<RecentEvent> RecentEvents => _recentEvents;
        internal static string LastCompileResult { get; private set; } = "(no compile yet)";

        private static readonly List<RecentEvent> _recentEvents = new List<RecentEvent>(RecentEventBuffer);

        public struct RecentEvent {
            public DateTime When;
            public string Kind;     // Changed / Created / Deleted / Renamed
            public string Path;
        }

        private sealed class WatcherState {
            public string Root;
            public string Label;
            public string FilterDescription;
            public int ErrorRetryCount;
            public DateTime FirstErrorAtUtc;
        }

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

            var watchRoot = ResolvePackageWatchRoot(dataPath);
            if (string.IsNullOrEmpty(watchRoot)) {
                LogError($"[Watcher] Could not resolve package root for {PackageId}; watcher not started.");
            } else {
                StartWatcher(watchRoot, PackageId);
            }

            // Allow disabling via WkEditorPrefs-style toggle so the
            // WkSettingsProvider's "Enable hot-reload watcher" toggle has
            // an effect on next Editor startup.
            if (!UnityEditor.EditorPrefs.GetBool(HotReloadEnabledPrefsKey, true)) {
                LogInfo("Hot-reload watcher disabled via settings; tearing down watchers.");
                foreach (var w in _watchers) {
                    try { w.EnableRaisingEvents = false; w.Dispose(); } catch { /* ignore */ }
                }
                _watchers.Clear();
                _watcherState.Clear();
                return;
            }

            EditorApplication.update += Tick;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationStarted += OnCompileStarted;
            CompilationPipeline.compilationFinished += OnCompileFinished;
            AssemblyReloadEvents.beforeAssemblyReload += () => LogDebug("[Reload] beforeAssemblyReload");
            AssemblyReloadEvents.afterAssemblyReload  += () => LogDebug("[Reload] afterAssemblyReload");
        }

        private static void StartWatcher(string root, string label) {
            // Single watcher per root with Filter = "*" so we can react to
            // .cs and .asmdef / .asmref changes in one place; ShouldIgnore
            // gates everything else (.meta, .TMP, ~ -prefixed) plus we
            // additionally screen for the extensions we care about in OnChange.
            try {
                var w = new FileSystemWatcher(root) {
                    IncludeSubdirectories = true,
                    Filter = "*",
                    NotifyFilter = NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.Size
                                 | NotifyFilters.CreationTime,
                    InternalBufferSize = WatcherBufferSize,
                    EnableRaisingEvents = true,
                };
                w.Changed += OnChange;
                w.Created += OnChange;
                w.Deleted += OnChange;
                w.Renamed += OnRename;
                w.Error   += (s, e) => OnWatcherError(w, e);
                _watchers.Add(w);
                _watcherState[w] = new WatcherState {
                    Root = root,
                    Label = label,
                    FilterDescription = "*.cs / *.asmdef / *.asmref / *.shader / *.compute / *.cginc / *.hlsl",
                };
                LogInfo($"[Watcher] Active on {root} ({label}, buffer={WatcherBufferSize / 1024} KB)");
            } catch (Exception ex) {
                LogException(ex, $"[Watcher] Failed to start on {root}");
            }
        }

        private static string ResolvePackageWatchRoot(string dataPath) {
            try {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(EditorHotReload).Assembly);
                if (info != null) {
                    if (!string.IsNullOrEmpty(info.resolvedPath) && Directory.Exists(info.resolvedPath)) {
                        return info.resolvedPath;
                    }
                    var fromAssetPath = UnityAssetPathToOsPath(dataPath, info.assetPath);
                    if (!string.IsNullOrEmpty(fromAssetPath) && Directory.Exists(fromAssetPath)) {
                        return fromAssetPath;
                    }
                }
            } catch (Exception ex) {
                LogException(ex, "[Watcher] PackageInfo lookup failed");
            }

            var embedded = EmbeddedPackagePath(dataPath, PackageId);
            if (!string.IsNullOrEmpty(embedded) && Directory.Exists(embedded)) return embedded;

            return null;
        }

        internal static string EmbeddedPackagePath(string dataPath, string packageId) {
            if (string.IsNullOrEmpty(dataPath) || string.IsNullOrEmpty(packageId)) return null;
            var projectRoot = Path.GetDirectoryName(dataPath);
            if (string.IsNullOrEmpty(projectRoot)) return null;
            return Path.Combine(projectRoot, "Packages", packageId);
        }

        private static string UnityAssetPathToOsPath(string dataPath, string assetPath) {
            if (string.IsNullOrEmpty(dataPath) || string.IsNullOrEmpty(assetPath)) return null;
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase)) {
                var projectRoot = Path.GetDirectoryName(dataPath);
                if (string.IsNullOrEmpty(projectRoot)) return null;
                var suffix = assetPath.Length > "Assets".Length
                    ? assetPath.Substring("Assets".Length).TrimStart('/')
                    : "";
                return Path.Combine(projectRoot, "Assets", suffix.Replace('/', Path.DirectorySeparatorChar));
            }
            if (assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)) {
                var projectRoot = Path.GetDirectoryName(dataPath);
                if (string.IsNullOrEmpty(projectRoot)) return null;
                var suffix = assetPath.Substring("Packages/".Length).Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(projectRoot, "Packages", suffix);
            }
            return null;
        }

        private static void OnWatcherError(FileSystemWatcher offender, ErrorEventArgs args) {
            var ex = args.GetException();
            _watcherState.TryGetValue(offender, out var state);
            var label = state?.Label ?? "?";
            var root = state?.Root ?? "(unknown)";
            LogWarn($"[Watcher:{label}] Error: {ex?.Message ?? "(no message)"}");

            // Recovery: on InternalBufferOverflowException (the dominant
            // FileSystemWatcher failure mode), recreate the watcher up
            // to MaxErrorRetries times within ErrorRetryCooldownSeconds.
            // Outside that window, reset the counter. Past the cap, leave
            // the watcher down -- a domain reload restarts everything.
            if (state == null) return;
            var nowUtc = DateTime.UtcNow;
            if ((nowUtc - state.FirstErrorAtUtc).TotalSeconds > ErrorRetryCooldownSeconds) {
                state.ErrorRetryCount = 0;
                state.FirstErrorAtUtc = nowUtc;
            }
            if (state.ErrorRetryCount >= MaxErrorRetries) {
                LogError($"[Watcher:{label}] Hit retry cap ({MaxErrorRetries} restarts in {ErrorRetryCooldownSeconds:N0}s); leaving watcher down until next domain reload.");
                return;
            }
            state.ErrorRetryCount++;

            try { offender.EnableRaisingEvents = false; offender.Dispose(); } catch { /* ignore */ }
            _watchers.Remove(offender);
            _watcherState.Remove(offender);
            System.Threading.Thread.Sleep(1000);
            LogWarn($"[Watcher:{label}] Restarting (attempt {state.ErrorRetryCount}/{MaxErrorRetries})...");
            StartWatcher(root, label);
        }

        // ----- change detection ------------------------------------------

        private static void OnChange(object s, FileSystemEventArgs e) {
            if (ShouldIgnore(e.FullPath)) return;
            if (!IsTrackedExtension(e.FullPath)) return;
            _pendingRefresh = true;
            _refreshDueAt = EditorApplication.timeSinceStartup + DebounceSeconds;
            if (IsShaderSource(e.FullPath)) {
                lock (_pendingShaderPathsLock) { _pendingShaderPaths.Add(e.FullPath); }
            }
            RecordEvent(e.ChangeType.ToString(), e.FullPath);
            LogDebug($"[Watcher] {e.ChangeType} {TrimPath(e.FullPath)}");
        }

        private static void OnRename(object s, RenamedEventArgs e) {
            if (ShouldIgnore(e.FullPath) && ShouldIgnore(e.OldFullPath)) return;
            if (!IsTrackedExtension(e.FullPath) && !IsTrackedExtension(e.OldFullPath)) return;
            _pendingRefresh = true;
            _refreshDueAt = EditorApplication.timeSinceStartup + DebounceSeconds;
            if (IsShaderSource(e.FullPath)) {
                lock (_pendingShaderPathsLock) { _pendingShaderPaths.Add(e.FullPath); }
            }
            RecordEvent("Renamed", e.FullPath);
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

        internal static bool IsTrackedExtension(string path) {
            if (string.IsNullOrEmpty(path)) return false;
            return path.EndsWith(".cs",     StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase)
                || IsShaderSource(path);
        }

        internal static bool IsShaderSource(string path) {
            if (string.IsNullOrEmpty(path)) return false;
            return path.EndsWith(".shader",  StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cginc",   StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".hlsl",    StringComparison.OrdinalIgnoreCase);
        }

        // Whether the file is a shader binary the importer can recompile
        // directly (.shader, .compute). Include files (.cginc, .hlsl) don't
        // have their own shader importer; their changes invalidate dependent
        // .shader files instead, handled separately in DrainPendingShaders.
        internal static bool IsRecompilableShaderAsset(string path) {
            if (string.IsNullOrEmpty(path)) return false;
            return path.EndsWith(".shader",  StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase);
        }

        private static void RecordEvent(string kind, string path) {
            lock (_recentEvents) {
                _recentEvents.Add(new RecentEvent { When = DateTime.Now, Kind = kind, Path = path });
                while (_recentEvents.Count > RecentEventBuffer) _recentEvents.RemoveAt(0);
            }
        }

        private static void Tick() {
            if (!_pendingRefresh) return;
            if (EditorApplication.timeSinceStartup < _refreshDueAt) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _pendingRefresh = false;
            _refreshCounter++;

            // Snapshot the pending shader set BEFORE the AssetDatabase.Refresh
            // call, in case Refresh itself triggers another OnChange burst.
            List<string> shaderOsPaths = null;
            lock (_pendingShaderPathsLock) {
                if (_pendingShaderPaths.Count > 0) {
                    shaderOsPaths = new List<string>(_pendingShaderPaths);
                    _pendingShaderPaths.Clear();
                }
            }

            LogDebug($"[Refresh] AssetDatabase.Refresh() #{_refreshCounter}");
            try { AssetDatabase.Refresh(); }
            catch (Exception ex) { LogException(ex, "[Refresh] AssetDatabase.Refresh() failed"); }

            if (shaderOsPaths != null) DrainPendingShaders(shaderOsPaths);
        }

        // Force-reimport every changed .shader / .compute the watcher caught,
        // and for any .cginc / .hlsl includes, force-reimport every .shader /
        // .compute under the same deployed package or Assets-subtree root.
        // This pierces Unity's ShaderCache, which otherwise occasionally
        // returns a stale compiled binary even when the source file has
        // changed -- particularly for edits to pass-scope state directives
        // that don't alter the per-pass HLSL the compiler hashes.
        private static void DrainPendingShaders(List<string> osPaths) {
            var directReimports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var includeChangeRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var osPath in osPaths) {
                var unityPath = ToUnityPath(osPath);
                if (string.IsNullOrEmpty(unityPath)) continue;
                if (IsRecompilableShaderAsset(osPath)) {
                    directReimports.Add(unityPath);
                } else {
                    // .cginc / .hlsl: collect the package / Assets-subtree
                    // root we'll later scan for dependent shaders.
                    var root = ResolveReimportRoot(unityPath);
                    if (!string.IsNullOrEmpty(root)) includeChangeRoots.Add(root);
                }
            }
            // Expand each include-change root into the actual .shader and
            // .compute files inside it. Using AssetDatabase.FindAssets keeps
            // us in Unity's path space and respects whatever the project's
            // Library/ thinks is currently visible.
            if (includeChangeRoots.Count > 0) {
                foreach (var root in includeChangeRoots) {
                    CollectShadersUnder(root, "t:Shader",        directReimports);
                    CollectShadersUnder(root, "t:ComputeShader", directReimports);
                }
            }
            if (directReimports.Count == 0) return;
            int reimported = 0;
            foreach (var unityPath in directReimports) {
                try {
                    AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceUpdate);
                    reimported++;
                    LogDebug($"[Shader] ForceUpdate {unityPath}");
                } catch (Exception ex) {
                    LogException(ex, $"[Shader] ForceUpdate {unityPath} failed");
                }
            }
            if (reimported > 0) {
                _shaderReimportCounter += reimported;
                LogInfo(
                    $"[Shader] Reimported {reimported} shader asset(s) " +
                    $"after {osPaths.Count} watched change(s)" +
                    (includeChangeRoots.Count > 0 ? $", expanded {includeChangeRoots.Count} include-change root(s)" : ""));
            }
        }

        private static void CollectShadersUnder(string root, string typeFilter, HashSet<string> sink) {
            string[] guids;
            try {
                guids = AssetDatabase.FindAssets(typeFilter, new[] { root });
            } catch (Exception ex) {
                LogException(ex, $"[Shader] FindAssets {typeFilter} under {root} failed");
                return;
            }
            foreach (var guid in guids) {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p)) sink.Add(p);
            }
        }

        // Pick a Unity-path "scan root" for finding shaders that depend on a
        // changed include file. Only this package's root expands; unrelated
        // Assets/ or Packages/ changes must not trigger work from this watcher.
        internal static string ResolveReimportRoot(string unityPath) {
            if (string.IsNullOrEmpty(unityPath)) return null;
            unityPath = unityPath.Replace('\\', '/');
            return IsOwnPackageUnityPath(unityPath, PackageId)
                ? "Packages/" + PackageId
                : null;
        }

        internal static bool IsOwnPackageUnityPath(string unityPath, string packageId) {
            if (string.IsNullOrEmpty(unityPath) || string.IsNullOrEmpty(packageId)) return false;
            unityPath = unityPath.Replace('\\', '/').TrimEnd('/');
            var root = "Packages/" + packageId;
            return string.Equals(unityPath, root, StringComparison.OrdinalIgnoreCase)
                || unityPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        // Convert a FileSystemWatcher OS path to the Unity asset path Unity's
        // AssetDatabase APIs expect (project-relative, forward slashes).
        private static string ToUnityPath(string osPath) {
            var trimmed = TrimPath(osPath);
            if (string.IsNullOrEmpty(trimmed)) return null;
            return trimmed.Replace('\\', '/');
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
            LastCompileResult = errors > 0
                ? $"{asmName}: {errors} errors, {warnings} warnings"
                : $"{asmName}: ok ({warnings} warnings)";
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

        // ----- inline file logger ----------------------------------------

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
                UnityEngine.Debug.LogWarning($"[VRCFury QoL HotReload] Failed to init log file: {ex.Message}");
            }
        }

        private static void WriteHeader() {
            WriteRaw("================================================================");
            WriteRaw($"WhyKnot HotReload ({AssemblyIdentity})");
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
            UnityEngine.Debug.LogWarning($"[VRCFury QoL HotReload] {msg}");
        }
        private static void LogError(string msg) {
            WriteLevel("ERROR", msg);
            UnityEngine.Debug.LogError($"[VRCFury QoL HotReload] {msg}");
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
