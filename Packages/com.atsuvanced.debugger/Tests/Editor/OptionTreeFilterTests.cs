using NUnit.Framework;

namespace Atsuvanced.Debugger.Tests
{
    public class OptionTreeFilterTests
    {
        private OptionDefinition _hp, _speed, _target, _warp, _goblinHp;
        private OptionRegistry _r;

        [SetUp]
        public void SetUp()
        {
            _r = new OptionRegistry();
            _r.AddOption(_hp = OptionDefinition.Create("Hp", () => 1, category: "Player/Status"));
            _r.AddOption(_speed = OptionDefinition.Create("Speed", () => 1f, category: "Player/Move"));
            _r.AddOption(_target = OptionDefinition.Create("Target", () => "", v => { }, "Map",
                group: new OptionGroupAttribute("warp")));
            _r.AddOption(_warp = OptionDefinition.FromMethod("Warp", () => { }, "Map", 1,
                group: new OptionGroupAttribute("warp")));
            _r.AddOption(_goblinHp = OptionDefinition.Create("Hp", () => 1, category: "Enemies/Goblin"));
        }

        [Test]
        public void EmptyQuery_IsInactive()
        {
            var f = _r.CreateFilter(" ");

            Assert.IsFalse(f.IsActive);
            Assert.IsTrue(f.IsVisible(_speed));
            Assert.IsNull(f.GetExpanded(_r.FindCategory("Player")));
        }

        [Test]
        public void OnlyBranchesWithHitsAreVisibleAndExpanded()
        {
            var f = _r.CreateFilter("hp");

            Assert.IsTrue(f.IsVisible(_hp));
            Assert.IsTrue(f.IsVisible(_goblinHp));
            Assert.IsFalse(f.IsVisible(_speed));

            Assert.AreEqual(true, f.GetExpanded(_r.FindCategory("Player")));
            Assert.AreEqual(true, f.GetExpanded(_r.FindCategory("Player/Status")));
            Assert.AreEqual(false, f.GetExpanded(_r.FindCategory("Player/Move")));
            Assert.IsFalse(f.IsVisible(_r.FindCategory("Map")));
            Assert.IsTrue(f.IsVisible(_r.Root));
        }

        [Test]
        public void HitOnOneGroupMember_ShowsWholeGroup()
        {
            var f = _r.CreateFilter("target");

            Assert.IsTrue(f.IsHit(_target));
            Assert.IsFalse(f.IsHit(_warp));
            Assert.IsTrue(f.IsVisible(_warp), "Group partner stays visible so the UI unit is not split.");
            Assert.IsTrue(f.IsVisible(_r.FindGroup("Map/warp")));
        }

        [Test]
        public void GroupLabel_IsSearchable()
        {
            var f = _r.CreateFilter("warp");

            Assert.IsTrue(f.IsHit(_target));
            Assert.IsTrue(f.IsHit(_warp));
        }
    }
}