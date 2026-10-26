using System;
using System.Collections.Generic;
using NUnit.Framework;
using UmeVrcfQol.Tools;
using UnityEngine;

namespace WhyKnot.VrcfQol.Tests {

    public class MigrateIntoFlipbookToolTests {

        [Serializable]
        private class FakeState {
            [SerializeReference] public List<object> actions = new List<object>();
        }

        [Serializable]
        private class FakeIcon {
            public string id;
            public Texture2D objRef;
        }

        [Serializable]
        private class FakeToggle {
            public string name;
            public FakeState state = new FakeState();
            public bool saved;
            public bool simpleOutTransition = true;
            public float defaultSliderValue;
            public string globalParam;
            public FakeIcon icon;
            public FakeState transitionStateIn = new FakeState();
            [NonSerialized] public bool addMenuItem = true;
            [Obsolete] public bool includeInRest;
            [SerializeField] private string exclusiveTag;
            private int scratch;

            public void SetPrivate(string tag, int value) {
                exclusiveTag = tag;
                scratch = value;
            }
        }

        [Serializable]
        private class FakeAction {
            public string clip;
        }

        [Test]
        public void DefaultToggle_ReportsNothing() {
            var toggle = new FakeToggle { name = "Hat" };
            toggle.state.actions.Add(new FakeAction { clip = "a" });

            var changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "name", "state");

            CollectionAssert.IsEmpty(changed);
        }

        [Test]
        public void ChangedSettings_AreReported() {
            var toggle = new FakeToggle {
                saved = true,
                simpleOutTransition = false,
                defaultSliderValue = 0.5f,
                globalParam = "Hat",
                icon = new FakeIcon { id = "abc" },
            };
            toggle.transitionStateIn.actions.Add(new FakeAction { clip = "in" });
            toggle.SetPrivate("Hats", 3);

            var changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "name", "state");

            CollectionAssert.AreEquivalent(
                new[] { "saved", "simpleOutTransition", "defaultSliderValue", "globalParam", "icon", "transitionStateIn", "exclusiveTag" },
                changed);
        }

        [Test]
        public void NullAndEmptyValues_CountAsDefault() {
            var toggle = new FakeToggle { globalParam = "", icon = new FakeIcon(), transitionStateIn = null };

            var changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "name", "state");

            CollectionAssert.IsEmpty(changed);
        }

        [Test]
        public void NonSerializedAndObsoleteFields_AreIgnored() {
            var toggle = new FakeToggle { addMenuItem = false };
#pragma warning disable 612
            toggle.includeInRest = true;
#pragma warning restore 612

            var changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "name", "state");

            CollectionAssert.IsEmpty(changed);
        }

        private class FakeFlipbook {
            public List<object> pages = new List<object>();
        }

        private class FakeHolder {
            public FakeState state = new FakeState();
        }

        private class FakeRoot {
            public FakeHolder content = new FakeHolder();
        }

        [Test]
        public void Walk_ResolvesTheFlipbookAtThePagePathPrefix() {
            var root = new FakeRoot();
            var first = new FakeFlipbook();
            var second = new FakeFlipbook();
            root.content.state.actions.Add(first);
            root.content.state.actions.Add(new FakeAction());
            root.content.state.actions.Add(second);

            var resolved = DuplicateActionTool.Walk(root, "content.state.actions.Array.data[2]");

            Assert.AreSame(second, resolved);
        }
    }
}
