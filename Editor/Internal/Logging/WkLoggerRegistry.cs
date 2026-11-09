#nullable enable

using System;
using System.Collections.Generic;

namespace UmeVrcfQol.Internal.Logging
{
    public static class WkLoggerRegistry
    {
        private static readonly Dictionary<string, WkLogger> Loggers =
            new(StringComparer.Ordinal);
        private static readonly object Lock = new();

        public static WkLogger Get(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException("packageId is required", nameof(packageId));
            }
            lock (Lock)
            {
                if (Loggers.TryGetValue(packageId, out WkLogger? logger))
                {
                    return logger;
                }
            }
            throw new InvalidOperationException(
                $"No WkLogger registered for '{packageId}'. " +
                "Build one in your package's [InitializeOnLoad] static constructor:\n" +
                $"    new WkLogger(\"{packageId}\", \"<DisplayName>\", \"<x.y.z>\");\n" +
                "The constructor self-registers with WkLoggerRegistry.");
        }

        public static bool IsRegistered(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return false;
            }

            lock (Lock)
            {
                return Loggers.ContainsKey(packageId);
            }
        }

        public static WkLogger[] All()
        {
            lock (Lock)
            {
                var arr = new WkLogger[Loggers.Count];
                int i = 0;
                foreach (KeyValuePair<string, WkLogger> kvp in Loggers)
                {
                    arr[i++] = kvp.Value;
                }

                return arr;
            }
        }

        internal static void Register(WkLogger logger)
        {
            if (logger == null)
            {
                return;
            }

            lock (Lock)
            {
                Loggers[logger.PackageId] = logger;
            }
        }
    }
}
