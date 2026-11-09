#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace UmeVrcfQol.Internal.HotReload
{
    internal static partial class EditorHotReload
    {
        private static string? ResolvePackageWatchRoot(string dataPath)
        {
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(EditorHotReload).Assembly);
                if (info != null)
                {
                    if (!string.IsNullOrEmpty(info.resolvedPath) && Directory.Exists(info.resolvedPath))
                    {
                        return info.resolvedPath;
                    }
                    string? fromAssetPath = UnityAssetPathToOsPath(dataPath, info.assetPath);
                    if (fromAssetPath != null && fromAssetPath.Length > 0 && Directory.Exists(fromAssetPath))
                    {
                        return fromAssetPath;
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnityException or InvalidOperationException)
            {
                LogException(ex, "[Watcher] PackageInfo lookup failed");
            }

            string? embedded = EmbeddedPackagePath(dataPath, PackageId);
            return embedded != null && embedded.Length > 0 && Directory.Exists(embedded) ? embedded : null;
        }

        internal static string? EmbeddedPackagePath(string dataPath, string packageId)
        {
            if (string.IsNullOrEmpty(dataPath) || string.IsNullOrEmpty(packageId))
            {
                return null;
            }

            string? projectRoot = Path.GetDirectoryName(dataPath);
            return projectRoot == null || projectRoot.Length == 0 ? null : Path.Combine(projectRoot, "Packages", packageId);
        }

        private static string? UnityAssetPathToOsPath(string dataPath, string assetPath)
        {
            if (string.IsNullOrEmpty(dataPath) || string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                string? projectRoot = Path.GetDirectoryName(dataPath);
                if (projectRoot == null || projectRoot.Length == 0)
                {
                    return null;
                }

                string suffix = assetPath.Length > "Assets".Length
                    ? assetPath["Assets".Length..].TrimStart('/')
                    : "";
                return Path.Combine(projectRoot, "Assets", suffix.Replace('/', Path.DirectorySeparatorChar));
            }
            if (assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                string? projectRoot = Path.GetDirectoryName(dataPath);
                if (projectRoot == null || projectRoot.Length == 0)
                {
                    return null;
                }

                string suffix = assetPath["Packages/".Length..].Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(projectRoot, "Packages", suffix);
            }
            return null;
        }

        private static bool ShouldIgnore(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            string name = Path.GetFileName(path);
            return name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".TMP", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith('~');
        }

        internal static bool IsTrackedExtension(string path)
        {
            return !string.IsNullOrEmpty(path)
                && (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase)
                    || IsShaderSource(path));
        }

        internal static bool IsShaderSource(string path)
        {
            return !string.IsNullOrEmpty(path)
                && (path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".cginc", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsRecompilableShaderAsset(string path)
        {
            return !string.IsNullOrEmpty(path)
                && (path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase));
        }

        internal static string? ResolveReimportRoot(string unityPath)
        {
            if (string.IsNullOrEmpty(unityPath))
            {
                return null;
            }

            unityPath = unityPath.Replace('\\', '/');
            return IsOwnPackageUnityPath(unityPath, PackageId)
                ? "Packages/" + PackageId
                : null;
        }

        internal static bool IsOwnPackageUnityPath(string unityPath, string packageId)
        {
            if (string.IsNullOrEmpty(unityPath) || string.IsNullOrEmpty(packageId))
            {
                return false;
            }

            unityPath = unityPath.Replace('\\', '/').TrimEnd('/');
            string root = "Packages/" + packageId;
            return string.Equals(unityPath, root, StringComparison.OrdinalIgnoreCase)
                || unityPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ToUnityPath(string osPath)
        {
            string trimmed = TrimPath(osPath);
            return string.IsNullOrEmpty(trimmed) ? null : trimmed.Replace('\\', '/');
        }
    }
}
