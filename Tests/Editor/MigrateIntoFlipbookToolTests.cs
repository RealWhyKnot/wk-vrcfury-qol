#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UmeVrcfQol.Tools;
using UnityEngine;

namespace WhyKnot.VrcfQol.Tests
{
    public sealed class MigrateIntoFlipbookToolTests
    {
        private static readonly string[] ChangedSettingNames =
        {
            "Saved", "SimpleOutTransition", "DefaultSliderValue", "GlobalParam", "Icon", "TransitionStateIn", "_exclusiveTag",
        };

        [Test]
        public void DefaultToggleReportsNothing()
        {
            var toggle = new FakeToggle { Name = "Hat" };
            toggle.State.Actions.Add(new FakeAction { Clip = "a" });

            List<string> changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "Name", "State");

            CollectionAssert.IsEmpty(changed);
        }

        [Test]
        public void ChangedSettingsAreReported()
        {
            var toggle = new FakeToggle
            {
                Saved = true,
                SimpleOutTransition = false,
                DefaultSliderValue = 0.5f,
                GlobalParam = "Hat",
                Icon = new FakeIcon { Id = "abc" },
            };
            toggle.TransitionStateIn?.Actions.Add(new FakeAction { Clip = "in" });
            toggle.SetPrivate("Hats", 3);

            List<string> changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "Name", "State");

            Assert.IsTrue(toggle.HasPrivate("Hats", 3));
            CollectionAssert.AreEquivalent(ChangedSettingNames, changed);
        }

        [Test]
        public void NullAndEmptyValuesCountAsDefault()
        {
            var toggle = new FakeToggle
            {
                GlobalParam = "",
                Icon = new FakeIcon { ObjRef = null },
                TransitionStateIn = null,
            };

            List<string> changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "Name", "State");

            CollectionAssert.IsEmpty(changed);
        }

        [Test]
        public void NonSerializedAndObsoleteFieldsAreIgnored()
        {
            var toggle = new FakeToggle { AddMenuItem = false };
            FieldInfo? obsolete = typeof(FakeToggle).GetField("IncludeInRest");
            if (obsolete == null)
            {
                Assert.Fail("FakeToggle.IncludeInRest is missing.");
                return;
            }

            obsolete.SetValue(toggle, value: false);

            List<string> changed = MigrateIntoFlipbookTool.ChangedSerializedFields(toggle, new FakeToggle(), "Name", "State");

            CollectionAssert.IsEmpty(changed);
        }

        [Test]
        public void WalkResolvesTheFlipbookAtThePagePathPrefix()
        {
            var root = new FakeRoot();
            var first = new FakeFlipbook();
            var second = new FakeFlipbook();
            root.Content.State.Actions.Add(first);
            root.Content.State.Actions.Add(new FakeAction());
            root.Content.State.Actions.Add(second);

            object? resolved = DuplicateActionTool.Walk(root, "Content.State.Actions.Array.data[2]");

            Assert.AreSame(second, resolved);
            Assert.AreEqual(0, second.Pages.Count);
        }

        private sealed class FakeState
        {
            [SerializeReference] public List<object> Actions = new();
        }

        private sealed class FakeIcon
        {
            public string? Id;
            public Texture2D? ObjRef;
        }

        private sealed class FakeToggle
        {
            public string? Name;
            public FakeState State = new();
            public bool Saved;
            public bool SimpleOutTransition = true;
            public float DefaultSliderValue;
            public string? GlobalParam;
            public FakeIcon? Icon;
            public FakeState? TransitionStateIn = new();
            [NonSerialized] public bool AddMenuItem = true;
            [Obsolete("Mirrors a field VRCFury no longer reads.")] public bool IncludeInRest = true;
            [SerializeField] private string? _exclusiveTag;
            private int _scratch;

            public void SetPrivate(string tag, int value)
            {
                _exclusiveTag = tag;
                _scratch = value;
            }

            public bool HasPrivate(string tag, int value)
            {
                return string.Equals(_exclusiveTag, tag, StringComparison.Ordinal) && _scratch == value;
            }
        }

        private sealed class FakeAction
        {
            public string? Clip;
        }

        private sealed class FakeFlipbook
        {
            public List<object> Pages = new();
        }

        private sealed class FakeHolder
        {
            public FakeState State = new();
        }

        private sealed class FakeRoot
        {
            public FakeHolder Content = new();
        }
    }
}
