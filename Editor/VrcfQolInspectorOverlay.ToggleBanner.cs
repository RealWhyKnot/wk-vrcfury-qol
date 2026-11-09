#nullable enable

using System;
using System.Collections.Generic;
using UmeVrcfQol.Internal.Reflection;
using UmeVrcfQol.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmeVrcfQol
{
    internal static partial class VrcfQolInspectorOverlay
    {
        private static void EnsureToggleBanner(VisualElement root)
        {
            HashSet<Component> togglesOnSelection = CollectTogglesOnSelection();
            if (togglesOnSelection.Count == 0)
            {
                RemoveAllBanners(root);
                return;
            }

            Dictionary<Component, VisualElement> anchored = AnchorBanners(root, togglesOnSelection);
            foreach (VisualElement banner in root.Query<VisualElement>(className: ToggleBannerClass).ToList())
            {
                if (banner.userData is not Component owner
                        || !anchored.TryGetValue(owner, out VisualElement? canonical)
                        || !ReferenceEquals(banner, canonical))
                {
                    banner.RemoveFromHierarchy();
                }
            }
        }

        private static HashSet<Component> CollectTogglesOnSelection()
        {
            var toggles = new HashSet<Component>();
            VrcfQol.ReflectionCache r = VrcfQol.Reflection;
            GameObject selected = Selection.activeGameObject;
            if (!r.TryEnsure(out _) || selected == null)
            {
                return toggles;
            }

            foreach (Component c in selected.GetComponents(r.VRCFuryType))
            {
                if (c == null)
                {
                    continue;
                }

                object content = r.ContentField.GetValue(c);
                if (content != null && content.GetType() == r.ToggleType)
                {
                    toggles.Add(c);
                }
            }

            return toggles;
        }

        private static Dictionary<Component, VisualElement> AnchorBanners(VisualElement root, HashSet<Component> togglesOnSelection)
        {
            var anchored = new Dictionary<Component, VisualElement>();
            foreach (VisualElement wrapper in EditorElementWalker.EnumerateEditorWrappers(root))
            {
                if (!string.Equals(wrapper.GetType().Name, "EditorElement", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!EditorElementWalker.TryGetEditorTarget(wrapper, out Component? comp) || comp == null)
                {
                    continue;
                }

                if (!togglesOnSelection.Contains(comp) || anchored.ContainsKey(comp))
                {
                    continue;
                }

                VisualElement? banner = BuildBannerInside(wrapper, comp);
                if (banner != null)
                {
                    anchored[comp] = banner;
                }
            }

            return anchored;
        }

        private static void RemoveAllBanners(VisualElement root)
        {
            foreach (VisualElement banner in root.Query<VisualElement>(className: ToggleBannerClass).ToList())
            {
                banner.RemoveFromHierarchy();
            }
        }

        private static VisualElement? BuildBannerInside(VisualElement editorElement, Component target)
        {
            VisualElement? host = EditorElementWalker.FindInspectorContent(editorElement);
            if (host == null)
            {
                return null;
            }

            VisualElement? banner = host.Q<VisualElement>(className: ToggleBannerClass);
            if (banner == null || !ReferenceEquals(banner.parent, host))
            {
                banner?.RemoveFromHierarchy();
                banner = new VisualElement();
                banner.AddToClassList(ToggleBannerClass);
                EditorElementWalker.ApplyBannerChromeStyle(banner);
                host.Insert(0, banner);
            }
            banner.userData = target;
            PopulateBanner(banner, target);
            return banner;
        }

        private static void PopulateBanner(VisualElement banner, Component target)
        {
            banner.Clear();

            var mutedText = new StyleColor(new Color(0.78f, 0.78f, 0.78f, 1f));
            bool supported = VrcfQol.Reflection.ToggleUseGlobalParamField != null
                         && VrcfQol.Reflection.ToggleGlobalParamField != null;

            if (!supported)
            {
                banner.style.borderLeftColor = new StyleColor(new Color(0.55f, 0.55f, 0.55f, 1f));
                var msg = new Label("wk-vrcfury-qol: this VRCFury version does not expose Global Parameter fields. Auto-sync disabled.");
                msg.style.color = mutedText;
                msg.style.flexGrow = 1;
                msg.style.whiteSpace = WhiteSpace.Normal;
                banner.Add(msg);
                return;
            }

            bool optedOut = AutoGlobalParameterTool.IsOptedOut(target);
            banner.style.borderLeftColor = new StyleColor(optedOut
                ? new Color(0.78f, 0.42f, 0.32f, 1f)
                : new Color(0.42f, 0.70f, 0.45f, 1f));

            List<ToggleInfo> avatarToggles = AutoGlobalParameterTool.CollectAvatarToggles(AutoGlobalParameterTool.AvatarRootOf(target));
            string myId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            (string headline, string tooltip) = DescribeSync(optedOut, FindToggle(avatarToggles, myId), avatarToggles);

            var label = new Label(headline);
            label.style.color = mutedText;
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.tooltip = tooltip;
            banner.Add(label);
            AddBannerButtons(banner, target, optedOut);
        }

        private static ToggleInfo? FindToggle(List<ToggleInfo> avatarToggles, string id)
        {
            foreach (ToggleInfo t in avatarToggles)
            {
                if (string.Equals(t.GlobalObjectId, id, StringComparison.Ordinal))
                {
                    return t;
                }
            }

            return null;
        }

        private static (string headline, string tooltip) DescribeSync(bool optedOut, ToggleInfo? found, List<ToggleInfo> avatarToggles)
        {
            if (optedOut)
            {
                return DescribeOptedOut(found, avatarToggles);
            }

            if (found is ToggleInfo mine && !string.Equals(mine.CurrentGlobalParam, mine.MenuPath, StringComparison.Ordinal))
            {
                if (mine.IsPinned)
                {
                    return (
                        $"wk-vrcfury-qol: Global Parameter kept as '{mine.CurrentGlobalParam}'",
                        "This Toggle already had this Global Parameter, so it isn't renamed to match the Menu Path. Renaming it would reset its saved value in VRChat. To rename it anyway, use Sync global parameter now from the WhyKnot right-click menu.");
                }

                if (!string.IsNullOrEmpty(mine.MenuPath) && !string.IsNullOrEmpty(mine.CurrentGlobalParam))
                {
                    return (
                        $"wk-vrcfury-qol: Global Parameter auto-synced with Menu Path (renamed to '{mine.CurrentGlobalParam}' to avoid collision)",
                        $"Another Toggle on this avatar already uses the menu-path name '{mine.MenuPath}'. To avoid a VRCFury build error, this Toggle's globalParam was set to '{mine.CurrentGlobalParam}'. Rename the menu path to take ownership of '{mine.MenuPath}' again.");
                }
            }

            return (
                "wk-vrcfury-qol: Global Parameter auto-synced with Menu Path",
                "Keeps 'Use Global Parameter' checked and sets 'globalParam' to the Menu Path so VRCFury cannot wipe custom work during avatar updates. A Global Parameter that was already set when the scene opened is left alone.");
        }

        private static (string headline, string tooltip) DescribeOptedOut(ToggleInfo? found, List<ToggleInfo> avatarToggles)
        {
            string headline = "wk-vrcfury-qol: Global Parameter sync off";
            string tooltip = "Global Parameter is NOT being kept in sync with the Menu Path for this toggle. Click Turn on to resume syncing.";
            if (found is ToggleInfo mine && !string.IsNullOrEmpty(mine.CurrentGlobalParam) && CollidesWithOptedOut(mine, avatarToggles))
            {
                headline += $" - still colliding on '{mine.CurrentGlobalParam}'";
                tooltip += $"\n\nAnother opted-out Toggle on this avatar also holds globalParam='{mine.CurrentGlobalParam}'. VRCFury will error at build. Enable sync on one of them to auto-disambiguate.";
            }

            return (headline, tooltip);
        }

        private static bool CollidesWithOptedOut(ToggleInfo mine, List<ToggleInfo> avatarToggles)
        {
            foreach (ToggleInfo t in avatarToggles)
            {
                if (!string.Equals(t.GlobalObjectId, mine.GlobalObjectId, StringComparison.Ordinal)
                    && t.IsOptedOut
                    && string.Equals(t.CurrentGlobalParam, mine.CurrentGlobalParam, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddBannerButtons(VisualElement banner, Component target, bool optedOut)
        {
            bool previewing = PreviewTool.IsPreviewingComponent(target);
            var previewBtn = new Button(() => PreviewTool.ToggleTogglePreview(target))
            {
                text = previewing ? "Stop Previewing" : "Preview",
                tooltip = previewing
                    ? "Destroy the temporary preview copy and return to the original avatar."
                    : "Create a temporary avatar copy with this toggle applied. Does not move the Scene camera.",
            };
            EditorElementWalker.ApplyInlineButtonStyle(previewBtn);
            EditorElementWalker.ApplyDangerButtonStyle(previewBtn, previewing);
            banner.Add(previewBtn);

            var optBtn = new Button(() =>
            {
                AutoGlobalParameterTool.SetOptedOut(target, !optedOut);
                if (optedOut)
                {
                    AutoGlobalParameterTool.ApplyTo(target, force: true);
                }
            })
            {
                text = optedOut ? "Turn on" : "Turn off",
                tooltip = optedOut
                    ? "Resume auto-syncing the Global Parameter with the Menu Path. If they differ, the Global Parameter is renamed to match."
                    : "Stop auto-syncing the Global Parameter. The current value will not be touched.",
            };
            EditorElementWalker.ApplyInlineButtonStyle(optBtn);
            banner.Add(optBtn);
        }
    }
}
