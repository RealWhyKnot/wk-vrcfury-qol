// WkEditorPrefs.cs
//
// Typed wrapper over EditorPrefs that prefixes every key with the caller's
// package id so two WhyKnot packages installed in the same Unity project
// cannot collide on key namespace. Today each tool that uses EditorPrefs
// invents its own prefix convention, and the prefixes have drifted apart;
// this gives one source of truth.
//
// WkSessionState handles the equivalent for SessionState -- foldout
// expanded/collapsed state, last-selected tab, anything that should
// reset on Unity restart but persist across domain reloads in the same
// session.

using UnityEditor;

namespace UmeVrcfQol.Internal.Utilities {

    public static class WkEditorPrefs {

        private static string Key(string packageId, string key) {
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(key)) return key;
            return packageId + "." + key;
        }

        public static bool   GetBool  (string packageId, string key, bool   fallback = false) => EditorPrefs.GetBool  (Key(packageId, key), fallback);
        public static void   SetBool  (string packageId, string key, bool   value)            => EditorPrefs.SetBool  (Key(packageId, key), value);
        public static int    GetInt   (string packageId, string key, int    fallback = 0)     => EditorPrefs.GetInt   (Key(packageId, key), fallback);
        public static void   SetInt   (string packageId, string key, int    value)            => EditorPrefs.SetInt   (Key(packageId, key), value);
        public static float  GetFloat (string packageId, string key, float  fallback = 0f)    => EditorPrefs.GetFloat (Key(packageId, key), fallback);
        public static void   SetFloat (string packageId, string key, float  value)            => EditorPrefs.SetFloat (Key(packageId, key), value);
        public static string GetString(string packageId, string key, string fallback = "")    => EditorPrefs.GetString(Key(packageId, key), fallback);
        public static void   SetString(string packageId, string key, string value)            => EditorPrefs.SetString(Key(packageId, key), value);

        public static void   Delete   (string packageId, string key)                          => EditorPrefs.DeleteKey(Key(packageId, key));
        public static bool   Has      (string packageId, string key)                          => EditorPrefs.HasKey   (Key(packageId, key));
    }

    /// <summary>
    /// SessionState-backed expand/collapse helper for foldouts and other
    /// UI state that should survive domain reload but reset on Unity
    /// restart. Keys are caller-supplied; prefix them yourself if you
    /// need package-scoping (SessionState is editor-session-scoped, so
    /// the prefix collision risk is lower than EditorPrefs).
    /// </summary>
    public static class WkSessionState {

        public static bool Foldout(string scopeKey, bool defaultOpen) {
            if (string.IsNullOrEmpty(scopeKey)) return defaultOpen;
            return SessionState.GetBool(scopeKey, defaultOpen);
        }

        public static void SetFoldout(string scopeKey, bool open) {
            if (string.IsNullOrEmpty(scopeKey)) return;
            SessionState.SetBool(scopeKey, open);
        }
    }
}
