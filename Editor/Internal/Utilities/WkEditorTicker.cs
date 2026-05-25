// WkEditorTicker.cs
//
// Interval-debounced EditorApplication.update subscription. Inspector
// overlays, sync loops, and background validators all share the shape
// "every N seconds, do X; don't double-tick if the previous still hasn't
// returned; survive a thrown exception so the next tick still fires."
// Each call site previously held its own (private const double, private
// static double _next, if EditorApplication.timeSinceStartup < _next return)
// triple, and the constants drifted over time. WkEditorTicker centralises
// the pattern with a single subscribe/unsubscribe lifecycle and a try/catch
// that routes thrown exceptions through the caller-supplied logger (falling
// back to UnityEngine.Debug.LogException) so the tick body can't kill the
// watcher.

using System;
using UnityEditor;
using UmeVrcfQol.Internal.Logging;

namespace UmeVrcfQol.Internal.Utilities {

    public sealed class WkEditorTicker {

        private readonly double _interval;
        private readonly Action _onTick;
        private readonly string _debugName;
        private readonly WkLogger _errorLogger;
        private double _nextTime;
        private bool _running;
        private bool _inTick;

        /// <summary>
        /// Construct a ticker that invokes <paramref name="onTick"/> at
        /// most once every <paramref name="intervalSeconds"/> while
        /// <see cref="Start"/> is active. <paramref name="debugName"/> is
        /// included in exception logs to identify which ticker threw.
        /// <paramref name="errorLogger"/> receives exceptions thrown by
        /// the tick body; when null the ticker falls back to the first
        /// registered <see cref="WkLogger"/> on <see cref="WkLoggerRegistry"/>,
        /// and finally to <see cref="UnityEngine.Debug.LogException(Exception)"/>
        /// so the exception never silently disappears.
        /// </summary>
        public WkEditorTicker(double intervalSeconds, Action onTick, WkLogger errorLogger = null, string debugName = null) {
            _interval = intervalSeconds > 0 ? intervalSeconds : 0.1;
            _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
            _errorLogger = errorLogger;
            _debugName = string.IsNullOrEmpty(debugName) ? "WkEditorTicker" : debugName;
        }

        /// <summary>Last time (in EditorApplication.timeSinceStartup units) that the tick body returned.</summary>
        public double LastRunUnscaledTime { get; private set; }

        /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
        public bool IsRunning => _running;

        /// <summary>
        /// Subscribe to <see cref="EditorApplication.update"/>. Idempotent.
        /// </summary>
        public void Start() {
            if (_running) return;
            _running = true;
            _nextTime = EditorApplication.timeSinceStartup + _interval;
            EditorApplication.update += Pump;
        }

        /// <summary>Unsubscribe. Idempotent.</summary>
        public void Stop() {
            if (!_running) return;
            _running = false;
            EditorApplication.update -= Pump;
        }

        /// <summary>
        /// Invoke the tick body immediately (resets the interval timer).
        /// Re-entrant calls are dropped silently.
        /// </summary>
        public void RunNow() {
            if (_inTick) return;
            InvokeBody();
            _nextTime = EditorApplication.timeSinceStartup + _interval;
        }

        private void Pump() {
            if (!_running) return;
            if (_inTick) return;
            if (EditorApplication.timeSinceStartup < _nextTime) return;
            InvokeBody();
            _nextTime = EditorApplication.timeSinceStartup + _interval;
        }

        private void InvokeBody() {
            _inTick = true;
            try {
                _onTick();
                LastRunUnscaledTime = EditorApplication.timeSinceStartup;
            } catch (Exception ex) {
                var logger = _errorLogger ?? FirstRegisteredLogger();
                if (logger != null) logger.Exception(ex, $"{_debugName} tick body threw");
                else UnityEngine.Debug.LogException(ex);
            } finally {
                _inTick = false;
            }
        }

        private static WkLogger FirstRegisteredLogger() {
            var all = WkLoggerRegistry.All();
            return all != null && all.Length > 0 ? all[0] : null;
        }
    }
}
