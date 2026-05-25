// WkReflection.cs
//
// Small, allocation-free reflection helpers for the patterns every tool
// that pokes at a foreign package's internals reinvents: find a field
// across an inheritance chain, read a typed value with a fallback when
// the field is missing or the wrong type, walk a Unity-style serialized
// property path through a raw object graph, and resolve an assembly by
// short name.
//
// These intentionally do not cache MemberInfo lookups -- caching belongs
// to the higher-level WkReflectionCache abstraction so the lifetime is
// tied to "the foreign package I'm probing." A bare GetField every call
// is fast enough for the call sites we have, and the no-cache shape
// avoids the "stale handle after assembly reload" footgun.

using System;
using System.Reflection;

namespace UmeVrcfQol.Internal.Reflection {

    public static class WkReflection {

        /// <summary>
        /// Default binding flags for "any instance member" -- public or
        /// non-public, only on the declaring type (use <see cref="FindField"/>
        /// for inheritance-aware lookup).
        /// </summary>
        public const BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Walk <paramref name="type"/>'s inheritance chain looking for a
        /// field named <paramref name="name"/>. Returns the first match
        /// found, or null when the chain is exhausted. Uses
        /// <see cref="BindingFlags.DeclaredOnly"/> per level so a private
        /// field on a base class is reachable even when the derived type
        /// declares no field of the same name.
        /// </summary>
        public static FieldInfo FindField(Type type, string name, BindingFlags flags = AllInstance) {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            var declaredFlags = flags | BindingFlags.DeclaredOnly;
            while (type != null) {
                var f = type.GetField(name, declaredFlags);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }

        /// <summary>
        /// Read <paramref name="name"/> from <paramref name="target"/>'s
        /// runtime type. Returns <paramref name="fallback"/> on any failure:
        /// null target, missing field, type mismatch on cast, or thrown
        /// exception from the getter. Useful for "read a field if it exists,
        /// otherwise act as if the value were the default."
        /// </summary>
        public static T GetFieldValue<T>(object target, string name, T fallback = default) {
            if (target == null || string.IsNullOrEmpty(name)) return fallback;
            try {
                var field = FindField(target.GetType(), name);
                if (field == null) return fallback;
                var raw = field.GetValue(target);
                if (raw is T typed) return typed;
                if (raw == null) return fallback;
                // Handle numeric conversion paths (int->float etc) when T
                // is a primitive and raw is convertible to it.
                if (typeof(T).IsPrimitive) {
                    return (T) Convert.ChangeType(raw, typeof(T));
                }
                return fallback;
            } catch {
                return fallback;
            }
        }

        /// <summary>
        /// Walk a Unity-serialised property path (the same shape
        /// <see cref="UnityEditor.SerializedProperty.propertyPath"/> uses)
        /// through a raw object graph and return the value at the end.
        /// Handles dotted field access and ".Array.data[N]" list-element
        /// segments. Returns null when any segment fails to resolve.
        ///
        /// Limits (documented for callers that hit them): polymorphic
        /// SerializeReference fields, NonSerialized fields exposed only via
        /// SerializeField, and IList implementations that aren't arrays or
        /// generic List&lt;T&gt; are not supported and return null.
        /// </summary>
        public static object WalkPath(object root, string unityPropertyPath) {
            if (root == null || string.IsNullOrEmpty(unityPropertyPath)) return root;

            // Normalise ".Array.data[N]" to "[N]" so the segment parser is uniform.
            var normalised = System.Text.RegularExpressions.Regex.Replace(
                unityPropertyPath, @"\.Array\.data\[(\d+)\]", "[$1]");

            object cursor = root;
            int i = 0;
            while (i < normalised.Length && cursor != null) {
                if (normalised[i] == '.') { i++; continue; }

                if (normalised[i] == '[') {
                    int close = normalised.IndexOf(']', i);
                    if (close < 0) return null;
                    if (!int.TryParse(normalised.Substring(i + 1, close - i - 1), out var index)) return null;
                    cursor = IndexInto(cursor, index);
                    i = close + 1;
                    continue;
                }

                // Read a field name up to the next '.' or '['.
                int end = i;
                while (end < normalised.Length && normalised[end] != '.' && normalised[end] != '[') end++;
                var fieldName = normalised.Substring(i, end - i);
                var field = FindField(cursor.GetType(), fieldName);
                if (field == null) return null;
                cursor = field.GetValue(cursor);
                i = end;
            }
            return cursor;
        }

        private static object IndexInto(object container, int index) {
            if (container == null || index < 0) return null;
            if (container is Array array) {
                return index < array.Length ? array.GetValue(index) : null;
            }
            if (container is System.Collections.IList list) {
                return index < list.Count ? list[index] : null;
            }
            return null;
        }

        /// <summary>
        /// Resolve a loaded assembly by its short name (matches
        /// <see cref="AssemblyName.Name"/>, case-sensitive). Returns true
        /// + assembly when found; false + null otherwise.
        /// </summary>
        public static bool TryFindAssembly(string assemblyShortName, out Assembly assembly) {
            assembly = null;
            if (string.IsNullOrEmpty(assemblyShortName)) return false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                if (asm.GetName().Name == assemblyShortName) {
                    assembly = asm;
                    return true;
                }
            }
            return false;
        }
    }
}
