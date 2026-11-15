using System.Collections.Generic;
using System.Linq;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    public class TabStructureTests
    {
        private OptionRegistry _r;
        private DebugTabRegistry _tabs;

        private void Add(string category, string name)
        {
            _r.AddOption(OptionDefinition.FromMethod(name, () => { }, category));
        }

        [SetUp]
        public void SetUp()
        {
            _r = new OptionRegistry();
            _tabs = new DebugTabRegistry();
            _tabs.Add(new DebugTabDefinition("system", "System", 0));
            _tabs.Add(new DebugTabDefinition("console", "Console",
                () => DebugPage.FromElement("c", () => new VisualElement()), 200));
        }

        // ---- Sections -----------------------------------------------------------------------

        [Test]
        public void SectionCollection_OrdersAndReplaces()
        {
            var c = new DebugSectionCollection();
            var changes = 0;
            c.Changed += () => changes++;
            c.Add(new DebugSection("b", "B", () => null, 2));
            c.Add(new DebugSection("a", "A", () => null, 1));
            c.Add(new DebugSection("b", "B2", () => null, 0));

            CollectionAssert.AreEqual(new[] { "B2", "A" }, c.Select(s => s.Title));
            Assert.IsTrue(c.Remove("a"));
            Assert.IsFalse(c.Remove("a"));
            Assert.AreEqual(4, changes);
        }

        [Test]
        public void SinglePageConstructor_CreatesOneSection()
        {
            var tab = _tabs.Find("console");
            Assert.AreEqual(1, tab.Sections.Count);
            Assert.AreEqual(DebugTabDefinition.MainSectionId, tab.Sections[0].Id);
        }

        // ---- Option categories → tabs / sections ------------------------------------------------

        [Test]
        public void TopLevelCategoriesBecomeTabsBetweenSystemAndConsole()
        {
            Add("Player/Status", "Hp");
            Add("World", "Pause");
            new OptionTabsSync(_r, _tabs);

            CollectionAssert.AreEqual(new[] { "system", "option:Player", "option:World", "console" },
                _tabs.Tabs.Select(t => t.Id));
        }

        [Test]
        public void SectionsAreGeneralPlusLevel2_AndFollowDynamicChanges()
        {
            Add("Player/Status", "Hp");
            new OptionTabsSync(_r, _tabs);
            var player = _tabs.Find("option:Player");
            CollectionAssert.AreEqual(new[] { "Status" }, player.Sections.Select(s => s.Title));

            Add("Player", "Heal"); // direct option → General appears first
            Add("Player/Wallet", "Gold");
            CollectionAssert.AreEqual(new[] { DebuggerStrings.Get("section.general"), "Status", "Wallet" },
                player.Sections.Select(s => s.Title));

            _r.RemoveCategory("Player/Wallet");
            CollectionAssert.AreEqual(new[] { DebuggerStrings.Get("section.general"), "Status" },
                player.Sections.Select(s => s.Title));

            _r.RemoveCategory("Player");
            Assert.IsNull(_tabs.Find("option:Player"), "Tab disappears with its category.");
        }

        [Test]
        public void UserSectionsOnGeneratedTabsAreKept()
        {
            Add("Player/Status", "Hp");
            new OptionTabsSync(_r, _tabs);
            var player = _tabs.Find("option:Player");
            player.Sections.Add(new DebugSection("custom", "Custom", () => null));

            Add("Player/Wallet", "Gold");
            _r.RemoveCategory("Player/Wallet");

            Assert.IsTrue(player.Sections.Contains("custom"));
        }

        [Test]
        public void SystemInfoCategoriesBecomeSections()
        {
            var info = new SystemInfoRegistry();
            var system = _tabs.Find("system");
            new SystemInfoSectionsSync(info, system, c => new SystemInfoPage(c));

            info.Add(InfoEntry.Create("a", 1), "Application");
            info.Add(InfoEntry.Create("b", 2), "Network");
            CollectionAssert.AreEqual(new[] { "Application", "Network" }, system.Sections.Select(s => s.Title));
            Assert.AreEqual("1", system.Sections[1].Badge());
        }

        [Test]
        public void SectionedPage_SwitchesSectionsWithLifecycle()
        {
            var log = new List<string>();
            var tab = new DebugTabDefinition("t", "T", 0);
            tab.Sections.Add(new DebugSection("a", "A", () => new LogPage("A", log), 0));
            tab.Sections.Add(new DebugSection("b", "B", () => new LogPage("B", log), 1));
            _tabs.Add(tab);
            var nav = new PageNavigator(_tabs, null, () => { });
            nav.SetActive(true);
            nav.SelectTab("t");

            var root = (SectionedPage)nav.CurrentPage;
            Assert.AreEqual("a", root.SelectedSectionId);
            Assert.AreEqual("A", root.Title, "The tab bar names the tab; the header shows the section.");

            root.Select("b");
            CollectionAssert.AreEqual(new[] { "A:show", "A:hide", "B:show" }, log);

            tab.Sections.Remove("b");
            Assert.AreEqual("a", root.SelectedSectionId, "Falls back when the selected section disappears.");
            Assert.Contains("B:dispose", log);
            Assert.AreEqual(PageChrome.Default, root.Chrome);
        }

        [Test]
        public void SingleSectionTab_UsesThePageChrome()
        {
            _tabs.Add(new DebugTabDefinition("full", "Full", () => new FullPage(), 0));
            var nav = new PageNavigator(_tabs, null, () => { });
            nav.SetActive(true);
            nav.SelectTab("full");
            Assert.AreEqual(PageChrome.None, nav.CurrentPage.Chrome);
        }

        // ---- Sectioned root page ---------------------------------------------------------------

        private sealed class LogPage : DebugPage
        {
            private readonly List<string> _log;

            public LogPage(string t, List<string> log) : base(t)
            {
                _log = log;
            }

            protected override VisualElement CreateContent()
            {
                return new VisualElement();
            }

            protected override void OnShow()
            {
                _log.Add(Title + ":show");
            }

            protected override void OnHide()
            {
                _log.Add(Title + ":hide");
            }

            protected override void OnDispose()
            {
                _log.Add(Title + ":dispose");
            }
        }

        private sealed class FullPage : DebugPage
        {
            public override PageChrome Chrome => PageChrome.None;

            protected override VisualElement CreateContent()
            {
                return new VisualElement();
            }
        }
    }

    public class OptionListModelTests
    {
        private OptionRegistry _r;

        private void Add(string category, string name)
        {
            _r.AddOption(OptionDefinition.FromMethod(name, () => { }, category));
        }

        [SetUp]
        public void SetUp()
        {
            _r = new OptionRegistry();
            Add("Player/Status", "Hp");
            Add("Player/Status/Buffs", "Haste");
            Add("Player/Status/Buffs/Timers", "Haste Time");
            Add("Player/Status/Debuffs", "Poison");
            Add("Player", "Heal");
        }

        private static string[] Names(OptionListModel m)
        {
            return m.Rows.Select(r =>
                r.Kind == OptionListModel.RowKind.Category ? "[" + r.Node.Name + "]" : r.Entry.Option.Name).ToArray();
        }

        [Test]
        public void GeneralSection_ShowsOnlyDirectOptions()
        {
            var m = new OptionListModel(_r, "Player", true);
            m.Refresh();
            CollectionAssert.AreEqual(new[] { "Heal" }, Names(m));
        }

        [Test]
        public void Level3IsCollapsedAndOnlyOneBranchOpens()
        {
            var m = new OptionListModel(_r, "Player/Status", false);
            m.Refresh();
            CollectionAssert.AreEqual(new[] { "Hp", "[Buffs]", "[Debuffs]" }, Names(m));

            m.Toggle(_r.FindCategory("Player/Status/Buffs"));
            m.Toggle(_r.FindCategory("Player/Status/Buffs/Timers"));
            CollectionAssert.AreEqual(new[] { "Hp", "[Buffs]", "Haste", "[Timers]", "Haste Time", "[Debuffs]" },
                Names(m));

            m.Toggle(_r.FindCategory("Player/Status/Debuffs"));
            CollectionAssert.AreEqual(new[] { "Hp", "[Buffs]", "[Debuffs]", "Poison" }, Names(m));

            m.Toggle(_r.FindCategory("Player/Status/Debuffs"));
            CollectionAssert.AreEqual(new[] { "Hp", "[Buffs]", "[Debuffs]" }, Names(m));
        }

        [Test]
        public void ClosingNestedKeepsParentOpen()
        {
            var m = new OptionListModel(_r, "Player/Status", false);
            m.Toggle(_r.FindCategory("Player/Status/Buffs/Timers"));
            m.Toggle(_r.FindCategory("Player/Status/Buffs/Timers"));
            CollectionAssert.AreEqual(new[] { "Hp", "[Buffs]", "Haste", "[Timers]", "[Debuffs]" }, Names(m));
        }

        [Test]
        public void GlobalSearch_ExpandsOnlyMatchingBranches()
        {
            Add("World/Time", "Pause");
            var m = new OptionListModel(_r, "", false);
            m.SetFilter(_r.CreateFilter("timers"));
            CollectionAssert.AreEqual(new[] { "[Player]", "[Status]", "[Buffs]", "[Timers]", "Haste Time" }, Names(m));

            m.SetFilter(OptionTreeFilter.None);
            CollectionAssert.AreEqual(new[] { "[Player]", "[World]" }, Names(m));
        }

        [Test]
        public void MissingRoot_YieldsNoRows()
        {
            var m = new OptionListModel(_r, "Nope", false);
            m.Refresh();
            Assert.IsEmpty(m.Rows);
        }
    }
}

namespace Atsuvanced.Debugger.Tests
{
    public class BuiltInTabsTests
    {
        [Test]
        public void BuiltInTabOrder_WithSearchBeforeOptionCategories()
        {
            var service = TestDebugger.Create(new MemoryDebuggerStorage());
            service.AddOption(OptionDefinition.FromMethod("Heal", () => { }, "Player"));
            service.AddOption(OptionDefinition.FromMethod("Pause", () => { }, "World/Time"));

            CollectionAssert.AreEqual(
                new[]
                {
                    "system", "search", "pinned", "option:Player", "option:World", "console", "profiler", "bug-reporter"
                },
                service.Tabs.Tabs.Select(t => t.Id));
            service.Dispose();
        }

        [Test]
        public void SearchTabSearchesEveryOption()
        {
            var service = TestDebugger.Create(new MemoryDebuggerStorage());
            service.AddOption(OptionDefinition.FromMethod("Heal", () => { }, "Player"));
            service.AddOption(OptionDefinition.FromMethod("Heal World", () => { }, "World/Time"));
            service.ShowDebugPanel(DebugTabIds.Search);

            var root = (SectionedPage)service.Pages.CurrentPage;
            var page = (OptionSearchPage)root.CurrentSectionPage;
            page.View.SetFilter(service.Options.CreateFilter("heal"));

            var hits = page.View.Model.Rows.Count(r => r.Kind == OptionListModel.RowKind.Entry);
            Assert.AreEqual(2, hits, "Hits from different top-level categories are shown together.");
            service.Dispose();
        }
    }
}