// WkGlobalId.cs
//
// Thin null-safe wrapper around UnityEditor.GlobalObjectId for the
// "serialise an Object reference to a string, recover it later" pattern.
// Used by every preview tool that needs to survive a domain reload or
// Unity restart and still remember which avatar / component the user
// was looking at.
//
// IMPORTANT: GlobalObjectId only resolves reliably for objects in saved
// scenes or in prefab assets. Objects in unsaved scenes have a null
// scene GUID and won't round-trip. Moving an object between scenes
// changes its GlobalObjectId. GlobalObjectId.TryParse only proves the
// string parsed as a well-formed identifier -- not that resolution will
// succeed. Callers should treat TryResolve's return value as the truth,
// not TryParse's, and degrade gracefully when round-trip fails.

using UnityEditor;
using UnityEngine;

namespace UmeVrcfQol.Internal.Reflection {

    public static class WkGlobalId {

        /// <summary>
        /// Serialise <paramref name="obj"/> to a GlobalObjectId string.
        /// Returns null when <paramref name="obj"/> is null. The result
        /// can be stored in EditorPrefs / SessionState and round-tripped
        /// through <see cref="TryResolve{T}"/> in a later session.
        /// </summary>
        public static string Stringify(Object obj) {
            if (obj == null) return null;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            var text = id.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        /// <summary>
        /// Resolve <paramref name="serialized"/> back to a typed Object.
        /// Returns true + the object on full success; false + null on
        /// any failure (malformed string, owning scene not loaded, object
        /// destroyed, type mismatch on cast). Does not throw.
        /// </summary>
        public static bool TryResolve<T>(string serialized, out T obj) where T : Object {
            obj = null;
            if (string.IsNullOrEmpty(serialized)) return false;
            if (!GlobalObjectId.TryParse(serialized, out var id)) return false;
            var raw = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            obj = raw as T;
            return obj != null;
        }

        /// <summary>
        /// Write <paramref name="obj"/>'s GlobalObjectId to
        /// <paramref name="prefsKey"/>, or delete the key when
        /// <paramref name="obj"/> is null. Useful as the symmetric inverse
        /// of <see cref="RecallFromPrefs{T}"/>.
        /// </summary>
        public static bool TryRoundTripToPrefs(string prefsKey, Object obj) {
            if (string.IsNullOrEmpty(prefsKey)) return false;
            if (obj == null) {
                EditorPrefs.DeleteKey(prefsKey);
                return true;
            }
            var text = Stringify(obj);
            if (string.IsNullOrEmpty(text)) return false;
            EditorPrefs.SetString(prefsKey, text);
            return true;
        }

        /// <summary>
        /// Read a GlobalObjectId string previously written by
        /// <see cref="TryRoundTripToPrefs"/> and resolve it back to a
        /// typed Object. Returns null when the key is absent or the
        /// resolve fails for any reason.
        /// </summary>
        public static T RecallFromPrefs<T>(string prefsKey) where T : Object {
            if (string.IsNullOrEmpty(prefsKey)) return null;
            if (!EditorPrefs.HasKey(prefsKey)) return null;
            var text = EditorPrefs.GetString(prefsKey);
            return TryResolve<T>(text, out var obj) ? obj : null;
        }
    }
}
