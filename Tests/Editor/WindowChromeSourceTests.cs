#nullable enable

using System;
using System.IO;
using NUnit.Framework;

namespace UmeVrcfQol.Tests
{
    public sealed class WindowChromeSourceTests
    {
        private static readonly string[] PublicWindowSources = {
            "Editor/Internal/WkToolWindow.cs",
            "Editor/Internal/HotReload/WkHotReloadStatus.cs",
            "Editor/Internal/Logging/WkLogViewerWindow.cs",
            "Editor/Tools/MissingReferenceWindow.cs",
            "Editor/Tools/MoveVrcfComponentsWindow.cs",
            "Editor/Tools/ReplaceReferencesWindow.cs",
        };

        private static readonly string[] LongContentSources = {
            "Editor/Internal/HotReload/WkHotReloadStatus.cs",
            "Editor/Tools/MissingReferenceWindow.cs",
            "Editor/Tools/ReplaceReferencesWindow.cs",
        };

        private static readonly string[] AnimatedChromeMarkers = {
            "AnimatedAccentLine",
            "RepaintAnimatedChrome",
            "AnimateChrome",
        };

        [Test]
        public void PublicWindowsRenderWhyKnotFooter()
        {
            string packageRoot = LocatePackageRoot();
            foreach (string relativePath in PublicWindowSources)
            {
                string text = ReadSource(packageRoot, relativePath);
                bool hasFooter = text.Contains("WkStyles.BrandFooter", StringComparison.Ordinal)
                    || text.Contains("WkStyles.WindowFooter", StringComparison.Ordinal)
                    || text.Contains(": WkToolWindow", StringComparison.Ordinal);
                Assert.IsTrue(hasFooter, $"{relativePath} must render the WhyKnot footer.");
            }
        }

        [Test]
        public void PublicWindowsHaveScrollProtection()
        {
            string packageRoot = LocatePackageRoot();
            foreach (string relativePath in PublicWindowSources)
            {
                string text = ReadSource(packageRoot, relativePath);
                bool hasScroll = text.Contains("ScrollViewScope", StringComparison.Ordinal)
                    || text.Contains("BeginScrollView", StringComparison.Ordinal)
                    || text.Contains(": WkToolWindow", StringComparison.Ordinal);
                Assert.IsTrue(hasScroll, $"{relativePath} must protect overflowing content with a scroll view.");
            }
        }

        [Test]
        public void LongContentAreasUseCappedScrollHeights()
        {
            string packageRoot = LocatePackageRoot();
            foreach (string relativePath in LongContentSources)
            {
                string text = ReadSource(packageRoot, relativePath);
                StringAssert.Contains("CappedListHeight", text, $"{relativePath} must cap long content height.");
            }
        }

        [Test]
        public void PublicWindowsUseBrandedTitleContent()
        {
            string packageRoot = LocatePackageRoot();
            foreach (string relativePath in PublicWindowSources)
            {
                string text = ReadSource(packageRoot, relativePath);
                bool hasTitleContent = text.Contains("WkStyles.TitleContent", StringComparison.Ordinal)
                    || text.Contains(": WkToolWindow", StringComparison.Ordinal);
                Assert.IsTrue(hasTitleContent, $"{relativePath} must use shared text-only title content.");
                Assert.IsFalse(text.Contains("BrandLogoTexture", StringComparison.Ordinal),
                    $"{relativePath} must not place the WhyKnot logo in title/tab chrome.");
            }
        }

        [Test]
        public void PublicWindowsHaveAutoSizeHooks()
        {
            string packageRoot = LocatePackageRoot();
            foreach (string relativePath in PublicWindowSources)
            {
                string text = ReadSource(packageRoot, relativePath);
                bool hasAutoSize = text.Contains("RequestAutoSize", StringComparison.Ordinal)
                    || text.Contains("AutoSizeWindow", StringComparison.Ordinal)
                    || text.Contains(": WkToolWindow", StringComparison.Ordinal);
                Assert.IsTrue(hasAutoSize, $"{relativePath} must request capped automatic resizing.");
            }
        }

        [Test]
        public void PublicWindowsDoNotRenderExtraLogos()
        {
            string packageRoot = LocatePackageRoot();
            foreach (string relativePath in PublicWindowSources)
            {
                string text = ReadSource(packageRoot, relativePath);
                Assert.IsFalse(text.Contains("BrandLogoMark", StringComparison.Ordinal),
                    $"{relativePath} must leave logo rendering to the shared footer.");
                Assert.IsFalse(text.Contains("BrandLogoTexture", StringComparison.Ordinal),
                    $"{relativePath} must leave logo rendering to the shared footer.");
            }
        }

        [Test]
        public void PublicWindowsDoNotDrawAnimatedTopAccent()
        {
            string packageRoot = LocatePackageRoot();
            foreach (string relativePath in PublicWindowSources)
            {
                string text = ReadSource(packageRoot, relativePath);
                foreach (string marker in AnimatedChromeMarkers)
                {
                    Assert.IsFalse(text.Contains(marker, StringComparison.Ordinal), $"{relativePath} must not draw animated title chrome.");
                }
            }
        }

        [Test]
        public void PackageIncludesWhyKnotLogoAsset()
        {
            string packageRoot = LocatePackageRoot();
            string logoPath = Path.Combine(packageRoot, "Editor", "Internal", "Assets", "WhyKnotLogo.png");
            Assert.IsTrue(File.Exists(logoPath), "Expected the package to include the WhyKnot logo editor asset.");

            string styles = ReadSource(packageRoot, "Editor/Internal/Styling/WkStyles.cs");
            StringAssert.Contains("BrandLogoAssetName", styles);
            StringAssert.Contains("TitleContent", styles);
            StringAssert.Contains("BrandLogoMark", styles);
            StringAssert.Contains("Made with", styles);
            StringAssert.Contains("\\u2665", styles);
            StringAssert.Contains("AutoSizeWindow", styles);
            StringAssert.DoesNotContain("new GUIContent(title, BrandLogoTexture", styles);
            foreach (string marker in AnimatedChromeMarkers)
            {
                StringAssert.DoesNotContain(marker, styles);
            }
        }

        private static string ReadSource(string packageRoot, string relativePath)
        {
            string fullPath = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(fullPath), $"Expected to find {relativePath} under {packageRoot}.");
            return File.ReadAllText(fullPath);
        }

        private static string LocatePackageRoot()
        {
            var dir = new DirectoryInfo(GetThisDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "package.json")))
            {
                dir = dir.Parent;
            }
            return dir == null
                ? throw new DirectoryNotFoundException("Could not locate the wk-vrcfury-qol package root.")
                : dir.FullName;
        }

        private static string GetThisDirectory(
                [System.Runtime.CompilerServices.CallerFilePath] string filePath = "")
        {
            return Path.GetDirectoryName(filePath);
        }
    }
}
