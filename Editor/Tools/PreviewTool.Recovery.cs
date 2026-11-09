#nullable enable

using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools
{
    internal static partial class PreviewTool
    {
        private static void CleanupAbandonedPreviewClones()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || string.IsNullOrEmpty(go.name))
                {
                    continue;
                }

                if (!go.name.StartsWith(PreviewPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!EditorUtility.IsPersistent(go))
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void RememberPreview(PreviewSession session)
        {
            StoreSourceForCrashRecovery(session.SourceRoot, session.SourceWasHidden);
            SessionState.SetInt(SessionPreviewId, session.CloneRoot != null ? session.CloneRoot.GetInstanceID() : 0);
            SessionState.SetInt(SessionSourceId, session.SourceRoot != null ? session.SourceRoot.GetInstanceID() : 0);
            SessionState.SetBool(SessionSourceWasHidden, session.SourceWasHidden);
        }

        private static void ForgetPreview()
        {
            SessionState.EraseInt(SessionPreviewId);
            SessionState.EraseInt(SessionSourceId);
            SessionState.EraseBool(SessionSourceWasHidden);
            EditorPrefs.DeleteKey(PrefsSourceGlobalId);
            EditorPrefs.DeleteKey(PrefsSourceWasHidden);
        }

        private static void RestoreAbandonedPreviewState()
        {
            bool hasSourceState = SessionState.GetInt(SessionSourceId, 0) != 0 ||
                EditorPrefs.HasKey(PrefsSourceGlobalId);
            GameObject? source = ResolveRememberedSource();
            var clone = EditorUtility.InstanceIDToObject(SessionState.GetInt(SessionPreviewId, 0)) as GameObject;
            if (source == null && hasSourceState && EditorPrefs.HasKey(PrefsSourceGlobalId))
            {
                CleanupAbandonedPreviewClones();
                return;
            }

            bool sourceWasHidden = SessionState.GetBool(
                SessionSourceWasHidden,
                EditorPrefs.GetBool(PrefsSourceWasHidden, defaultValue: false));
            RestoreSourceVisibility(source, sourceWasHidden);
            if (clone != null && !EditorUtility.IsPersistent(clone))
            {
                Object.DestroyImmediate(clone);
            }

            CleanupAbandonedPreviewClones();
            ActiveSession = null;
            ForgetPreview();
        }

        private static void HideSourceAvatar(GameObject source)
        {
            if (source == null)
            {
                return;
            }

            SceneVisibilityManager.instance.Hide(source, includeDescendants: true);
        }

        private static void RestoreSourceVisibility(GameObject? source, bool sourceWasHidden)
        {
            if (source == null || sourceWasHidden)
            {
                return;
            }

            SceneVisibilityManager.instance.Show(source, includeDescendants: true);
        }

        private static bool IsSceneHidden(GameObject source)
        {
            return source != null && SceneVisibilityManager.instance.IsHidden(source);
        }

        private static void StoreSourceForCrashRecovery(GameObject source, bool sourceWasHidden)
        {
            if (source == null)
            {
                return;
            }

            string id = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            EditorPrefs.SetString(PrefsSourceGlobalId, id);
            EditorPrefs.SetBool(PrefsSourceWasHidden, sourceWasHidden);
        }

        private static GameObject? ResolveRememberedSource()
        {
            var source = EditorUtility.InstanceIDToObject(SessionState.GetInt(SessionSourceId, 0)) as GameObject;
            if (source != null)
            {
                return source;
            }

            string idText = EditorPrefs.GetString(PrefsSourceGlobalId, string.Empty);
            return string.IsNullOrEmpty(idText) || !GlobalObjectId.TryParse(idText, out GlobalObjectId id)
                ? null
                : GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        }

        private sealed class PreviewSession
        {
            public readonly Component SourceComponent;
            public readonly GameObject SourceRoot;
            public readonly GameObject CloneRoot;
            public readonly string Title;
            public readonly string ShortLabel;
            public int PageIndex;
            public bool SourceWasHidden;
            public bool HasFocusBounds;
            public Bounds FocusBounds;
            public Object[] PreviousSelection = Array.Empty<Object>();

            public PreviewSession(Component sourceComponent, GameObject sourceRoot, GameObject cloneRoot, string title, string shortLabel)
            {
                SourceComponent = sourceComponent;
                SourceRoot = sourceRoot;
                CloneRoot = cloneRoot;
                Title = title;
                ShortLabel = shortLabel;
            }
        }
    }
}
