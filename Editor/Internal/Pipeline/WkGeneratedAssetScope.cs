// WkGeneratedAssetScope.cs
//
// Three-tier folder lifecycle for generated assets, modelled after
// NDMF's distinction between temporary build artefacts
// (Packages/.../__Generated, cleared after every build) and persistent
// manual-bake artefacts (Assets/ZZZ_GeneratedAssets, user-saved). The
// middle tier (Session) covers preview/iteration artefacts that
// survive domain reload but get cleared when the using-block exits.
//
//   Temporary  ->  Packages/<packageId>/__Generated/<containerKey>/
//                  ...cleared on Dispose. Use for upload-time mesh,
//                  controller, clip, parameter assets that the avatar
//                  references for the duration of one build only.
//
//   Session    ->  Assets/WhyKnot/__Session/<packageId>/<containerKey>/
//                  ...cleared on Dispose. Use for preview clones'
//                  controllers/clips: must outlive a domain reload
//                  (during a Unity scripted-recompile pass) but be
//                  gone when the preview stops.
//
//   Persistent ->  Assets/WhyKnot/Generated/<packageId>/<containerKey>/
//                  ...kept on Dispose; ClearPreviousAssets removes
//                  prior contents only when the caller asks. Use for
//                  Bake-style flows where the user explicitly wants
//                  the artefact to persist.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UmeVrcfQol.Internal.Utilities;

namespace UmeVrcfQol.Internal.Pipeline {

    public enum WkGeneratedAssetTier {
        Temporary,
        Session,
        Persistent,
    }

    public sealed class WkGeneratedAssetScope : IDisposable {

        public WkGeneratedAssetTier Tier { get; }
        public string PackageId { get; }
        public string ContainerKey { get; }
        public string AssetFolder { get; }

        private bool _disposed;

        public WkGeneratedAssetScope(WkGeneratedAssetTier tier, string packageId, string containerKey) {
            if (string.IsNullOrEmpty(packageId))    throw new ArgumentException("packageId is required",    nameof(packageId));
            if (string.IsNullOrEmpty(containerKey)) throw new ArgumentException("containerKey is required", nameof(containerKey));
            Tier = tier;
            PackageId = packageId;
            ContainerKey = SanitiseSegment(containerKey);
            AssetFolder = ComputeFolder(tier, packageId, ContainerKey);
            FolderUtility.EnsureFolder(AssetFolder);
        }

        /// <summary>
        /// Save an asset under this scope's folder. Returns the canonical
        /// asset path. The caller is responsible for any subsequent
        /// AssetDatabase.SaveAssets if they want it on disk now rather
        /// than on next refresh.
        /// </summary>
        public string SaveAsset(UnityEngine.Object asset, string name) {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (string.IsNullOrEmpty(name)) name = asset.GetType().Name;
            var path = AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/" + name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            return path;
        }

        /// <summary>
        /// Remove every asset currently in this scope's folder. For the
        /// Persistent tier this is the way a caller resets between
        /// re-bakes; for Temporary / Session tiers this is a no-op since
        /// Dispose handles it.
        /// </summary>
        public void ClearPreviousAssets() {
            if (string.IsNullOrEmpty(AssetFolder)) return;
            if (!AssetDatabase.IsValidFolder(AssetFolder)) return;
            var assets = AssetDatabase.FindAssets("", new[] { AssetFolder });
            foreach (var guid in assets) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && path != AssetFolder) {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            switch (Tier) {
                case WkGeneratedAssetTier.Temporary:
                case WkGeneratedAssetTier.Session:
                    // Wipe the whole folder. Filesystem-level delete is
                    // necessary because some files might have been written
                    // without going through AssetDatabase.
                    if (AssetDatabase.IsValidFolder(AssetFolder)) {
                        AssetDatabase.DeleteAsset(AssetFolder);
                    }
                    break;
                case WkGeneratedAssetTier.Persistent:
                    // No-op -- the folder + contents persist deliberately.
                    break;
            }
        }

        private static string ComputeFolder(WkGeneratedAssetTier tier, string packageId, string containerKey) {
            switch (tier) {
                case WkGeneratedAssetTier.Temporary:
                    return "Packages/" + packageId + "/__Generated/" + containerKey;
                case WkGeneratedAssetTier.Session:
                    return "Assets/WhyKnot/__Session/" + packageId + "/" + containerKey;
                case WkGeneratedAssetTier.Persistent:
                    return "Assets/WhyKnot/Generated/" + packageId + "/" + containerKey;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        private static string SanitiseSegment(string segment) {
            foreach (var ch in Path.GetInvalidFileNameChars()) segment = segment.Replace(ch, '_');
            return segment.Trim();
        }
    }
}
