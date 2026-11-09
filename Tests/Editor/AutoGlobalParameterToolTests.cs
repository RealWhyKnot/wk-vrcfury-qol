#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UmeVrcfQol.Tools;

namespace WhyKnot.VrcfQol.Tests
{
    public sealed class AutoGlobalParameterToolTests
    {
        private static ToggleInfo Info(string id, string menu, string cur = "", bool optedOut = false)
        {
            return new ToggleInfo
            {
                GlobalObjectId = id,
                MenuPath = menu,
                CurrentGlobalParam = cur,
                IsOptedOut = optedOut,
            };
        }

        [Test]
        public void NoCollisionsAssignsMenuPathToEach()
        {
            var input = new List<ToggleInfo> {
                Info("a", "Hat"),
                Info("b", "Glasses"),
                Info("c", "Backpack"),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["a"]);
            Assert.AreEqual("Glasses", result["b"]);
            Assert.AreEqual("Backpack", result["c"]);
            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void TwoCollidingFirstKeepsNameSecondGetsTwo()
        {
            var input = new List<ToggleInfo> {
                Info("a", "Hat"),
                Info("b", "Hat"),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["a"]);
            Assert.AreEqual("Hat 2", result["b"]);
        }

        [Test]
        public void ThreeCollidingGetSequentialSuffixes()
        {
            var input = new List<ToggleInfo> {
                Info("a", "Hat"),
                Info("b", "Hat"),
                Info("c", "Hat"),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["a"]);
            Assert.AreEqual("Hat 2", result["b"]);
            Assert.AreEqual("Hat 3", result["c"]);
        }

        [Test]
        public void EmptyMenuPathNotAssigned()
        {
            var input = new List<ToggleInfo> {
                Info("a", ""),
                Info("b", "   "),
                Info("c", "Hat"),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.IsFalse(result.ContainsKey("a"));
            Assert.IsFalse(result.ContainsKey("b"));
            Assert.AreEqual("Hat", result["c"]);
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void OptedOutHoldsBareNameNonOptedOutGetsSuffix()
        {
            var input = new List<ToggleInfo> {
                Info("a", "Hat", cur: "Hat", optedOut: true),
                Info("b", "Hat"),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.IsFalse(result.ContainsKey("a"));
            Assert.AreEqual("Hat 2", result["b"]);
        }

        [Test]
        public void OptedOutHoldsSuffixedNameNonOptedOutRoutesAround()
        {
            var input = new List<ToggleInfo> {
                Info("a", "Foo", cur: "Foo 2", optedOut: true),
                Info("b", "Foo"),
                Info("c", "Foo"),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.IsFalse(result.ContainsKey("a"));
            Assert.AreEqual("Foo", result["b"]);
            Assert.AreEqual("Foo 3", result["c"]);
        }

        [Test]
        public void StableOrderInputPermutationDoesNotChangeResult()
        {
            var forward = new List<ToggleInfo> {
                Info("a", "Hat"),
                Info("b", "Hat"),
                Info("c", "Hat"),
            };
            var reversed = new List<ToggleInfo> {
                Info("c", "Hat"),
                Info("b", "Hat"),
                Info("a", "Hat"),
            };

            IReadOnlyDictionary<string, string> r1 = AutoGlobalParameterTool.ComputeAssignments(forward);
            IReadOnlyDictionary<string, string> r2 = AutoGlobalParameterTool.ComputeAssignments(reversed);

            Assert.AreEqual(r1["a"], r2["a"]);
            Assert.AreEqual(r1["b"], r2["b"]);
            Assert.AreEqual(r1["c"], r2["c"]);
        }

        [Test]
        public void BothCollidingOptedOutNoAssignmentsProduced()
        {
            var input = new List<ToggleInfo> {
                Info("a", "Hat", cur: "Hat", optedOut: true),
                Info("b", "Hat", cur: "Hat", optedOut: true),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual(0, result.Count);
        }

        private static ToggleInfo Set(string id, string menu, string cur, bool byTool = false, bool held = false)
        {
            ToggleInfo info = Info(id, menu, cur);
            info.UsesGlobalParam = true;
            info.AssignedByTool = byTool;
            info.HeldPreviously = held;
            return info;
        }

        [Test]
        public void ExistingCustomGlobalParamIsKept()
        {
            var input = new List<ToggleInfo> { Set("a", "Hat", "MyHatParam") };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("MyHatParam", result["a"]);
        }

        [Test]
        public void ExistingGlobalParamSurvivesMenuRename()
        {
            var input = new List<ToggleInfo> { Set("a", "Hats", "Hat", held: true) };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["a"]);
        }

        [Test]
        public void ValueWrittenByToolThisSessionFollowsMenuPath()
        {
            var input = new List<ToggleInfo> { Set("a", "Hats", "Hat", byTool: true, held: true) };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hats", result["a"]);
        }

        [Test]
        public void GlobalParamNotInUseAdoptsMenuPath()
        {
            var input = new List<ToggleInfo> { Info("a", "Hat", cur: "Old") };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["a"]);
        }

        [Test]
        public void DuplicateOriginalKeepsNameCopyGetsSuffix()
        {
            var input = new List<ToggleInfo> {
                Set("a", "Hat", "Hat"),
                Set("b", "Hat", "Hat", held: true),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["b"]);
            Assert.AreEqual("Hat 2", result["a"]);
        }

        [Test]
        public void ToolOwnedDuplicateOriginalKeepsNameCopyGetsSuffix()
        {
            var input = new List<ToggleInfo> {
                Set("a", "Hat", "Hat"),
                Set("b", "Hat", "Hat", byTool: true, held: true),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["b"]);
            Assert.AreEqual("Hat 2", result["a"]);
        }

        [Test]
        public void CustomNameCollisionLoserFallsBackToMenuPath()
        {
            var input = new List<ToggleInfo> {
                Set("a", "Bag", "Shared"),
                Set("b", "Hat", "Shared", held: true),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Shared", result["b"]);
            Assert.AreEqual("Bag", result["a"]);
        }

        [Test]
        public void ExistingGlobalParamReservesNameAgainstNewToggle()
        {
            var input = new List<ToggleInfo> {
                Set("b", "Bag", "Hat"),
                Info("a", "Hat"),
            };

            IReadOnlyDictionary<string, string> result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["b"]);
            Assert.AreEqual("Hat 2", result["a"]);
        }
    }
}
