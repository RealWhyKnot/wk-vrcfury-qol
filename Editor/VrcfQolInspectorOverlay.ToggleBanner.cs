// VrcfQolInspectorOverlay.ToggleBanner.cs

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UmeVrcfQol.Tools;
using UmeVrcfQol.Internal.Reflection;

namespace UmeVrcfQol {

    internal static partial class VrcfQolInspectorOverlay {

        // ----------------------------------------------------------------------
        // Per-Toggle auto-update + Preview banner (one banner per Toggle
        // component, sitting inside that Toggle's editor subtree so the Preview
        // button belongs to exactly the Toggle it visually accompanies -- not
        // a global banner that can only address the first Toggle on a
        // multi-Toggle GameObject).
        // ----------------------------------------------------------------------

        private static void EnsureToggleBanner(VisualElement root) {
            if (!VrcfQol.Reflection.TryEnsure(out _)) {
                RemoveAllBanners(root);
                return;
            }

            var selected = Selection.activeGameObject;
            if (selected == null) {
                RemoveAllBanners(root);
                return;
            }

            var r = VrcfQol.Reflection;
            var togglesOnSelection = new HashSet<Component>();
            foreach (Component c in selected.GetComponents(r.VRCFuryType)) {
                if (c == null) continue;
                var content = r.ContentField.GetValue(c);
                if (content == null || content.GetType() != r.ToggleType) continue;
                togglesOnSelection.Add(c);
            }

            if (togglesOnSelection.Count == 0) {
                RemoveAllBanners(root);
                return;
            }

            // Track the canonical banner per Toggle Component. The cleanup
            // pass below removes any other banner that points at the same
            // Component -- a stale banner stranded above the EditorElement
            // header after an inspector rebuild won't be visible to
            // BuildBannerInside's host-scoped Q<>, so it survives unless we
            // explicitly drop everything that isn't the canonical one.
            var anchored = new Dictionary<Component, VisualElement>();
            foreach (var wrapper in EditorElementWalker.EnumerateEditorWrappers(root)) {
                if (wrapper.GetType().Name != "EditorElement") continue;
                if (!EditorElementWalker.TryGetEditorTarget(wrapper, out var comp)) continue;
                if (!togglesOnSelection.Contains(comp)) continue;
                if (anchored.ContainsKey(comp)) continue;
                var banner = BuildBannerInside(wrapper, comp);
                if (banner != null) anchored[comp] = banner;
            }

            foreach (var banner in root.Query<VisualElement>(className: ToggleBannerClass).ToList()) {
                var owner = banner.userData as Component;
                if (owner == null
                        || !anchored.TryGetValue(owner, out var canonical)
                        || !ReferenceEquals(banner, canonical)) {
                    banner.RemoveFromHierarchy();
                }
            }
        }

        private static void RemoveAllBanners(VisualElement root) {
            foreach (var banner in root.Query<VisualElement>(className: ToggleBannerClass).ToList()) {
                banner.RemoveFromHierarchy();
            }
        }

        private static VisualElement BuildBannerInside(VisualElement editorElement, Component target) {
            // Anchor the banner inside the InspectorElement (the editor's
            // content host) so it sits BELOW the component header rather
            // than above it, integrating with the VRCFury inspector body.
            // Returns the canonical banner so the caller can mark it as the
            // one to keep during the duplicate-cleanup pass.
            var host = EditorElementWalker.FindInspectorContent(editorElement);
            if (host == null) return null;
            var banner = host.Q<VisualElement>(className: ToggleBannerClass);
            if (banner == null || !ReferenceEquals(banner.parent, host)) {
                if (banner != null) banner.RemoveFromHierarchy();
                banner = new VisualElement();
                banner.AddToClassList(ToggleBannerClass);
                EditorElementWalker.ApplyBannerChromeStyle(banner);
                host.Insert(0, banner);
            }
            banner.userData = target;
            PopulateBanner(banner, target);
            return banner;
        }


        private static void PopulateBanner(VisualElement banner, Component target) {
            banner.Clear();

            var mutedText = new StyleColor(new Color(0.78f, 0.78f, 0.78f, 1f));
            var supported = VrcfQol.Reflection.ToggleUseGlobalParamField != null
                         && VrcfQol.Reflection.ToggleGlobalParamField != null;

            if (!supported) {
                banner.style.borderLeftColor = new StyleColor(new Color(0.55f, 0.55f, 0.55f, 1f));
                var msg = new Label("vrcfury-qol: this VRCFury version does not expose Global Parameter fields. Auto-sync disabled.");
                msg.style.color = mutedText;
                msg.style.flexGrow = 1;
                msg.style.whiteSpace = WhiteSpace.Normal;
                banner.Add(msg);
                return;
            }

            var optedOut = AutoGlobalParameterTool.IsOptedOut(target);
            banner.style.borderLeftColor = new StyleColor(optedOut
                ? new Color(0.78f, 0.42f, 0.32f, 1f)
                : new Color(0.42f, 0.70f, 0.45f, 1f));

            // Read this Toggle's actual state + the avatar's other Toggles so we
            // can surface disambiguation / collision context. Cheap: one
            // Resources scan per inspector repaint.
            var avatarRoot = PreviewTool.FindAvatarRoot(target) ?? target.gameObject;
            var avatarToggles = AutoGlobalParameterTool.CollectAvatarToggles(avatarRoot);
            var myId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            ToggleInfo mine = default;
            var foundMine = false;
            foreach (var t in avatarToggles) {
                if (t.GlobalObjectId == myId) { mine = t; foundMine = true; break; }
            }

            string headline, tooltip;
            if (optedOut) {
                headline = "vrcfury-qol: Global Parameter sync off";
                tooltip = "Global Parameter is NOT being kept in sync with the Menu Path for this toggle. Click Turn on to resume syncing.";
                if (foundMine && !string.IsNullOrEmpty(mine.CurrentGlobalParam)) {
                    var collidesWithAnotherOptedOut = false;
                    foreach (var t in avatarToggles) {
                        if (t.GlobalObjectId == myId) continue;
                        if (t.IsOptedOut && t.CurrentGlobalParam == mine.CurrentGlobalParam) {
                            collidesWithAnotherOptedOut = true;
                            break;
                        }
                    }
                    if (collidesWithAnotherOptedOut) {
                        headline += $" - still colliding on '{mine.CurrentGlobalParam}'";
                        tooltip += $"\n\nAnother opted-out Toggle on this avatar also holds globalParam='{mine.CurrentGlobalParam}'. VRCFury will error at build. Enable sync on one of them to auto-disambiguate.";
                    }
                }
            } else if (foundMine
                       && !string.IsNullOrEmpty(mine.MenuPath)
                       && !string.IsNullOrEmpty(mine.CurrentGlobalParam)
                       && mine.CurrentGlobalParam != mine.MenuPath) {
                headline = $"vrcfury-qol: Global Parameter auto-synced with Menu Path (renamed to '{mine.CurrentGlobalParam}' to avoid collision)";
                tooltip = $"Another Toggle on this avatar already uses the menu-path name '{mine.MenuPath}'. To avoid a VRCFury build error, this Toggle's globalParam was set to '{mine.CurrentGlobalParam}'. Rename the menu path to take ownership of '{mine.MenuPath}' again.";
            } else {
                headline = "vrcfury-qol: Global Parameter auto-synced with Menu Path";
                tooltip = "Keeps 'Use Global Parameter' checked and 'globalParam' equal to the Menu Path so VRCFury cannot wipe custom work during avatar updates.";
            }

            var label = new Label(headline);
            label.style.color = mutedText;
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.tooltip = tooltip;
            banner.Add(label);

            var capturedTarget = target;
            var capturedOptedOut = optedOut;
            var previewing = PreviewTool.IsPreviewingComponent(capturedTarget);
            var previewBtn = new Button(() => PreviewTool.ToggleTogglePreview(capturedTarget)) {
                text = previewing ? "Stop Previewing" : "Preview"
            };
            previewBtn.tooltip = previewing
                ? "Destroy the temporary preview copy and return to the original avatar."
                : "Create a temporary avatar copy with this toggle applied. Does not move the Scene camera.";
            EditorElementWalker.ApplyInlineButtonStyle(previewBtn);
            EditorElementWalker.ApplyDangerButtonStyle(previewBtn, previewing);
            banner.Add(previewBtn);

            var optBtn = new Button(() => {
                AutoGlobalParameterTool.SetOptedOut(capturedTarget, !capturedOptedOut);
                if (capturedOptedOut) {
                    AutoGlobalParameterTool.ApplyTo(capturedTarget, force: true);
                }
            }) {
                text = optedOut ? "Turn on" : "Turn off"
            };
            optBtn.tooltip = optedOut
                ? "Resume auto-syncing the Global Parameter with the Menu Path."
                : "Stop auto-syncing the Global Parameter. The current value will not be touched.";
            EditorElementWalker.ApplyInlineButtonStyle(optBtn);
            banner.Add(optBtn);
        }
    }
}
