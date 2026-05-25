// WkLoggerRegistry.cs
//
// Process-wide lookup for the per-package WkLogger instances. WkLogger
// self-registers on construction; downstream packages typically expose
// their registered instance via a small static holder
// (e.g. `VrcfQolLogger.Instance`) so call sites don't have to type the
// packageId every time.
//
// Get(packageId) throws if no logger was registered. That's deliberate:
// the requirement is that every WhyKnot package owns and registers its
// logger in [InitializeOnLoad], so a missing registration is a wiring
// bug we want to surface loudly.

using System;
using System.Collections.Generic;

namespace UmeVrcfQol.Internal.Logging {

    public static class WkLoggerRegistry {

        private static readonly Dictionary<string, WkLogger> _loggers =
            new Dictionary<string, WkLogger>(StringComparer.Ordinal);
        private static readonly object _lock = new object();

        /// <summary>
        /// Look up the WkLogger registered for <paramref name="packageId"/>.
        /// Throws InvalidOperationException if no logger was registered --
        /// every WhyKnot package must build and register one in its
        /// [InitializeOnLoad] static constructor.
        /// </summary>
        public static WkLogger Get(string packageId) {
            if (string.IsNullOrEmpty(packageId)) {
                throw new ArgumentException("packageId is required", nameof(packageId));
            }
            lock (_lock) {
                if (_loggers.TryGetValue(packageId, out var logger)) return logger;
            }
            throw new InvalidOperationException(
                $"No WkLogger registered for '{packageId}'. " +
                "Build one in your package's [InitializeOnLoad] static constructor:\n" +
                $"    new WkLogger(\"{packageId}\", \"<DisplayName>\", \"<x.y.z>\");\n" +
                "The constructor self-registers with WkLoggerRegistry.");
        }

        /// <summary>True if a logger has been registered for the package.</summary>
        public static bool IsRegistered(string packageId) {
            if (string.IsNullOrEmpty(packageId)) return false;
            lock (_lock) {
                return _loggers.ContainsKey(packageId);
            }
        }

        /// <summary>Snapshot of every currently registered logger. Useful for the smoke-test viewer or diagnostics.</summary>
        public static WkLogger[] All() {
            lock (_lock) {
                var arr = new WkLogger[_loggers.Count];
                int i = 0;
                foreach (var kvp in _loggers) arr[i++] = kvp.Value;
                return arr;
            }
        }

        /// <summary>
        /// Internal entry point used by the WkLogger constructor. If two
        /// loggers register the same packageId (typically after a domain
        /// reload), the newer one wins; the older one's file handle has
        /// already been closed by the reload.
        /// </summary>
        internal static void Register(WkLogger logger) {
            if (logger == null) return;
            lock (_lock) {
                _loggers[logger.PackageId] = logger;
            }
        }
    }
}
