// WkRawSdkHook.cs
//
// Raw IVRCSDKPreprocessAvatarCallback wrapper for features that need
// SDK-callback semantics WITHOUT going through NDMF (rare; document
// the criteria in the subclass's XML doc). Most features should use
// WkAvatarPipeline + WkAvatarPass<TSession> which automatically routes
// through NDMF when available.
//
// Lifecycle:
//   OnPreprocessAvatar(go) -> CreateSession + RunOnAvatar (caller's logic)
//   OnPostprocessAvatar()  -> DisposeSession for the matching avatar
//
// Play-mode entry handled by WkRawSdkHookRegistry which subscribes to
// EditorApplication.playModeStateChanged. Active sessions are disposed
// on ExitingPlayMode so partial state never leaks across the boundary.

#if WK_VRC_SDK_AVATARS
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace UmeVrcfQol.Internal.Pipeline {

    public abstract class WkRawSdkHook<TSession>
        : IVRCSDKPreprocessAvatarCallback, IVRCSDKPostprocessAvatarCallback
        where TSession : class {

        public abstract int callbackOrder { get; }

        private readonly Dictionary<GameObject, TSession> _uploadSessions = new Dictionary<GameObject, TSession>();
        private static GameObject _activeUploadAvatar;

        protected abstract TSession CreateSession(GameObject avatarRoot, WkBuildMode mode);
        protected abstract bool RunOnAvatar(GameObject avatarRoot, TSession session, WkBuildMode mode);
        protected virtual void DisposeSession(TSession session, WkBuildMode mode) { }

        public bool OnPreprocessAvatar(GameObject avatarGameObject) {
            if (avatarGameObject == null) return true;
            _activeUploadAvatar = avatarGameObject;
            TSession session;
            try {
                session = CreateSession(avatarGameObject, WkBuildMode.Upload);
            } catch (Exception ex) {
                Debug.LogException(ex);
                return false;
            }
            _uploadSessions[avatarGameObject] = session;
            try {
                return RunOnAvatar(avatarGameObject, session, WkBuildMode.Upload);
            } catch (Exception ex) {
                Debug.LogException(ex);
                return false;
            }
        }

        public void OnPostprocessAvatar() {
            if (_activeUploadAvatar == null) return;
            if (_uploadSessions.TryGetValue(_activeUploadAvatar, out var s)) {
                try { DisposeSession(s, WkBuildMode.Upload); } catch (Exception ex) { Debug.LogException(ex); }
                _uploadSessions.Remove(_activeUploadAvatar);
            }
            _activeUploadAvatar = null;
        }

        internal void DisposeAllSessionsOnPlayModeExit() {
            foreach (var kv in _uploadSessions) {
                try { DisposeSession(kv.Value, WkBuildMode.Upload); } catch (Exception ex) { Debug.LogException(ex); }
            }
            _uploadSessions.Clear();
            _activeUploadAvatar = null;
        }
    }
}
#endif
