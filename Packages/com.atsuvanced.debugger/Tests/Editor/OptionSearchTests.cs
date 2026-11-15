using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Atsuvanced.Debugger.Tests
{
    public class OptionSearchTests
    {
        private readonly List<OptionSearchResult> _results = new();
        private OptionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new OptionRegistry();
            _registry.AddOption(OptionDefinition.Create("Hp", () => 1, category: "Player/Status"));
            _registry.AddOption(OptionDefinition.Create("Max Hp", () => 1, category: "Player/Status"));
            _registry.AddOption(OptionDefinition.Create("Speed", () => 1f, category: "Player/Move"));
            _registry.AddOption(OptionDefinition.FromMethod("全回復", () => { }, "プレイヤー/ステータス",
                description: "HP と MP を最大まで回復"));
            _registry.AddOption(OptionDefinition.Create("Hp", () => 1, category: "Enemies/Goblin"));
        }

        private string[] Search(string query)
        {
            _registry.Search(query, _results);
            return _results.Select(r => r.Option.Id).ToArray();
        }

        [Test]
        public void EmptyQuery_ReturnsNothing()
        {
            Assert.IsEmpty(Search("  "));
        }

        [Test]
        public void NamePrefix_RanksAboveContains()
        {
            var ids = Search("hp");

            Assert.AreEqual(4, ids.Length); // includes description hit "HP と MP ..."
            Assert.That(ids.Take(2), Is.EquivalentTo(new[] { "Player/Status/Hp", "Enemies/Goblin/Hp" }));
            Assert.AreEqual("Player/Status/Max Hp", ids[2]);
            Assert.AreEqual("プレイヤー/ステータス/全回復", ids[3]);
        }

        [Test]
        public void MultipleTerms_AreAnded_AndMatchCategories()
        {
            CollectionAssert.AreEqual(new[] { "Player/Status/Hp", "Player/Status/Max Hp" }, Search("player hp"));
            CollectionAssert.AreEqual(new[] { "Enemies/Goblin/Hp" }, Search("gob hp"));
        }

        [Test]
        public void PathTerm_SpansSegments()
        {
            CollectionAssert.AreEqual(new[] { "Player/Move/Speed" }, Search("move/sp"));
        }

        [Test]
        public void Japanese_IgnoresKanaTypeAndWidth()
        {
            CollectionAssert.AreEqual(new[] { "プレイヤー/ステータス/全回復" }, Search("すてーたす"));
            CollectionAssert.AreEqual(new[] { "プレイヤー/ステータス/全回復" }, Search("ＭＰ"));
            CollectionAssert.AreEqual(new[] { "プレイヤー/ステータス/全回復" }, Search("回復"));
        }

        [Test]
        public void ResultsReflectDynamicChanges()
        {
            var opt = OptionDefinition.Create("Hp Regen", () => 1, category: "Player/Status");
            _registry.AddOption(opt);
            Assert.Contains("Player/Status/Hp Regen", Search("regen"));

            _registry.RemoveOption(opt);
            Assert.IsEmpty(Search("regen"));
        }
    }
}