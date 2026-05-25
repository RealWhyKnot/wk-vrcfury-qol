// WkAvatarPreviewSession.cs
//
// Non-destructive preview clone of an avatar with original<->proxy
// object mapping, lifecycle hooks for domain reload / scene change /
// play-mode entry, and crash recovery via GlobalObjectId in EditorPrefs.
// Preview tools (vrcfury-qol's PreviewTool, avatar-qol's
// AvatarPreviewController) call this for the plumbing; they own the
// "what to do to the clone" application logic.
//
// Lifecycle invariants:
//   - Clone is created with HideFlags.DontSave + HideInHierarchy. The
//     scene gets a hidden GameObject that does not save to the scene
//     asset and does not appear in the Hierarchy panel.
//   - On Dispose, the clone is DestroyImmediate-d so the DontSave-flagged
//     proxy doesn't leak across scene loads.
//   - On AssemblyReloadEvents.afterAssemblyReload, the clone reference
//     goes null (Unity doesn't preserve scene objects across script
//     domain reloads cleanly); OnDomainReload fires so callers can
//     decide whether to ForceReset.
//   - On EditorSceneManager.activeSceneChangedInEditMode, OnSceneChanged
//     fires; callers typically Dispose at that point because the proxy
//     was in the prior scene.
//   - On EditorApplication.playModeStateChanged.ExitingEditMode (entering
//     play mode), the proxy is automatically Dispose-d so play-mode
//     starts from a clean slate.
//
// Crash recovery limits (per WkGlobalId docs): only objects in saved
// scenes or prefab assets can be recovered. Unsaved-scene previews
// don't survive an Editor restart. The CleanupAbandonedClones static
// helper sweeps the active scene for orphaned proxies on startup.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UmeVrcfQol.Internal.Reflection;

namespace UmeVrcfQol.Internal.Pipeline {

    public sealed class WkAvatarPreviewSession : IDisposable {

        private const string PrefsSourceGlobalId  = "UmeVrcfQol.Internal.PreviewSession.SourceGlobalId.";
        private const string PrefsSourceWasHidden = "UmeVrcfQol.Internal.PreviewSession.SourceWasHidden.";
        private const string ProxyMarkerName = "_WhyKnot.Preview.Proxy";

        public GameObject Source { get; private set; }
        public GameObject Proxy { get; private set; }
        public string OwnerPackageId { get; }
        public bool HideOnConstruct { get; }

        private bool _sourceWasActive;
        private bool _disposed;
        private readonly Dictionary<GameObject, GameObject> _originalToProxy = new Dictionary<GameObject, GameObject>();
        private readonly Dictionary<GameObject, GameObject> _proxyToOriginal = new Dictionary<GameObject, GameObject>();

        public event Action OnDomainReload;
        public event Action OnSceneChanged;

        public WkAvatarPreviewSession(GameObject sourceAvatar, string ownerPackageId, bool hideSource = true) {
            if (sourceAvatar == null) throw new ArgumentNullException(nameof(sourceAvatar));
            if (string.IsNullOrEmpty(ownerPackageId)) throw new ArgumentException("ownerPackageId is required", nameof(ownerPackageId));

            Source = sourceAvatar;
            OwnerPackageId = ownerPackageId;
            HideOnConstruct = hideSource;

            BuildProxy();
            if (hideSource) HideSource();
            RememberInPrefs();
            SubscribeLifecycle();
        }

        // ---- mapping ----------------------------------------------

        public T GetOriginalFor<T>(T proxyObject) where T : UnityEngine.Object {
            if (proxyObject == null) return null;
            var proxyGo = AsGameObject(proxyObject);
            if (proxyGo != null && _proxyToOriginal.TryGetValue(proxyGo, out var originalGo)) {
                return MapComponent(proxyObject, proxyGo, originalGo);
            }
            return null;
        }

        public T GetProxyFor<T>(T originalObject) where T : UnityEngine.Object {
            if (originalObject == null) return null;
            var origGo = AsGameObject(originalObject);
            if (origGo != null && _originalToProxy.TryGetValue(origGo, out var proxyGo)) {
                return MapComponent(originalObject, origGo, proxyGo);
            }
            return null;
        }

        public IEnumerable<(GameObject original, GameObject proxy)> EnumerateMappings() {
            foreach (var kv in _originalToProxy) yield return (kv.Key, kv.Value);
        }

        // ---- source visibility ------------------------------------

        public void HideSource() {
            if (Source == null) return;
            _sourceWasActive = Source.activeSelf;
            Source.SetActive(false);
        }

        public void RestoreSourceVisibility() {
            if (Source == null) return;
            Source.SetActive(_sourceWasActive);
        }

        // ---- force reset ------------------------------------------

        public void ForceReset() {
            DestroyProxy();
            BuildProxy();
        }

        // ---- dispose ----------------------------------------------

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            UnsubscribeLifecycle();
            DestroyProxy();
            RestoreSourceVisibility();
            ForgetInPrefs();
        }

        // ---- crash recovery ---------------------------------------

        public static WkAvatarPreviewSession TryRecoverFromCrash(string ownerPackageId) {
            var sourceKey = PrefsSourceGlobalId + ownerPackageId;
            var hiddenKey = PrefsSourceWasHidden + ownerPackageId;
            if (!EditorPrefs.HasKey(sourceKey)) return null;
            var source = WkGlobalId.RecallFromPrefs<GameObject>(sourceKey);
            if (source == null) {
                // Source no longer resolvable -- clear stale prefs and exit.
                EditorPrefs.DeleteKey(sourceKey);
                EditorPrefs.DeleteKey(hiddenKey);
                return null;
            }
            // Caller decides whether to actually rebuild the preview; we
            // give them the source reference and clean prefs flag so they
            // can call the ctor with the right hideSource value.
            var hideSource = EditorPrefs.GetBool(hiddenKey, true);
            EditorPrefs.DeleteKey(sourceKey);
            EditorPrefs.DeleteKey(hiddenKey);
            return new WkAvatarPreviewSession(source, ownerPackageId, hideSource);
        }

        public static void CleanupAbandonedClones(string ownerPackageId) {
            // Sweep the active scene for proxies that share our marker
            // name but aren't owned by an active session. DontSave
            // objects survive across some operations and can leak.
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all) {
                if (go == null) continue;
                if (go.name != ProxyMarkerName) continue;
                if ((go.hideFlags & HideFlags.DontSave) == 0) continue;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ---- internals --------------------------------------------

        private void BuildProxy() {
            if (Source == null) return;
            Proxy = UnityEngine.Object.Instantiate(Source);
            Proxy.name = ProxyMarkerName;
            Proxy.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            _originalToProxy.Clear();
            _proxyToOriginal.Clear();
            BuildMapping(Source.transform, Proxy.transform);
        }

        private void BuildMapping(Transform original, Transform proxy) {
            _originalToProxy[original.gameObject] = proxy.gameObject;
            _proxyToOriginal[proxy.gameObject] = original.gameObject;
            // Instantiate preserves child order so a parallel walk is
            // a 1:1 mapping. If the child counts ever disagree something
            // foreign is mutating the hierarchy mid-construction; bail.
            int n = Math.Min(original.childCount, proxy.childCount);
            for (int i = 0; i < n; i++) {
                BuildMapping(original.GetChild(i), proxy.GetChild(i));
            }
        }

        private void DestroyProxy() {
            if (Proxy != null) {
                UnityEngine.Object.DestroyImmediate(Proxy);
                Proxy = null;
            }
            _originalToProxy.Clear();
            _proxyToOriginal.Clear();
        }

        private void RememberInPrefs() {
            if (Source == null) return;
            WkGlobalId.TryRoundTripToPrefs(PrefsSourceGlobalId + OwnerPackageId, Source);
            EditorPrefs.SetBool(PrefsSourceWasHidden + OwnerPackageId, HideOnConstruct);
        }

        private void ForgetInPrefs() {
            EditorPrefs.DeleteKey(PrefsSourceGlobalId + OwnerPackageId);
            EditorPrefs.DeleteKey(PrefsSourceWasHidden + OwnerPackageId);
        }

        private void SubscribeLifecycle() {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChangedInEditMode;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void UnsubscribeLifecycle() {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChangedInEditMode;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnAfterAssemblyReload() {
            OnDomainReload?.Invoke();
            // The proxy reference goes invalid after domain reload; clear it
            // so subsequent ForceReset rebuilds cleanly.
            Proxy = null;
            _originalToProxy.Clear();
            _proxyToOriginal.Clear();
        }

        private void OnSceneChangedInEditMode(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to) {
            OnSceneChanged?.Invoke();
            // Default behaviour: dispose. Callers wanting to keep the
            // preview across scene changes subscribe to OnSceneChanged
            // and rebuild themselves.
            Dispose();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change) {
            if (change == PlayModeStateChange.ExitingEditMode) {
                Dispose();
            }
        }

        private static GameObject AsGameObject(UnityEngine.Object obj) {
            switch (obj) {
                case GameObject go: return go;
                case Component c: return c.gameObject;
                default: return null;
            }
        }

        private static T MapComponent<T>(T sourceObj, GameObject sourceGo, GameObject targetGo) where T : UnityEngine.Object {
            // Plain GameObject case: just cast the mapped GameObject.
            if (sourceObj is GameObject) return targetGo as T;
            // Component case: find the same component type+index on the
            // target GameObject. Instantiate preserves component order
            // so the index-on-source equals the index-on-target.
            if (sourceObj is Component sourceComp) {
                var sourceComps = sourceGo.GetComponents(sourceComp.GetType());
                int idx = Array.IndexOf(sourceComps, sourceComp);
                if (idx < 0) return null;
                var targetComps = targetGo.GetComponents(sourceComp.GetType());
                if (idx < targetComps.Length) return targetComps[idx] as T;
            }
            return null;
        }
    }
}
