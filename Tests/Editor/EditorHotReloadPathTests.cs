// EditorHotReloadPathTests.cs
//
// Pure-function coverage for the hot-reload path rules. The watcher itself is
// live editor plumbing, but these tests pin down the important boundary: this
// package only reacts to its own package root.

using System.IO;
using NUnit.Framework;
using UmeVrcfQol.Internal.HotReload;

namespace WhyKnot.VrcfQol.Tests {

    public sealed class EditorHotReloadPathTests {

        [TestCase("dev.whyknot.wk-vrcfury-qol.Editor", "dev.whyknot.wk-vrcfury-qol")]
        [TestCase("dev.whyknot.wk-vrcfury-qol.HotReload.Editor", "dev.whyknot.wk-vrcfury-qol")]
        [TestCase("dev.whyknot.wk-vrcfury-qol", "dev.whyknot.wk-vrcfury-qol")]
        [TestCase("", "dev.whyknot.wk-vrcfury-qol")]
        [TestCase(null, "dev.whyknot.wk-vrcfury-qol")]
        public void ResolvePackageIdFromAssemblyName_StripsEditorSuffixes(string assemblyName, string expected) {
            Assert.AreEqual(expected, EditorHotReload.ResolvePackageIdFromAssemblyName(assemblyName));
        }

        [TestCase("dev.whyknot.wk-vrcfury-qol.Editor", "dev.whyknot.wk-vrcfury-qol.settings.hot-reload-enabled")]
        [TestCase("dev.whyknot.wk-vrcfury-qol.HotReload.Editor", "dev.whyknot.wk-vrcfury-qol.settings.hot-reload-enabled")]
        public void ResolveHotReloadEnabledPrefsKey_UsesPackageSpecificKey(string assemblyName, string expected) {
            Assert.AreEqual(expected, EditorHotReload.ResolveHotReloadEnabledPrefsKey(assemblyName));
        }

        [Test]
        public void EmbeddedPackagePath_PointsAtPackageOnly() {
            var dataPath = Path.Combine("C:\\", "Avatar", "Assets");
            var expected = Path.Combine("C:\\", "Avatar", "Packages", "dev.whyknot.wk-vrcfury-qol");

            Assert.AreEqual(expected, EditorHotReload.EmbeddedPackagePath(dataPath, "dev.whyknot.wk-vrcfury-qol"));
        }

        [TestCase("Packages/dev.whyknot.wk-vrcfury-qol", true)]
        [TestCase("Packages/dev.whyknot.wk-vrcfury-qol/Editor/Internal/HotReload/EditorHotReload.cs", true)]
        [TestCase("Packages\\dev.whyknot.wk-vrcfury-qol\\Editor\\Internal\\HotReload\\EditorHotReload.cs", true)]
        [TestCase("Packages/dev.whyknot.wk-vrcfury-qol-other/Editor/Foo.cs", false)]
        [TestCase("Packages/dev.whyknot.wk-vrc-qol/Editor/Foo.cs", false)]
        [TestCase("Packages/com.vrcfury.vrcfury/Editor/Foo.cs", false)]
        [TestCase("Assets/Editor/Foo.cs", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsOwnPackageUnityPath_MatchesOnlyThisPackage(string unityPath, bool expected) {
            Assert.AreEqual(expected, EditorHotReload.IsOwnPackageUnityPath(unityPath, "dev.whyknot.wk-vrcfury-qol"));
        }

        [TestCase("Foo.cs", true)]
        [TestCase("Foo.asmdef", true)]
        [TestCase("Foo.asmref", true)]
        [TestCase("Foo.shader", true)]
        [TestCase("Foo.compute", true)]
        [TestCase("Foo.cginc", true)]
        [TestCase("Foo.hlsl", true)]
        [TestCase("Foo.png", false)]
        [TestCase("Foo.meta", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsTrackedExtension_RecognisesScriptsAndShaders(string path, bool expected) {
            Assert.AreEqual(expected, EditorHotReload.IsTrackedExtension(path));
        }

        [TestCase("Packages/dev.whyknot.wk-vrcfury-qol/Editor/Internal/Shaders/Common.cginc", "Packages/dev.whyknot.wk-vrcfury-qol")]
        [TestCase("Packages\\dev.whyknot.wk-vrcfury-qol\\Editor\\Internal\\Shaders\\Common.hlsl", "Packages/dev.whyknot.wk-vrcfury-qol")]
        public void ResolveReimportRoot_FindsOnlyOwnPackageRoot(string unityPath, string expected) {
            Assert.AreEqual(expected, EditorHotReload.ResolveReimportRoot(unityPath));
        }

        [TestCase("Packages/dev.whyknot.wk-vrc-qol/Editor/Tools/MaskPainter/Shaders/Common.cginc")]
        [TestCase("Packages/com.unity.render-pipelines.universal/Shaders/Common.hlsl")]
        [TestCase("Assets/Shaders/Common.cginc")]
        [TestCase("Packages")]
        [TestCase("")]
        [TestCase(null)]
        public void ResolveReimportRoot_SkipsEverythingOutsideThisPackage(string unityPath) {
            Assert.IsNull(EditorHotReload.ResolveReimportRoot(unityPath));
        }
    }
}
