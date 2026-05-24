using System.Collections.Generic;
using NUnit.Framework;
using UmeVrcfQol.Tools;

namespace WhyKnot.VrcfQol.Tests {

    public class AutoGlobalParameterToolTests {

        private static ToggleInfo Info(string id, string menu, string cur = "", bool optedOut = false) {
            return new ToggleInfo {
                GlobalObjectId = id,
                MenuPath = menu,
                CurrentGlobalParam = cur,
                IsOptedOut = optedOut,
            };
        }

        [Test]
        public void NoCollisions_AssignsMenuPathToEach() {
            var input = new List<ToggleInfo> {
                Info("a", "Hat"),
                Info("b", "Glasses"),
                Info("c", "Backpack"),
            };

            var result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["a"]);
            Assert.AreEqual("Glasses", result["b"]);
            Assert.AreEqual("Backpack", result["c"]);
            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void TwoColliding_FirstKeepsName_SecondGetsTwo() {
            var input = new List<ToggleInfo> {
                Info("a", "Hat"),
                Info("b", "Hat"),
            };

            var result = AutoGlobalParameterTool.ComputeAssignments(input);

            // GlobalObjectId order: "a" < "b" lexically, so "a" keeps the bare name.
            Assert.AreEqual("Hat", result["a"]);
            Assert.AreEqual("Hat 2", result["b"]);
        }

        [Test]
        public void ThreeColliding_GetSequentialSuffixes() {
            var input = new List<ToggleInfo> {
                Info("a", "Hat"),
                Info("b", "Hat"),
                Info("c", "Hat"),
            };

            var result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.AreEqual("Hat", result["a"]);
            Assert.AreEqual("Hat 2", result["b"]);
            Assert.AreEqual("Hat 3", result["c"]);
        }

        [Test]
        public void EmptyMenuPath_NotAssigned() {
            var input = new List<ToggleInfo> {
                Info("a", ""),
                Info("b", "   "),
                Info("c", "Hat"),
            };

            var result = AutoGlobalParameterTool.ComputeAssignments(input);

            Assert.IsFalse(result.ContainsKey("a"));
            Assert.IsFalse(result.ContainsKey("b"));
            Assert.AreEqual("Hat", result["c"]);
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void OptedOutHoldsBareName_NonOptedOutGetsSuffix() {
            var input = new List<ToggleInfo> {
                Info("a", "Hat", cur: "Hat", optedOut: true),
                Info("b", "Hat"),
            };

            var result = AutoGlobalParameterTool.ComputeAssignments(input);

            // Opted-out toggle holds 'Hat' (no assignment in result map).
            Assert.IsFalse(result.ContainsKey("a"));
            Assert.AreEqual("Hat 2", result["b"]);
        }

        [Test]
        public void OptedOutHoldsSuffixedName_NonOptedOutRoutesAround() {
            var input = new List<ToggleInfo> {
                Info("a", "Foo", cur: "Foo 2", optedOut: true),
                Info("b", "Foo"),
                Info("c", "Foo"),
            };

            var result = AutoGlobalParameterTool.ComputeAssignments(input);

            // 'Foo 2' is taken by the opted-out toggle.
            // b (first by id order) gets 'Foo'.
            // c looks for the next free: 'Foo 2' taken, 'Foo 3' free.
            Assert.IsFalse(result.ContainsKey("a"));
            Assert.AreEqual("Foo", result["b"]);
            Assert.AreEqual("Foo 3", result["c"]);
        }

        [Test]
        public void StableOrder_InputPermutationDoesNotChangeResult() {
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

            var r1 = AutoGlobalParameterTool.ComputeAssignments(forward);
            var r2 = AutoGlobalParameterTool.ComputeAssignments(reversed);

            Assert.AreEqual(r1["a"], r2["a"]);
            Assert.AreEqual(r1["b"], r2["b"]);
            Assert.AreEqual(r1["c"], r2["c"]);
        }

        [Test]
        public void BothCollidingOptedOut_NoAssignmentsProduced() {
            var input = new List<ToggleInfo> {
                Info("a", "Hat", cur: "Hat", optedOut: true),
                Info("b", "Hat", cur: "Hat", optedOut: true),
            };

            var result = AutoGlobalParameterTool.ComputeAssignments(input);

            // Both opted-out; the tool produces no assignments. The collision
            // persists -- which is the correct outcome (user explicitly chose
            // to disable auto-fix on both).
            Assert.AreEqual(0, result.Count);
        }
    }
}
