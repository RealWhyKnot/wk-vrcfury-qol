#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security;
using UnityEngine;

namespace UmeVrcfQol.Internal.HotReload
{
    internal static partial class EditorHotReload
    {
        private static bool WriteFailureReported;

        private static bool IsFileAccessFailure(Exception ex)
        {
            return ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException;
        }

        private static void InitLogFile()
        {
            try
            {
                string dir = Path.Combine(ResolveLogRoot(), LogSubpath);
                Directory.CreateDirectory(dir);
                LogFilePath = Path.Combine(dir, $"session-{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
                CullOldSessions(dir);
                WriteHeader();
            }
            catch (Exception ex) when (IsFileAccessFailure(ex))
            {
                LogFilePath = null;
                Debug.LogWarning("[VRCFury QoL HotReload] Failed to init log file.");
                Debug.LogException(ex);
            }
        }

        private static string ResolveLogRoot()
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
            }
            return string.IsNullOrEmpty(root) ? Environment.CurrentDirectory : root;
        }

        private static void CullOldSessions(string dir)
        {
            try
            {
                var old = new DirectoryInfo(dir).GetFiles("session-*.log")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Skip(MaxSessions - 1)
                    .ToList();
                foreach (FileInfo f in old)
                {
                    try
                    {
                        f.Delete();
                    }
                    catch (Exception ex) when (IsFileAccessFailure(ex))
                    {
                        LogDebug($"[Log] Could not delete {f.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (IsFileAccessFailure(ex))
            {
                LogDebug($"[Log] Could not list old sessions: {ex.Message}");
            }
        }

        private static string? ResolveProjectRoot()
        {
            try
            {
                return Directory.GetParent(Application.dataPath)?.FullName;
            }
            catch (Exception ex) when (ex is UnityException || IsFileAccessFailure(ex))
            {
                return null;
            }
        }

        private static void WriteHeader()
        {
            WriteRaw("================================================================");
            WriteRaw($"WhyKnot HotReload ({AssemblyIdentity})");
            WriteRaw($"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            WriteRaw($"Unity:    {Application.unityVersion}");
            WriteRaw($"Platform: {Application.platform} ({SystemInfo.operatingSystem})");
            WriteRaw($"Project:  {ResolveProjectRoot()}");
            WriteRaw($"Machine:  {Environment.MachineName}");
            WriteRaw($"User:     {Environment.UserName}");
            WriteRaw($"BatchMode:{Application.isBatchMode}");
            WriteRaw("================================================================");
        }

        private static void LogInfo(string msg)
        {
            WriteLevel("INFO ", msg);
        }

        private static void LogDebug(string msg)
        {
            WriteLevel("DEBUG", msg);
        }

        private static void LogWarn(string msg)
        {
            WriteLevel("WARN ", msg);
            Debug.LogWarning($"[VRCFury QoL HotReload] {msg}");
        }

        private static void LogError(string msg)
        {
            WriteLevel("ERROR", msg);
            Debug.LogError($"[VRCFury QoL HotReload] {msg}");
        }

        private static void LogException(Exception ex, string? context = null)
        {
            if (ex == null)
            {
                return;
            }

            string head = string.IsNullOrEmpty(context)
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{context} -- {ex.GetType().Name}: {ex.Message}";
            WriteLevel("ERROR", head);
            WriteRaw(ex.StackTrace ?? "(no stack)");
            WriteRaw("");
            Debug.LogException(ex);
        }

        private static void WriteLevel(string level, string msg)
        {
            WriteRaw($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}");
        }

        private static void WriteRaw(string line)
        {
            string? path = LogFilePath;
            if (path == null)
            {
                return;
            }

            lock (WriteLock)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var sw = new StreamWriter(fs);
                    sw.WriteLine(line);
                }
                catch (Exception ex) when (IsFileAccessFailure(ex))
                {
                    if (!WriteFailureReported)
                    {
                        WriteFailureReported = true;
                        Debug.LogWarning($"[VRCFury QoL HotReload] Could not write to {path}.");
                        Debug.LogException(ex);
                    }
                }
            }
        }

        private static string TrimPath(string p)
        {
            if (string.IsNullOrEmpty(p))
            {
                return p;
            }

            string? root = ResolveProjectRoot();
            return root != null && root.Length > 0 && p.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? p[root.Length..].TrimStart('/', '\\')
                : p;
        }
    }
}
