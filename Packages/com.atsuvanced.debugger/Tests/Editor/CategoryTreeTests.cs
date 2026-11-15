using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Atsuvanced.Debugger.Tests
{
    public class CategoryTreeTests
    {
        [TestCase("a/b/c", "a/b/c")]
        [TestCase(" a / b //c/ ", "a/b/c")]
        [TestCase("/", "")]
        [TestCase(null, "")]
        public void CategoryPath_Normalize(string input, string expected)
        {
            Assert.AreEqual(expected, CategoryPath.Normalize(input));
        }

        [Test]
        public void CategoryPath_IsSameOrDescendant()
        {
            Assert.IsTrue(CategoryPath.IsSameOrDescendant("A/B/C", "A/B"));
            Assert.IsTrue(CategoryPath.IsSameOrDescendant("A/B", "A/B"));
            Assert.IsFalse(CategoryPath.IsSameOrDescendant("A/BC", "A/B"));
            Assert.IsFalse(CategoryPath.IsSameOrDescendant("A", "A/B"));
        }

        [Test]
        public void ClassCategory_IsParentOfMemberCategories()
        {
            var r = new OptionRegistry();
            r.AddContainer(new PlayerOptions());

            Assert.AreEqual("Player/Status/Buffs", r.Options.Single(o => o.Name == "Haste").Category);
            Assert.AreEqual("Player/Status", r.Options.Single(o => o.Name == "Hp").Category);
            Assert.AreEqual("Player", r.Options.Single(o => o.Name == "Kill").Category);

            var buffs = r.FindCategory("Player/Status/Buffs");
            Assert.NotNull(buffs);
            Assert.AreEqual(3, buffs.Depth);
            Assert.AreEqual("Buffs", buffs.Name);
            Assert.AreEqual(3, r.FindCategory("Player").TotalOptionCount);
        }

        [Test]
        public void Prefix_PlacesInstancesUnderTheirOwnCategory()
        {
            var r = new OptionRegistry();
            r.AddContainer(new Enemy(), "Enemies/Goblin#1");
            r.AddContainer(new Enemy(), "Enemies/Goblin#2");

            var enemies = r.FindCategory("Enemies");
            CollectionAssert.AreEqual(new[] { "Goblin#1", "Goblin#2" }, enemies.Children.Select(c => c.Name));
            Assert.AreEqual("Enemies/Goblin#1", r.FindCategory("Enemies/Goblin#1").Options.Single().Category);
            Assert.NotNull(r.FindCategory("Enemies/Goblin#2/AI"));
            Assert.AreEqual(4, enemies.TotalOptionCount);
        }

        [Test]
        public void EmptyCategories_ArePrunedAndEventsRaised()
        {
            var r = new OptionRegistry();
            var added = new List<string>();
            var removed = new List<string>();
            r.CategoryAdded += n => added.Add(n.Path);
            r.CategoryRemoved += n => removed.Add(n.Path);

            var e1 = new Enemy();
            r.AddContainer(e1, "Enemies/Goblin#1");
            var e2 = new Enemy();
            r.AddContainer(e2, "Enemies/Goblin#2");
            CollectionAssert.AreEqual(
                new[]
                {
                    "Enemies", "Enemies/Goblin#1", "Enemies/Goblin#1/AI", "Enemies/Goblin#2", "Enemies/Goblin#2/AI"
                },
                added);

            r.RemoveContainer(e1);
            CollectionAssert.AreEquivalent(new[] { "Enemies/Goblin#1/AI", "Enemies/Goblin#1" }, removed);
            Assert.NotNull(r.FindCategory("Enemies"));

            r.RemoveContainer(e2);
            Assert.IsNull(r.FindCategory("Enemies"));
            Assert.AreEqual(0, r.Root.Children.Count);
            Assert.AreEqual(0, r.Root.TotalOptionCount);
        }

        [Test]
        public void RemoveCategory_RemovesSubtree()
        {
            var r = new OptionRegistry();
            r.AddContainer(new PlayerOptions());
            r.AddContainer(new Enemy(), "Enemies");

            Assert.AreEqual(2, r.RemoveCategory("Player/Status"));

            Assert.IsNull(r.FindCategory("Player/Status"));
            Assert.AreEqual(1, r.FindCategory("Player").TotalOptionCount);
            Assert.AreEqual(2, r.FindCategory("Enemies").TotalOptionCount);
        }

        [Test]
        public void DynamicContainer_CreatesAndRemovesCategoriesOnTheFly()
        {
            var r = new OptionRegistry();
            var d = new DynamicOptionContainer();
            r.AddContainer(d, "Network");

            var opt = OptionDefinition.Create("Latency", () => 10, category: "Sim/Lag");
            d.AddOption(opt);
            Assert.AreEqual("Network/Sim/Lag", opt.Category);
            Assert.NotNull(r.FindCategory("Network/Sim/Lag"));

            d.RemoveOption(opt);
            Assert.IsNull(r.FindCategory("Network"));
        }

        [Test]
        public void Options_AreOrderedBySortPriorityWithinCategory()
        {
            var r = new OptionRegistry();
            r.AddOption(OptionDefinition.FromMethod("b", () => { }, "C", 5));
            r.AddOption(OptionDefinition.FromMethod("a", () => { }, "C", 1));
            r.AddOption(OptionDefinition.FromMethod("c", () => { }, "C", 5));

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, r.FindCategory("C").Options.Select(o => o.Name));
        }

        [Test]
        public void PinCategory_IncludesSubCategories()
        {
            var r = new OptionRegistry();
            r.AddContainer(new PlayerOptions());

            r.PinCategory("Player/Status");

            CollectionAssert.AreEquivalent(new[] { "Haste", "Hp" }, r.PinnedOptions.Select(o => o.Name));
        }

        [OptionCategory("Player")]
        private class PlayerOptions
        {
            [OptionCategory("Status", "Buffs")] public bool Haste { get; set; }

            [OptionCategory("Status")] public int Hp { get; set; }

            public void Kill()
            {
            }
        }

        private class Enemy
        {
            public int Hp { get; set; }

            [OptionCategory("AI")] public bool Aggressive { get; set; }
        }
    }
}