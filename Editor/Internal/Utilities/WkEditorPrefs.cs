#nullable enable

using UnityEditor;

namespace UmeVrcfQol.Internal.Utilities
{
    public static class WkEditorPrefs
    {
        private static string Key(string packageId, string key)
        {
            return string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(key) ? key : packageId + "." + key;
        }

        public static bool GetBool(string packageId, string key, bool fallback = false)
        {
            return EditorPrefs.GetBool(Key(packageId, key), fallback);
        }

        public static void SetBool(string packageId, string key, bool value)
        {
            EditorPrefs.SetBool(Key(packageId, key), value);
        }

        public static int GetInt(string packageId, string key, int fallback = 0)
        {
            return EditorPrefs.GetInt(Key(packageId, key), fallback);
        }

        public static void SetInt(string packageId, string key, int value)
        {
            EditorPrefs.SetInt(Key(packageId, key), value);
        }

        public static float GetFloat(string packageId, string key, float fallback = 0f)
        {
            return EditorPrefs.GetFloat(Key(packageId, key), fallback);
        }

        public static void SetFloat(string packageId, string key, float value)
        {
            EditorPrefs.SetFloat(Key(packageId, key), value);
        }

        public static string GetString(string packageId, string key, string fallback = "")
        {
            return EditorPrefs.GetString(Key(packageId, key), fallback);
        }

        public static void SetString(string packageId, string key, string value)
        {
            EditorPrefs.SetString(Key(packageId, key), value);
        }

        public static void Delete(string packageId, string key)
        {
            EditorPrefs.DeleteKey(Key(packageId, key));
        }

        public static bool Has(string packageId, string key)
        {
            return EditorPrefs.HasKey(Key(packageId, key));
        }
    }
}
