#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;

namespace WhyKnot.VrcfQol.Tests
{

    public sealed class ReflectionCacheTests
    {

        private static AssemblyBuilder BuildAssembly(params string[] typeNames)
        {
            var name = new AssemblyName("VRCFuryFake" + Guid.NewGuid().ToString("N"));
            var asm = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            ModuleBuilder module = asm.DefineDynamicModule(name.Name);
            foreach (string typeName in typeNames)
            {
                module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class).CreateType();
            }
            return asm;
        }

        [Test]
        public void MissingAssemblyFailsEveryTime()
        {
            var cache = new UmeVrcfQol.VrcfQol.ReflectionCache(() => null);

            Assert.IsFalse(cache.TryEnsure(out string? first));
            Assert.IsFalse(cache.TryEnsure(out string? second));
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
        }

        [Test]
        public void MissingModelTypesFailsEveryTime()
        {
            Assembly asm = BuildAssembly("VF.Model.VRCFury");
            var cache = new UmeVrcfQol.VrcfQol.ReflectionCache(() => asm);

            Assert.IsFalse(cache.TryEnsure(out string? first));
            Assert.IsFalse(cache.TryEnsure(out string? second));
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.IsNull(cache.VRCFuryType);
            Assert.IsNull(cache.VrcfuryAsm);
        }

        [Test]
        public void MissingFlipbookPageFailsEveryTime()
        {
            Assembly asm = BuildAssembly(
                "VF.Model.VRCFury",
                "VF.Model.Feature.Toggle",
                "VF.Model.State",
                "VF.Model.StateAction.FlipBookBuilderAction");
            var cache = new UmeVrcfQol.VrcfQol.ReflectionCache(() => asm);

            Assert.IsFalse(cache.TryEnsure(out _));
            Assert.IsFalse(cache.TryEnsure(out string? second));
            Assert.IsNotNull(second);
            Assert.IsNull(cache.VRCFuryType);
            Assert.IsNull(cache.FlipbookBuilderActionType);
        }
    }
}
