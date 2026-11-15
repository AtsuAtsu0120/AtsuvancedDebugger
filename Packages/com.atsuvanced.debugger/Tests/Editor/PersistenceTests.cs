using System.Linq;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    public class PersistenceTests
    {
        private MemoryDebuggerStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _storage = new MemoryDebuggerStorage();
        }

        private static OptionDefinition Method(string category, string name)
        {
            return OptionDefinition.FromMethod(name, () => { }, category);
        }

        private (OptionRegistry registry, PinPersistence pins) NewSession()
        {
            var r = new OptionRegistry();
            return (r, new PinPersistence(r, new DebuggerState(_storage)));
        }

        // ---- DebuggerState ----------------------------------------------------------------

        [Test]
        public void State_PrefixesKeys_AndEmptyDeletes()
        {
            var state = new DebuggerState(_storage);
            state.Set("tab", "console");
            Assert.AreEqual("console", _storage.GetString(DebuggerState.KeyPrefix + "tab"));
            Assert.AreEqual("console", new DebuggerState(_storage).Get("tab"));

            state.Set("tab", "");
            Assert.IsNull(_storage.GetString(DebuggerState.KeyPrefix + "tab"));
            Assert.IsNull(state.Get("tab"));
        }

        // ---- Pins ---------------------------------------------------------------------------

        [Test]
        public void Pins_AreRestoredById_WhenOptionsAppearLater()
        {
            var (r1, p1) = NewSession();
            var hp = Method("Player", "Hp");
            r1.AddOption(hp);
            r1.AddOption(Method("Player", "Mp"));
            r1.Pin(hp);
            p1.Dispose();

            var (r2, _) = NewSession();
            var hp2 = Method("Player", "Hp");
            var mp2 = Method("Player", "Mp");
            r2.AddOption(mp2);
            r2.AddOption(hp2);
            Assert.IsTrue(r2.IsPinned(hp2));
            Assert.IsFalse(r2.IsPinned(mp2));
        }

        [Test]
        public void Pins_SurviveUnregistration_ButNotExplicitUnpin()
        {
            var (r, pins) = NewSession();
            var goblinHp = Method("Enemies/Goblin", "Hp");
            r.AddOption(goblinHp);
            r.Pin(goblinHp);

            r.RemoveOption(goblinHp); // e.g. the enemy despawned
            Assert.IsFalse(r.IsPinned(goblinHp));
            CollectionAssert.Contains(pins.PinnedIds.ToList(), "Enemies/Goblin/Hp");

            var respawned = Method("Enemies/Goblin", "Hp");
            r.AddOption(respawned);
            Assert.IsTrue(r.IsPinned(respawned), "Pinned again when it comes back.");

            r.Unpin(respawned);
            CollectionAssert.IsEmpty(pins.PinnedIds);
            Assert.IsNull(_storage.GetString(DebuggerState.KeyPrefix + PinPersistence.StateKey));
        }

        [Test]
        public void ClearPinned_ForgetsUnregisteredPinsToo()
        {
            var (r, pins) = NewSession();
            var a = Method("A", "X");
            var b = Method("B", "Y");
            r.AddOption(a);
            r.AddOption(b);
            r.Pin(a);
            r.Pin(b);
            r.RemoveOption(b);

            r.ClearPinned();
            CollectionAssert.IsEmpty(pins.PinnedIds);
        }

        [Test]
        public void RestoredPin_PinsTheWholeGroup()
        {
            var (r1, p1) = NewSession();
            r1.AddContainer(new SpawnOptions());
            r1.Pin(r1.Options.First(o => o.Name == "Go"));
            p1.Dispose();

            var (r2, _) = NewSession();
            r2.AddContainer(new SpawnOptions());
            Assert.IsTrue(r2.IsPinned(r2.Options.First(o => o.Name == "X")));
            Assert.IsTrue(r2.IsPinned(r2.Options.First(o => o.Name == "Go")));
            Assert.IsFalse(r2.IsPinned(r2.Options.First(o => o.Name == "Other")));
        }

        // ---- Navigation state -----------------------------------------------------------------

        private DebugService NewService()
        {
            var s = TestDebugger.Create(_storage);
            s.AddOption(Method("Player/Status", "Hp"));
            s.AddOption(Method("Player/Wallet", "Gold"));
            s.AddOption(Method("Player/Status/Buffs/Fire", "Burn"));
            return s;
        }

        [Test]
        public void TabAndSection_AreRestored()
        {
            var s1 = NewService();
            s1.ShowDebugPanel("option:Player");
            ((SectionedPage)s1.Pages.CurrentPage).Select("opt:Wallet");
            s1.Dispose();

            var s2 = NewService();
            s2.ShowDebugPanel();
            Assert.AreEqual("option:Player", s2.Pages.CurrentTabId);
            Assert.AreEqual("opt:Wallet", ((SectionedPage)s2.Pages.CurrentPage).SelectedSectionId);
            s2.Dispose();
        }

        [Test]
        public void MissingStoredTab_FallsBackToDefaultTab()
        {
            new DebuggerState(_storage).Set(DebugPanel.TabStateKey, "option:Gone");
            var s = NewService();
            s.ShowDebugPanel();
            Assert.AreEqual(DebugTabIds.SystemInformation, s.Pages.CurrentTabId);
            s.Dispose();
        }

        [Test]
        public void OpenCategory_IsRestored()
        {
            var s1 = NewService();
            s1.ShowDebugPanel("option:Player");
            var page = (OptionSectionPage)((SectionedPage)s1.Pages.CurrentPage).CurrentSectionPage;
            var buffs = s1.Options.FindCategory("Player/Status/Buffs");
            page.View.Model.Toggle(buffs);
            Assert.AreEqual("Player/Status/Buffs", page.View.Model.OpenPath);
            s1.Dispose();

            var s2 = NewService();
            s2.ShowDebugPanel("option:Player");
            var page2 = (OptionSectionPage)((SectionedPage)s2.Pages.CurrentPage).CurrentSectionPage;
            Assert.AreEqual("Player/Status/Buffs", page2.View.Model.OpenPath);
            s2.Dispose();
        }

        [Test]
        public void SearchQuery_IsRestored()
        {
            new DebuggerState(_storage).Set(OptionSearchPage.StateKey, "gold");
            var s = NewService();
            s.ShowDebugPanel(DebugTabIds.Search);
            var page = (OptionSearchPage)((SectionedPage)s.Pages.CurrentPage).CurrentSectionPage;
            Assert.AreEqual("gold", page.SearchField.value);

            s.Navigator.Update(1f); // applies the pending query
            var hits = page.View.Model.Rows.Count(r => r.Kind == OptionListModel.RowKind.Entry);
            Assert.AreEqual(1, hits);
            s.Dispose();
        }

        [Test]
        public void PersistStateOff_UsesMemoryOnly()
        {
            var s = TestDebugger.Create(_storage, settings => settings.General.PersistState = false);
            s.AddOption(Method("A", "X"));
            s.Options.Pin(s.Options.Options[0]);
            s.ShowDebugPanel(DebugTabIds.Console);
            s.Dispose();
            Assert.IsNull(_storage.GetString(DebuggerState.KeyPrefix + PinPersistence.StateKey));
            Assert.IsNull(_storage.GetString(DebuggerState.KeyPrefix + DebugPanel.TabStateKey));
        }

        internal class SpawnOptions
        {
            [OptionCategory("Spawn")]
            [OptionGroup("pos")]
            public int X { get; set; }

            [OptionCategory("Spawn")]
            [OptionGroup("pos")]
            public void Go()
            {
            }

            [OptionCategory("Spawn")]
            public void Other()
            {
            }
        }
    }

    public class PinnedOptionsTests
    {
        [Test]
        public void Model_ListsPinnedEntriesInTreeOrder_GroupsOnce()
        {
            var r = new OptionRegistry();
            var b = OptionDefinition.FromMethod("B", () => { }, "Z");
            var a = OptionDefinition.FromMethod("A", () => { }, "Spawn/Deep");
            r.AddOption(b);
            r.AddContainer(new PersistenceTests.SpawnOptions());
            r.AddOption(a);
            r.Pin(b);
            r.Pin(a);
            r.Pin(r.Options.First(o => o.Name == "Go"));

            var model = new PinnedOptionsModel(r);
            model.Refresh();
            // Root children in registration order (Z, Spawn); a group counts once.
            CollectionAssert.AreEqual(new[] { "B", "Group", "A" },
                model.Entries.Select(e => e.IsGroup ? "Group" : e.Option.Name));
        }

        [Test]
        public void View_FollowsPinChanges_AndReusesRows()
        {
            var r = new OptionRegistry();
            var a = OptionDefinition.FromMethod("A", () => { }, "X");
            var b = OptionDefinition.FromMethod("B", () => { }, "X");
            r.AddOption(a);
            r.AddOption(b);
            var view = new PinnedOptionsView(r, new OptionViewRegistry(r), new PinnedSettings());
            Assert.IsFalse(view.HasEntries);

            r.Pin(a);
            Assert.IsTrue(view.HasEntries);
            var rowA = view.Q<OptionRow>();
            Assert.AreEqual(OptionPlacement.Pinned, rowA.Context.Placement);
            Assert.IsNull(rowA.Q(className: "atsu-pin"), "No pin button in the overlay.");

            r.Pin(b);
            view.Tick(0f, true);
            Assert.AreEqual(2, view.EntryCount);
            Assert.AreSame(rowA, view.Q<OptionRow>(), "Existing rows are reused.");

            r.RemoveOption(a);
            view.Tick(0f, true);
            Assert.AreEqual(1, view.EntryCount);
            view.Dispose();
        }
    }

    public class PinnedTabTests
    {
        [Test]
        public void PinnedFilter_ShowsOnlyPinnedBranches_WithoutHighlight()
        {
            var r = new OptionRegistry();
            var hp = OptionDefinition.FromMethod("Hp", () => { }, "Player/Status");
            var gold = OptionDefinition.FromMethod("Gold", () => { }, "Player/Wallet");
            r.AddOption(hp);
            r.AddOption(gold);
            r.Pin(hp);

            var f = OptionTreeFilter.CreatePinned(r);
            Assert.IsTrue(f.IsActive);
            Assert.IsFalse(f.HighlightHits);
            Assert.IsTrue(f.IsVisible(hp));
            Assert.IsFalse(f.IsVisible(gold));
            Assert.IsTrue(f.IsVisible(r.FindCategory("Player/Status")));
            Assert.IsFalse(f.IsVisible(r.FindCategory("Player/Wallet")));
            Assert.IsFalse(OptionTreeFilter.CreatePinned(new OptionRegistry()).IsVisible(hp),
                "Nothing pinned = nothing shown.");
        }

        [Test]
        public void PinnedTab_FollowsPins_AndUnpinAllNeedsTwoPresses()
        {
            var s = TestDebugger.Create(new MemoryDebuggerStorage());
            s.AddOption(OptionDefinition.FromMethod("Hp", () => { }, "Player/Status"));
            s.AddOption(OptionDefinition.FromMethod("Gold", () => { }, "Player/Wallet"));
            s.Options.Pin(s.Options.Options[0]);
            s.ShowDebugPanel(DebugTabIds.Pinned);

            var page = (PinnedOptionsPage)((SectionedPage)s.Pages.CurrentPage).CurrentSectionPage;

            int Entries()
            {
                return page.View.Model.Rows.Count(row => row.Kind == OptionListModel.RowKind.Entry);
            }

            Assert.AreEqual(1, Entries());

            s.Options.Pin(s.Options.Options[1]);
            s.Navigator.Update(0.1f);
            Assert.AreEqual(2, Entries());

            Assert.AreEqual(DebuggerStrings.Get("pinned.unpinAll"), ((Button)page.Toolbar).text);
            page.PressUnpinAll();
            Assert.IsTrue(page.IsConfirmingUnpinAll);
            Assert.AreEqual(DebuggerStrings.Format("pinned.unpinConfirm", 2), ((Button)page.Toolbar).text);
            Assert.AreEqual(2, s.Options.PinnedOptions.Count, "First press only asks for confirmation.");
            page.PressUnpinAll();
            Assert.AreEqual(0, s.Options.PinnedOptions.Count);

            s.Navigator.Update(0.1f);
            Assert.AreEqual(2, Entries(), "Unpinned rows stay until the panel is closed.");

            // Switching tabs keeps them, too.
            s.Pages.SelectTab(DebugTabIds.Console);
            s.Pages.SelectTab(DebugTabIds.Pinned);
            Assert.AreEqual(2, Entries());

            // Re-pinning from the list works while it is still shown.
            s.Options.Pin(s.Options.Options[1]);
            s.Navigator.Update(0.1f);
            Assert.AreEqual(2, Entries());

            s.HideDebugPanel();
            s.ShowDebugPanel(DebugTabIds.Pinned);
            Assert.AreEqual(1, Entries(), "Closing the panel drops the unpinned rows.");

            // An unpinned option that is then unregistered disappears from the list.
            s.Options.Unpin(s.Options.Options[1]);
            s.RemoveOption(s.Options.Options[1]);
            s.Navigator.Update(0.1f);
            Assert.AreEqual(0, Entries());
            s.Dispose();
        }
    }

    public class PinnedMinimizeTests
    {
        [Test]
        public void Minimize_HidesList_AndIsRemembered()
        {
            var storage = new MemoryDebuggerStorage();
            var r = new OptionRegistry();
            var o = OptionDefinition.FromMethod("A", () => { }, "X");
            r.AddOption(o);
            r.Pin(o);

            var view = new PinnedOptionsView(r, new OptionViewRegistry(r), new PinnedSettings(),
                new DebuggerState(storage));
            Assert.IsFalse(view.IsMinimized);
            view.ToggleMinimized();
            Assert.IsTrue(view.IsMinimized);
            Assert.IsTrue(view.Q<ScrollView>().ClassListContains(UssClasses.Hidden));
            Assert.IsTrue(view.ClassListContains("atsu-pinned--minimized"));
            view.Dispose();

            var restored = new PinnedOptionsView(r, new OptionViewRegistry(r), new PinnedSettings(),
                new DebuggerState(storage));
            Assert.IsTrue(restored.IsMinimized, "Minimized state survives a restart.");
            restored.ToggleMinimized();
            Assert.IsNull(storage.GetString(DebuggerState.KeyPrefix + PinnedOptionsView.MinimizedStateKey));
            restored.Dispose();
        }
    }

    public class BuiltInPagesTests
    {
        [Test]
        public void SubclassReplacesBuiltInSection_AndQueryWinsOverSavedSearch()
        {
            var storage = new MemoryDebuggerStorage();
            new DebuggerState(storage).Set(OptionSearchPage.StateKey, "old");
            var s = TestDebugger.Create(storage);
            s.AddOption(OptionDefinition.FromMethod("Gold", () => { }, "Player"));
            var mine = new MySearchPage { Query = "gold" };
            s.Tabs.Find(DebugTabIds.Search).Sections.Add(new DebugSection("options", "Options", () => mine));

            s.ShowDebugPanel(DebugTabIds.Search);
            Assert.AreSame(mine, ((SectionedPage)s.Pages.CurrentPage).CurrentSectionPage);
            Assert.AreEqual("gold", mine.Query);
            s.Dispose();
        }

        private sealed class MySearchPage : OptionSearchPage
        {
        }
    }

    public class DestroyedContainerTests
    {
        [Test]
        public void DestroyedUnityObjectContainers_AreRemoved()
        {
            var r = new OptionRegistry();
            var so = ScriptableObject.CreateInstance<SoContainer>();
            r.AddContainer(so);
            Assert.IsNotNull(r.FindCategory("SO"));
            Assert.AreEqual(0, r.RemoveDestroyedContainers());

            Object.DestroyImmediate(so);
            Assert.AreEqual(1, r.RemoveDestroyedContainers());
            Assert.IsNull(r.FindCategory("SO"));
            Assert.IsFalse(r.ContainsContainer(so));
        }

        private sealed class SoContainer : ScriptableObject
        {
            [OptionCategory("SO")]
            public void Act()
            {
            }
        }
    }
}