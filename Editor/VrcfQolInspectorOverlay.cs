#nullable enable

using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmeVrcfQol
{
    [InitializeOnLoad]
    internal static partial class VrcfQolInspectorOverlay
    {
        private const string InjectedClass = "vrcfqol-injected";
        private const string ButtonBarClass = "vrcfqol-buttons";
        private const string ActionToolsClass = "vrcfqol-action-tools";
        private const string ActionToolsInjectedClass = "vrcfqol-action-tools-injected";
        private const string ToggleBannerClass = "vrcfqol-toggle-banner";

        private const double ScanIntervalSeconds = 0.25;

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
        private static readonly Regex PageLabelRegex = new(
            @"^Page #(?<page>\d+)$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            RegexTimeout);
        private static readonly Regex ActionPathRegex = new(
            @"(^|\.)actions\.Array\.data\[\d+\]$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            RegexTimeout);
        private static readonly Regex FlipbookActionPathRegex = new(
            @"\.pages\.Array\.data\[\d+\]\.state\.actions\.Array\.data\[\d+\]$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            RegexTimeout);

        private static double NextScan;
        private static string LastScanFailure = "";

        static VrcfQolInspectorOverlay()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < NextScan)
            {
                return;
            }

            NextScan = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
            try
            {
                Scan();
            }
            catch (Exception ex) when (VrcfQol.IsCallbackFailure(ex))
            {
                if (!string.Equals(LastScanFailure, ex.Message, StringComparison.Ordinal))
                {
                    LastScanFailure = ex.Message;
                    VrcfQolLogger.Instance.Exception(ex);
                }
            }
        }

        private static void Scan()
        {
            if (!VrcfQol.Reflection.TryEnsure(out _))
            {
                return;
            }

            EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (EditorWindow w in allWindows)
            {
                if (w == null)
                {
                    continue;
                }

                if (!string.Equals(w.GetType().Name, "InspectorWindow", StringComparison.Ordinal))
                {
                    continue;
                }

                VisualElement root = w.rootVisualElement;
                if (root == null)
                {
                    continue;
                }

                ScanRoot(root);
            }
        }

        private static void ScanRoot(VisualElement root)
        {
            EnsureToggleBanner(root);

            ScrollView? scroll = root.Q<ScrollView>();
            Vector2 savedScrollOffset = scroll != null ? scroll.scrollOffset : Vector2.zero;

            int injectedThisScan = 0;
            var labels = root.Query<Label>().ToList();
            foreach (Label label in labels)
            {
                Match match = PageLabelRegex.Match(label.text ?? "");
                if (!match.Success)
                {
                    continue;
                }

                if (TryInjectPageButtons(label))
                {
                    injectedThisScan++;
                }
            }
            injectedThisScan += InjectActionButtons(root);

            if (injectedThisScan > 0 && scroll != null)
            {
                ScrollView capturedScroll = scroll;
                Vector2 capturedOffset = savedScrollOffset;
                scroll.schedule
                    .Execute(() => capturedScroll.scrollOffset = capturedOffset)
                    .ExecuteLater(0);
            }
        }
    }
}
