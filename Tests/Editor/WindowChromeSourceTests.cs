// WindowChromeSourceTests.cs
//
// Source-level guard for the shared IMGUI chrome. The legacy windows are
// intentionally hand-rolled, so this catches accidental footer/scroll
// regressions before they reach Unity.

using System.IO;
using NUnit.Framework;

namespace UmeVrcfQol.Tests {

    public sealed class WindowChromeSourceTests {

        private static readonly string[] PublicWindowSources = {
            "Editor/Internal/WkToolWindow.cs",
            "Editor/Internal/HotReload/WkHotReloadStatus.cs",
            "Editor/Internal/Logging/WkLogViewerWindow.cs",
            "Editor/Tools/MissingReferenceWindow.cs",
            "Editor/Tools/MoveVrcfComponentsTool.cs",
            "Editor/Tools/ReplaceReferencesWindow.cs",
        };

        private static readonly string[] LongContentSources = {
            "Editor/Internal/HotReload/WkHotReloadStatus.cs",
            "Editor/Tools/MissingReferenceWindow.cs",
            "Editor/Tools/ReplaceReferencesWindow.cs",
        };

        [Test]
        public void PublicWindowsRenderWhyKnotFooter() {
            var packageRoot = LocatePackageRoot();
            foreach (var relativePath in PublicWindowSources) {
                string text = ReadSource(packageRoot, relativePath);
                bool hasFooter = text.Contains("WkStyles.BrandFooter")
                    || text.Contains("WkStyles.WindowFooter")
                    || text.Contains(": WkToolWindow");
                Assert.IsTrue(hasFooter, $"{relativePath} must render the WhyKnot footer.");
            }
        }

        [Test]
        public void PublicWindowsHaveScrollProtection() {
            var packageRoot = LocatePackageRoot();
            foreach (var relativePath in PublicWindowSources) {
                string text = ReadSource(packageRoot, relativePath);
                bool hasScroll = text.Contains("ScrollViewScope")
                    || text.Contains("BeginScrollView")
                    || text.Contains(": WkToolWindow");
                Assert.IsTrue(hasScroll, $"{relativePath} must protect overflowing content with a scroll view.");
            }
        }

        [Test]
        public void LongContentAreasUseCappedScrollHeights() {
            var packageRoot = LocatePackageRoot();
            foreach (var relativePath in LongContentSources) {
                string text = ReadSource(packageRoot, relativePath);
                StringAssert.Contains("CappedListHeight", text, $"{relativePath} must cap long content height.");
            }
        }

        [Test]
        public void PublicWindowsUseBrandedTitleContent() {
            var packageRoot = LocatePackageRoot();
            foreach (var relativePath in PublicWindowSources) {
                string text = ReadSource(packageRoot, relativePath);
                bool hasTitleContent = text.Contains("WkStyles.TitleContent")
                    || text.Contains(": WkToolWindow");
                Assert.IsTrue(hasTitleContent, $"{relativePath} must use shared text-only title content.");
                Assert.IsFalse(text.Contains("BrandLogoTexture"),
                    $"{relativePath} must not place the WhyKnot logo in title/tab chrome.");
            }
        }

        [Test]
        public void PublicWindowsHaveAutoSizeHooks() {
            var packageRoot = LocatePackageRoot();
            foreach (var relativePath in PublicWindowSources) {
                string text = ReadSource(packageRoot, relativePath);
                bool hasAutoSize = text.Contains("RequestAutoSize")
                    || text.Contains("AutoSizeWindow")
                    || text.Contains(": WkToolWindow");
                Assert.IsTrue(hasAutoSize, $"{relativePath} must request capped automatic resizing.");
            }
        }

        [Test]
        public void PublicWindowsDoNotRenderExtraLogos() {
            var packageRoot = LocatePackageRoot();
            foreach (var relativePath in PublicWindowSources) {
                string text = ReadSource(packageRoot, relativePath);
                Assert.IsFalse(text.Contains("BrandLogoMark"),
                    $"{relativePath} must leave logo rendering to the shared footer.");
                Assert.IsFalse(text.Contains("BrandLogoTexture"),
                    $"{relativePath} must leave logo rendering to the shared footer.");
            }
        }

        [Test]
        public void PackageIncludesWhyKnotLogoAsset() {
            var packageRoot = LocatePackageRoot();
            var logoPath = Path.Combine(packageRoot, "Editor", "Internal", "Assets", "WhyKnotLogo.png");
            Assert.IsTrue(File.Exists(logoPath), "Expected the package to include the WhyKnot logo editor asset.");

            string styles = ReadSource(packageRoot, "Editor/Internal/Styling/WkStyles.cs");
            StringAssert.Contains("BrandLogoAssetName", styles);
            StringAssert.Contains("TitleContent", styles);
            StringAssert.Contains("BrandLogoMark", styles);
            StringAssert.Contains("Made with", styles);
            StringAssert.Contains("\\u2665", styles);
            StringAssert.Contains("AutoSizeWindow", styles);
            StringAssert.DoesNotContain("new GUIContent(title, BrandLogoTexture", styles);
        }

        private static string ReadSource(string packageRoot, string relativePath) {
            var fullPath = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(fullPath), $"Expected to find {relativePath} under {packageRoot}.");
            return File.ReadAllText(fullPath);
        }

        private static string LocatePackageRoot() {
            var dir = new DirectoryInfo(GetThisDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "package.json"))) {
                dir = dir.Parent;
            }
            Assert.IsNotNull(dir, "Could not locate the wk-vrcfury-qol package root.");
            return dir.FullName;
        }

        private static string GetThisDirectory(
                [System.Runtime.CompilerServices.CallerFilePath] string filePath = "") {
            return Path.GetDirectoryName(filePath);
        }
    }
}
