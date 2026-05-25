// WkLogContext.cs
//
// Scope-stack + context-object stack used by WkLogger to attach
// "where in the pipeline did this come from" metadata to log lines
// without each call site threading the context through manually.
//
// Shape modelled after NDMF's ErrorReport (ReferenceStackScope +
// WithContextObject) since the use case is identical: a build pipeline
// fires dozens of log lines per pass, and the relevant attribution
// (which pass, which avatar, which component) belongs in the line
// without the caller spelling it out every time.
//
// Both stacks are thread-static so a future background-task pattern
// can use them without leaking scope between threads. In Editor-only
// code today only the main thread exercises them, but the [ThreadStatic]
// attribute is cheaper than a lock when uncontended and removes a
// foot-gun for callers that experiment with threading.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UmeVrcfQol.Internal.Logging {

    public static class WkLogContext {

        [ThreadStatic] private static List<string> _scopeStack;
        [ThreadStatic] private static List<UnityEngine.Object> _contextStack;

        private static List<string> Scopes  => _scopeStack  ??= new List<string>();
        private static List<UnityEngine.Object> Objects => _contextStack ??= new List<UnityEngine.Object>();

        /// <summary>
        /// Push <paramref name="label"/> onto the scope stack. Pops on
        /// dispose. Idiomatic usage:
        ///   using (WkLogContext.Scope("BoneMerger")) {
        ///       logger.Info("scan complete");   // file line: "[BoneMerger] scan complete"
        ///   }
        /// </summary>
        public static IDisposable Scope(string label) {
            if (string.IsNullOrEmpty(label)) return NoopScope.Instance;
            Scopes.Add(label);
            return new ScopePop(Scopes.Count - 1);
        }

        /// <summary>
        /// Push <paramref name="context"/> onto the context-object stack.
        /// While active, log lines record the object's path and the Unity
        /// Console mirror uses the two-arg LogXxx variants so clicking
        /// the console line pings the object in the Hierarchy.
        /// </summary>
        public static IDisposable WithContextObject(UnityEngine.Object context) {
            if (context == null) return NoopScope.Instance;
            Objects.Add(context);
            return new ObjectPop(Objects.Count - 1);
        }

        /// <summary>Outermost-first list of active scope labels. Empty when no scope is active.</summary>
        public static IReadOnlyList<string> CurrentScopes => Scopes;

        /// <summary>Most-recent non-null context object on the stack, or null when none is active.</summary>
        public static UnityEngine.Object CurrentContextObject {
            get {
                var list = Objects;
                for (int i = list.Count - 1; i >= 0; i--) {
                    if (list[i] != null) return list[i];
                }
                return null;
            }
        }

        /// <summary>
        /// Compose the active scopes into a single bracketed prefix for
        /// the log line ("[outer > inner] "). Returns the empty string
        /// when no scope is active.
        /// </summary>
        internal static string FormatScopePrefix() {
            var list = Scopes;
            if (list.Count == 0) return string.Empty;
            if (list.Count == 1) return "[" + list[0] + "] ";
            var sb = new System.Text.StringBuilder("[", 32);
            for (int i = 0; i < list.Count; i++) {
                if (i > 0) sb.Append(" > ");
                sb.Append(list[i]);
            }
            sb.Append("] ");
            return sb.ToString();
        }

        private sealed class ScopePop : IDisposable {
            private readonly int _index;
            private bool _disposed;
            public ScopePop(int index) { _index = index; }
            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                var list = Scopes;
                // Pop everything down to and including our index. Out-of-order
                // disposes (rare) still leave the stack consistent.
                while (list.Count > _index) list.RemoveAt(list.Count - 1);
            }
        }

        private sealed class ObjectPop : IDisposable {
            private readonly int _index;
            private bool _disposed;
            public ObjectPop(int index) { _index = index; }
            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                var list = Objects;
                while (list.Count > _index) list.RemoveAt(list.Count - 1);
            }
        }

        private sealed class NoopScope : IDisposable {
            public static readonly NoopScope Instance = new NoopScope();
            public void Dispose() { }
        }
    }
}
