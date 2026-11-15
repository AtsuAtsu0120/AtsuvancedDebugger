using System.Linq;
using NUnit.Framework;

namespace Atsuvanced.Debugger.Tests
{
    public class OptionGroupTests
    {
        [Test]
        public void MembersWithSameKey_FormOneEntry_AndRemainSeparateOptions()
        {
            var r = new OptionRegistry();
            r.AddContainer(new WarpOptions());
            var map = r.FindCategory("Map");

            Assert.AreEqual(4, map.Options.Count, "Definitions stay separate (SRDebugger compatible).");
            CollectionAssert.AreEqual(
                new[] { "Property:Map/ShowMinimap", "Group:Map/warp(2)", "Method:Map/Reload" },
                map.Entries.Select(e => e.ToString()));

            var group = map.FindGroup("warp");
            Assert.AreEqual("ワープ", group.Label);
            CollectionAssert.AreEqual(new[] { "Target", "Warp" }, group.Members.Select(m => m.Name));
            Assert.AreSame(group, r.GetGroup(group.Members[0]));
            Assert.AreSame(group, r.FindGroup("Map/warp"));
        }

        [Test]
        public void OnAction_KeepsInputPendingUntilButtonInvoked()
        {
            var c = new WarpOptions();
            var r = new OptionRegistry();
            r.AddContainer(c);
            var group = r.FindGroup("Map/warp");
            var target = group.Members[0];
            var warp = group.Members[1];

            Assert.AreEqual(OptionGroupCommit.OnAction, group.EffectiveCommit);

            group.SetValue(target, "Dungeon");
            Assert.AreEqual("Town", c.Target, "Typing must not touch the property.");
            Assert.AreEqual("Dungeon", group.GetDisplayValue(target));
            Assert.IsTrue(group.HasPendingChanges);

            group.Invoke(warp);
            Assert.AreEqual("Dungeon", c.WarpedTo, "Pending value is applied before the action runs.");
            Assert.AreEqual(1, c.SetCount);
            Assert.IsFalse(group.HasPendingChanges);
        }

        [Test]
        public void DiscardPending_RevertsToCurrentValue()
        {
            var c = new WarpOptions();
            var r = new OptionRegistry();
            r.AddContainer(c);
            var group = r.FindGroup("Map/warp");

            group.SetValue(group.Members[0], "Castle");
            group.DiscardPending();

            Assert.AreEqual("Town", group.GetDisplayValue(group.Members[0]));
            Assert.AreEqual(0, c.SetCount);
        }

        [Test]
        public void GroupWithoutMethod_IsImmediate()
        {
            var c = new ImmediateGroup();
            var r = new OptionRegistry();
            r.AddContainer(c);
            var group = r.FindGroup("Default/speed");

            Assert.AreEqual(OptionGroupLayout.Vertical, group.Layout);
            Assert.AreEqual(OptionGroupCommit.Immediate, group.EffectiveCommit);

            group.SetValue(group.Members.Single(m => m.Name == "Speed"), 3f);
            Assert.AreEqual(3f, c.Speed);
        }

        [Test]
        public void DynamicMembers_CreateAndRemoveGroup()
        {
            var r = new OptionRegistry();
            var d = new DynamicOptionContainer();
            r.AddContainer(d);
            OptionGroup added = null, removed = null;
            r.GroupAdded += g => added = g;
            r.GroupRemoved += g => removed = g;

            var text = "";
            var input = OptionDefinition.Create("Command", () => text, v => text = v, "Cheat",
                group: new OptionGroupAttribute("cmd"));
            var run = OptionDefinition.FromMethod("Run", () => { }, "Cheat", 1, group: new OptionGroupAttribute("cmd"));

            d.AddOption(input);
            d.AddOption(run);
            Assert.NotNull(added);
            Assert.AreEqual(2, added.Members.Count);

            d.RemoveOption(input);
            Assert.IsNull(removed);
            Assert.AreEqual(OptionGroupCommit.Immediate, added.EffectiveCommit);

            d.RemoveOption(run);
            Assert.AreSame(added, removed);
            Assert.IsNull(r.FindCategory("Cheat"));
        }

        [Test]
        public void PinningOneMember_PinsWholeGroup()
        {
            var r = new OptionRegistry();
            r.AddContainer(new WarpOptions());

            r.PinByName("Warp");
            CollectionAssert.AreEquivalent(new[] { "Target", "Warp" }, r.PinnedOptions.Select(o => o.Name));

            r.UnpinByName("Target");
            Assert.IsEmpty(r.PinnedOptions);
        }

        [Test]
        public void JoiningPinnedGroup_IsPinnedToo()
        {
            var r = new OptionRegistry();
            var d = new DynamicOptionContainer();
            r.AddContainer(d);
            var a = OptionDefinition.FromMethod("A", () => { }, "G", group: new OptionGroupAttribute("g"));
            d.AddOption(a);
            r.Pin(a);

            var b = OptionDefinition.FromMethod("B", () => { }, "G", group: new OptionGroupAttribute("g"));
            d.AddOption(b);

            Assert.IsTrue(r.IsPinned(b));
        }

        private class WarpOptions
        {
            public int SetCount;
            public string WarpedTo;
            private string _target = "Town";

            [OptionCategory("Map")]
            [OptionGroup("warp", Label = "ワープ")]
            [OptionSort(0)]
            public string Target
            {
                get => _target;
                set
                {
                    _target = value;
                    SetCount++;
                }
            }

            [OptionCategory("Map")]
            [OptionSort(-1)]
            public bool ShowMinimap { get; set; }

            [OptionCategory("Map")]
            [OptionGroup("warp")]
            [OptionSort(1)]
            public void Warp()
            {
                WarpedTo = Target;
            }

            [OptionCategory("Map")]
            [OptionSort(5)]
            public void Reload()
            {
            }
        }

        private class ImmediateGroup
        {
            [OptionGroup("speed", Layout = OptionGroupLayout.Vertical)]
            public float Speed { get; set; }

            [OptionGroup("speed")] public bool Fast { get; set; }
        }
    }
}