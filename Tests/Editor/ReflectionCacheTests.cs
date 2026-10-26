using System;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace WhyKnot.VrcfQol.Tests {

    public class ReflectionCacheTests {

        private static Assembly BuildAssembly(params string[] typeNames) {
            var name = new AssemblyName("VRCFuryFake" + Guid.NewGuid().ToString("N"));
            var asm = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            var module = asm.DefineDynamicModule(name.Name);
            foreach (var typeName in typeNames) {
                module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class).CreateType();
            }
            return asm;
        }

        [Test]
        public void MissingAssembly_FailsEveryTime() {
            var cache = new UmeVrcfQol.VrcfQol.ReflectionCache(() => null);

            Assert.IsFalse(cache.TryEnsure(out var first));
            Assert.IsFalse(cache.TryEnsure(out var second));
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
        }

        [Test]
        public void MissingModelTypes_FailsEveryTime() {
            var asm = BuildAssembly("VF.Model.VRCFury");
            var cache = new UmeVrcfQol.VrcfQol.ReflectionCache(() => asm);

            Assert.IsFalse(cache.TryEnsure(out var first));
            Assert.IsFalse(cache.TryEnsure(out var second));
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.IsNull(cache.VRCFuryType);
            Assert.IsNull(cache.VrcfuryAsm);
        }

        [Test]
        public void MissingFlipbookPage_FailsEveryTime() {
            var asm = BuildAssembly(
                "VF.Model.VRCFury",
                "VF.Model.Feature.Toggle",
                "VF.Model.State",
                "VF.Model.StateAction.FlipBookBuilderAction");
            var cache = new UmeVrcfQol.VrcfQol.ReflectionCache(() => asm);

            Assert.IsFalse(cache.TryEnsure(out _));
            Assert.IsFalse(cache.TryEnsure(out var second));
            Assert.IsNotNull(second);
            Assert.IsNull(cache.VRCFuryType);
            Assert.IsNull(cache.FlipbookBuilderActionType);
        }
    }
}
