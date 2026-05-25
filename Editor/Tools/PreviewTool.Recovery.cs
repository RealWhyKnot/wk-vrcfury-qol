// PreviewTool.Recovery.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmeVrcfQol.Tools {

    internal static partial class PreviewTool {

        private static void CleanupAbandonedPreviewClones() {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()) {
                if (go == null || string.IsNullOrEmpty(go.name)) continue;
                if (!go.name.StartsWith(PreviewPrefix, StringComparison.Ordinal)) continue;
                if (!EditorUtility.IsPersistent(go)) Object.DestroyImmediate(go);
            }
        }

        private static void RememberPreview(PreviewSession session) {
            SessionState.SetInt(SessionPreviewId, session.CloneRoot != null ? session.CloneRoot.GetInstanceID() : 0);
            SessionState.SetInt(SessionSourceId, session.SourceRoot != null ? session.SourceRoot.GetInstanceID() : 0);
            SessionState.SetBool(SessionSourceWasHidden, session.SourceWasHidden);
            StoreSourceForCrashRecovery(session.SourceRoot, session.SourceWasHidden);
        }

        private static void ForgetPreview() {
            SessionState.EraseInt(SessionPreviewId);
            SessionState.EraseInt(SessionSourceId);
            SessionState.EraseBool(SessionSourceWasHidden);
            EditorPrefs.DeleteKey(PrefsSourceGlobalId);
            EditorPrefs.DeleteKey(PrefsSourceWasHidden);
        }

        private static void RestoreAbandonedPreviewState() {
            var hasSourceState = SessionState.GetInt(SessionSourceId, 0) != 0 ||
                EditorPrefs.HasKey(PrefsSourceGlobalId);
            var source = ResolveRememberedSource();
            var clone = EditorUtility.InstanceIDToObject(SessionState.GetInt(SessionPreviewId, 0)) as GameObject;
            if (source == null && hasSourceState && EditorPrefs.HasKey(PrefsSourceGlobalId)) {
                CleanupAbandonedPreviewClones();
                return;
            }

            var sourceWasHidden = SessionState.GetBool(
                SessionSourceWasHidden,
                EditorPrefs.GetBool(PrefsSourceWasHidden, false));
            RestoreSourceVisibility(source, sourceWasHidden);
            if (clone != null && !EditorUtility.IsPersistent(clone)) Object.DestroyImmediate(clone);
            CleanupAbandonedPreviewClones();
            _active = null;
            ForgetPreview();
        }

        private static void HideSourceAvatar(GameObject source) {
            if (source == null) return;
            SceneVisibilityManager.instance.Hide(source, true);
        }

        private static void RestoreSourceVisibility(GameObject source, bool sourceWasHidden) {
            if (source == null || sourceWasHidden) return;
            SceneVisibilityManager.instance.Show(source, true);
        }

        private static bool IsSceneHidden(GameObject source) {
            return source != null && SceneVisibilityManager.instance.IsHidden(source);
        }

        private static void StoreSourceForCrashRecovery(GameObject source, bool sourceWasHidden) {
            if (source == null) return;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
            if (string.IsNullOrEmpty(id)) return;
            EditorPrefs.SetString(PrefsSourceGlobalId, id);
            EditorPrefs.SetBool(PrefsSourceWasHidden, sourceWasHidden);
        }

        private static GameObject ResolveRememberedSource() {
            var source = EditorUtility.InstanceIDToObject(SessionState.GetInt(SessionSourceId, 0)) as GameObject;
            if (source != null) return source;

            var idText = EditorPrefs.GetString(PrefsSourceGlobalId, string.Empty);
            if (string.IsNullOrEmpty(idText)) return null;
            if (!GlobalObjectId.TryParse(idText, out var id)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        }

        private sealed class PreviewSession {
            public Component SourceComponent;
            public GameObject SourceRoot;
            public GameObject CloneRoot;
            public string Title;
            public string ShortLabel;
            public int PageIndex;
            public bool SourceWasHidden;
            public bool HasFocusBounds;
            public Bounds FocusBounds;
            public Object[] PreviousSelection;
        }
    }
}
