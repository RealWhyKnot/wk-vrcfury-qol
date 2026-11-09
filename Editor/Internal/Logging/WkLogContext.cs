#nullable enable

using System;
using System.Collections.Generic;

namespace UmeVrcfQol.Internal.Logging
{
    public static class WkLogContext
    {
        [ThreadStatic] private static List<string>? ScopeStack;
        [ThreadStatic] private static List<UnityEngine.Object>? ContextStack;

        private static List<string> Scopes => ScopeStack ??= new List<string>();
        private static List<UnityEngine.Object> Objects => ContextStack ??= new List<UnityEngine.Object>();

        public static IDisposable Scope(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return NoopScope.Instance;
            }

            Scopes.Add(label);
            return new ScopePop(Scopes.Count - 1);
        }

        public static IDisposable WithContextObject(UnityEngine.Object context)
        {
            if (context == null)
            {
                return NoopScope.Instance;
            }

            Objects.Add(context);
            return new ObjectPop(Objects.Count - 1);
        }

        public static IReadOnlyList<string> CurrentScopes => Scopes;

        public static UnityEngine.Object? CurrentContextObject
        {
            get
            {
                List<UnityEngine.Object> list = Objects;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] != null)
                    {
                        return list[i];
                    }
                }
                return null;
            }
        }

        internal static string FormatScopePrefix()
        {
            List<string> list = Scopes;
            if (list.Count == 0)
            {
                return string.Empty;
            }

            if (list.Count == 1)
            {
                return "[" + list[0] + "] ";
            }

            var sb = new System.Text.StringBuilder("[", 32);
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" > ");
                }

                sb.Append(list[i]);
            }
            sb.Append("] ");
            return sb.ToString();
        }

        private sealed class ScopePop : IDisposable
        {
            private readonly int _index;
            private bool _disposed;
            public ScopePop(int index) { _index = index; }
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                List<string> list = Scopes;
                while (list.Count > _index)
                {
                    list.RemoveAt(list.Count - 1);
                }
            }
        }

        private sealed class ObjectPop : IDisposable
        {
            private readonly int _index;
            private bool _disposed;
            public ObjectPop(int index) { _index = index; }
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                List<UnityEngine.Object> list = Objects;
                while (list.Count > _index)
                {
                    list.RemoveAt(list.Count - 1);
                }
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
