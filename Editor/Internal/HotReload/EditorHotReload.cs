#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UmeVrcfQol.Internal.HotReload
{
    [InitializeOnLoad]
    internal static partial class EditorHotReload
    {
        private const double DebounceSeconds = 0.4;
        private const int MaxSessions = 3;
        private static readonly string AssemblyIdentity = ResolveAsmIdentity();
        private static readonly string PackageId = ResolvePackageIdFromAssemblyName(AssemblyIdentity);
        private static readonly string LogSubpath = "WhyKnot/Logs/" + AssemblyIdentity + ".hotreload";
        internal static readonly string HotReloadEnabledPrefsKey = ResolveHotReloadEnabledPrefsKey(AssemblyIdentity);

        private static string ResolveAsmIdentity()
        {
            return typeof(EditorHotReload).Assembly.GetName().Name ?? "dev.whyknot.wk-vrcfury-qol.Editor";
        }

        internal static string ResolvePackageIdFromAssemblyName(string assemblyName)
        {
            return string.IsNullOrEmpty(assemblyName)
                ? "dev.whyknot.wk-vrcfury-qol"
                : assemblyName.EndsWith(".HotReload.Editor", StringComparison.OrdinalIgnoreCase)
                    ? assemblyName[..^".HotReload.Editor".Length]
                    : assemblyName.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase)
                        ? assemblyName[..^".Editor".Length]
                        : assemblyName;
        }

        internal static string ResolveHotReloadEnabledPrefsKey(string assemblyName)
        {
            return ResolvePackageIdFromAssemblyName(assemblyName) + ".settings.hot-reload-enabled";
        }

        private const int WatcherBufferSize = 64 * 1024;
        private const int MaxErrorRetries = 3;
        private const double ErrorRetryCooldownSeconds = 60;
        private const int RecentEventBuffer = 50;

        private static readonly List<FileSystemWatcher> Watchers = new();
        private static readonly Dictionary<FileSystemWatcher, WatcherState> WatcherStates = new();
        private static volatile bool PendingRefresh;
        private static double RefreshDueAt;
        private static readonly object WriteLock = new();

        private static readonly HashSet<string> PendingShaderPaths = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object PendingShaderPathsLock = new();

        internal static int RefreshCount { get; private set; }
        internal static int ShaderReimportCount { get; private set; }
        internal static string? LogFilePath { get; private set; }
        internal static IReadOnlyList<RecentEvent> RecentEvents => RecentEventLog;
        internal static string LastCompileResult { get; private set; } = "(no compile yet)";

        private static readonly List<RecentEvent> RecentEventLog = new(RecentEventBuffer);

        public struct RecentEvent
        {
            public DateTime When;
            public string Kind;
            public string Path;
        }

        private sealed class WatcherState
        {
            public string? Root;
            public string? Label;
            public string? FilterDescription;
            public int ErrorRetryCount;
            public DateTime FirstErrorAtUtc;
        }

        static EditorHotReload()
        {
            InitLogFile();
            LogInfo("EditorHotReload starting");

            string dataPath;
            try
            {
                dataPath = Application.dataPath;
            }
            catch (UnityException ex)
            {
                LogException(ex, "Could not resolve Application.dataPath");
                return;
            }
            if (string.IsNullOrEmpty(dataPath))
            {
                LogError("Application.dataPath was empty; watchers not started");
                return;
            }

            string? watchRoot = ResolvePackageWatchRoot(dataPath);
            if (watchRoot == null || watchRoot.Length == 0)
            {
                LogError($"[Watcher] Could not resolve package root for {PackageId}; watcher not started.");
            }
            else
            {
                StartWatcher(watchRoot, PackageId);
            }

            if (!EditorPrefs.GetBool(HotReloadEnabledPrefsKey, defaultValue: true))
            {
                LogInfo("Hot-reload watcher disabled via settings; tearing down watchers.");
                foreach (FileSystemWatcher w in Watchers)
                {
                    StopWatcher(w);
                }
                Watchers.Clear();
                WatcherStates.Clear();
                return;
            }

            EditorApplication.update += Tick;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationStarted += OnCompileStarted;
            CompilationPipeline.compilationFinished += OnCompileFinished;
            AssemblyReloadEvents.beforeAssemblyReload += () => LogDebug("[Reload] beforeAssemblyReload");
            AssemblyReloadEvents.afterAssemblyReload += () => LogDebug("[Reload] afterAssemblyReload");
        }

        private static void StopWatcher(FileSystemWatcher watcher)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or PlatformNotSupportedException)
            {
                LogDebug($"[Watcher] Stop failed: {ex.Message}");
            }
        }

        private static void StartWatcher(string root, string label)
        {
            try
            {
                var w = new FileSystemWatcher(root)
                {
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
                w.Error += (s, e) => OnWatcherError(w, e);
                Watchers.Add(w);
                WatcherStates[w] = new WatcherState
                {
                    Root = root,
                    Label = label,
                    FilterDescription = "*.cs / *.asmdef / *.asmref / *.shader / *.compute / *.cginc / *.hlsl",
                };
                LogInfo($"[Watcher] Active on {root} ({label}, buffer={WatcherBufferSize / 1024} KB)");
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                LogException(ex, $"[Watcher] Failed to start on {root}");
            }
        }

        private static void OnWatcherError(FileSystemWatcher offender, ErrorEventArgs args)
        {
            Exception ex = args.GetException();
            WatcherStates.TryGetValue(offender, out WatcherState? state);
            string label = state?.Label ?? "?";
            string root = state?.Root ?? "(unknown)";
            LogWarn($"[Watcher:{label}] Error: {ex?.Message ?? "(no message)"}");

            if (state == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if ((nowUtc - state.FirstErrorAtUtc).TotalSeconds > ErrorRetryCooldownSeconds)
            {
                state.ErrorRetryCount = 0;
                state.FirstErrorAtUtc = nowUtc;
            }
            if (state.ErrorRetryCount >= MaxErrorRetries)
            {
                LogError($"[Watcher:{label}] Hit retry cap ({MaxErrorRetries} restarts in {ErrorRetryCooldownSeconds:N0}s); leaving watcher down until next domain reload.");
                return;
            }
            state.ErrorRetryCount++;

            StopWatcher(offender);
            Watchers.Remove(offender);
            WatcherStates.Remove(offender);
            System.Threading.Thread.Sleep(1000);
            LogWarn($"[Watcher:{label}] Restarting (attempt {state.ErrorRetryCount}/{MaxErrorRetries})...");
            StartWatcher(root, label);
        }

        private static void OnChange(object s, FileSystemEventArgs e)
        {
            if (ShouldIgnore(e.FullPath))
            {
                return;
            }

            if (!IsTrackedExtension(e.FullPath))
            {
                return;
            }

            PendingRefresh = true;
            RefreshDueAt = EditorApplication.timeSinceStartup + DebounceSeconds;
            if (IsShaderSource(e.FullPath))
            {
                lock (PendingShaderPathsLock) { PendingShaderPaths.Add(e.FullPath); }
            }
            RecordEvent(e.ChangeType.ToString(), e.FullPath);
            LogDebug($"[Watcher] {e.ChangeType} {TrimPath(e.FullPath)}");
        }

        private static void OnRename(object s, RenamedEventArgs e)
        {
            if (ShouldIgnore(e.FullPath) && ShouldIgnore(e.OldFullPath))
            {
                return;
            }

            if (!IsTrackedExtension(e.FullPath) && !IsTrackedExtension(e.OldFullPath))
            {
                return;
            }

            PendingRefresh = true;
            RefreshDueAt = EditorApplication.timeSinceStartup + DebounceSeconds;
            if (IsShaderSource(e.FullPath))
            {
                lock (PendingShaderPathsLock) { PendingShaderPaths.Add(e.FullPath); }
            }
            RecordEvent("Renamed", e.FullPath);
            LogDebug($"[Watcher] Renamed {TrimPath(e.OldFullPath)} -> {TrimPath(e.FullPath)}");
        }

        private static void RecordEvent(string kind, string path)
        {
            lock (RecentEventLog)
            {
                RecentEventLog.Add(new RecentEvent { When = DateTime.Now, Kind = kind, Path = path });
                while (RecentEventLog.Count > RecentEventBuffer)
                {
                    RecentEventLog.RemoveAt(0);
                }
            }
        }

        private static void Tick()
        {
            if (!PendingRefresh)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < RefreshDueAt)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            PendingRefresh = false;
            RefreshCount++;

            List<string>? shaderOsPaths = null;
            lock (PendingShaderPathsLock)
            {
                if (PendingShaderPaths.Count > 0)
                {
                    shaderOsPaths = new List<string>(PendingShaderPaths);
                    PendingShaderPaths.Clear();
                }
            }

            LogDebug($"[Refresh] AssetDatabase.Refresh() #{RefreshCount}");
            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception ex) when (ex is UnityException or InvalidOperationException or IOException)
            {
                LogException(ex, "[Refresh] AssetDatabase.Refresh() failed");
            }

            if (shaderOsPaths != null)
            {
                DrainPendingShaders(shaderOsPaths);
            }
        }

        private static void DrainPendingShaders(List<string> osPaths)
        {
            var directReimports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var includeChangeRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string osPath in osPaths)
            {
                string? unityPath = ToUnityPath(osPath);
                if (unityPath == null || unityPath.Length == 0)
                {
                    continue;
                }

                if (IsRecompilableShaderAsset(osPath))
                {
                    directReimports.Add(unityPath);
                }
                else
                {
                    string? root = ResolveReimportRoot(unityPath);
                    if (root != null && root.Length > 0)
                    {
                        includeChangeRoots.Add(root);
                    }
                }
            }
            if (includeChangeRoots.Count > 0)
            {
                foreach (string root in includeChangeRoots)
                {
                    CollectShadersUnder(root, "t:Shader", directReimports);
                    CollectShadersUnder(root, "t:ComputeShader", directReimports);
                }
            }
            if (directReimports.Count == 0)
            {
                return;
            }

            int reimported = 0;
            foreach (string unityPath in directReimports)
            {
                try
                {
                    AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceUpdate);
                    reimported++;
                    LogDebug($"[Shader] ForceUpdate {unityPath}");
                }
                catch (Exception ex) when (ex is UnityException or InvalidOperationException or IOException)
                {
                    LogException(ex, $"[Shader] ForceUpdate {unityPath} failed");
                }
            }
            if (reimported > 0)
            {
                ShaderReimportCount += reimported;
                LogInfo(
                    $"[Shader] Reimported {reimported} shader asset(s) " +
                    $"after {osPaths.Count} watched change(s)" +
                    (includeChangeRoots.Count > 0 ? $", expanded {includeChangeRoots.Count} include-change root(s)" : ""));
            }
        }

        private static void CollectShadersUnder(string root, string typeFilter, HashSet<string> sink)
        {
            string[] guids;
            try
            {
                guids = AssetDatabase.FindAssets(typeFilter, new[] { root });
            }
            catch (Exception ex) when (ex is UnityException or ArgumentException or InvalidOperationException)
            {
                LogException(ex, $"[Shader] FindAssets {typeFilter} under {root} failed");
                return;
            }
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p))
                {
                    sink.Add(p);
                }
            }
        }

        private static void OnCompileStarted(object ctx)
        {
            LogDebug("[Compile] Started");
        }

        private static void OnCompileFinished(object ctx)
        {
            LogDebug("[Compile] Finished");
        }

        private static void OnAssemblyCompiled(string asmPath, CompilerMessage[] messages)
        {
            int errors = 0, warnings = 0;
            if (messages != null)
            {
                for (int i = 0; i < messages.Length; i++)
                {
                    if (messages[i].type == CompilerMessageType.Error)
                    {
                        errors++;
                    }
                    else if (messages[i].type == CompilerMessageType.Warning)
                    {
                        warnings++;
                    }
                }
            }
            string asmName = Path.GetFileName(asmPath ?? "(unknown)");
            LastCompileResult = errors > 0
                ? $"{asmName}: {errors} errors, {warnings} warnings"
                : $"{asmName}: ok ({warnings} warnings)";
            if (errors > 0)
            {
                LogWarn($"[Asm] {asmName} errors={errors} warnings={warnings}");
            }
            else
            {
                LogDebug($"[Asm] {asmName} errors={errors} warnings={warnings}");
            }

            if (messages == null)
            {
                return;
            }

            foreach (CompilerMessage m in messages)
            {
                if (m.type != CompilerMessageType.Error)
                {
                    continue;
                }

                string where = string.IsNullOrEmpty(m.file) ? "" : $" {TrimPath(m.file)}({m.line},{m.column})";
                LogError($"[Compile]{where}: {m.message}");
            }
        }
    }
}
