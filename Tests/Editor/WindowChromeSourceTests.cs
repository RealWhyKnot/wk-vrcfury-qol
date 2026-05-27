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
