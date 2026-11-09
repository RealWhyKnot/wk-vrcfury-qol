#nullable enable

using UnityEditor;

namespace UmeVrcfQol.Internal.Utilities
{
    public static class WkSessionState
    {
        public static bool Foldout(string scopeKey, bool defaultOpen)
        {
            return string.IsNullOrEmpty(scopeKey) ? defaultOpen : SessionState.GetBool(scopeKey, defaultOpen);
        }

        public static void SetFoldout(string scopeKey, bool open)
        {
            if (string.IsNullOrEmpty(scopeKey))
            {
                return;
            }

            SessionState.SetBool(scopeKey, open);
        }
    }
}
