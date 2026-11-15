using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    public class EntryCodeTests
    {
        [Test]
        public void Gate_ValidatesAndUnlocksOncePerSession()
        {
            Assert.IsTrue(EntryCodeGate.IsValid("0420"));
            Assert.IsFalse(EntryCodeGate.IsValid("123"));
            Assert.IsFalse(EntryCodeGate.IsValid("12a4"));
            Assert.IsFalse(EntryCodeGate.IsValid(null));

            var gate = new EntryCodeGate(new EntryCodeSettings { Required = true, Code = "1234" });
            Assert.IsTrue(gate.IsRequired);
            Assert.IsFalse(gate.TrySubmit("0000"));
            Assert.AreEqual(1, gate.FailedAttempts);
            Assert.IsTrue(gate.TrySubmit("1234"));
            Assert.IsFalse(gate.IsRequired, "Not asked again in the session.");

            gate.EveryTime = true;
            Assert.IsTrue(gate.IsRequired);

            Assert.Throws<ArgumentException>(() => gate.SetCode("12"));
            gate.Disable();
            Assert.IsFalse(gate.IsRequired);

            var invalid = new EntryCodeGate(new EntryCodeSettings { Required = true, Code = "abc" });
            Assert.IsFalse(invalid.IsEnabled,
                "An invalid code in the settings disables the gate instead of locking the panel.");
        }

        [Test]
        public void Panel_OpensOnlyAfterTheRightCode_OnTheRequestedTab()
        {
            var s = TestDebugger.Create(new MemoryDebuggerStorage());
            s.Panel.SetEntryCode("4321");
            Assert.IsTrue(s.Panel.IsEntryCodeEnabled);

            s.ShowDebugPanel(DebugTabIds.Console);
            Assert.IsFalse(s.Panel.IsVisible);
            Assert.IsTrue(s.Panel.IsEntryCodeShown);
            Assert.IsFalse(s.PanelState.SubmitEntryCode("0000"));
            Assert.IsFalse(s.Panel.IsVisible);
            Assert.IsTrue(s.PanelState.SubmitEntryCode("4321"));
            Assert.IsTrue(s.Panel.IsVisible);
            Assert.IsFalse(s.Panel.IsEntryCodeShown);
            Assert.AreEqual(DebugTabIds.Console, s.Navigator.CurrentTabId);

            s.HideDebugPanel();
            s.ShowDebugPanel();
            Assert.IsTrue(s.Panel.IsVisible, "Unlocked for the rest of the session.");
            s.Dispose();
        }

        [Test]
        public void Cancel_And_BypassAndDisable()
        {
            var s = TestDebugger.Create(configure: b => b.UseEntryCode("1111"));
            s.ShowDebugPanel();
            s.PanelState.CancelEntryCode();
            Assert.IsFalse(s.Panel.IsEntryCodeShown);
            Assert.IsFalse(s.Panel.IsVisible);

            s.ShowDebugPanel(null, false);
            Assert.IsTrue(s.Panel.IsVisible, "requireEntryCode: false skips the PIN.");
            s.HideDebugPanel();

            s.ShowDebugPanel(DebugTabIds.Profiler);
            Assert.IsTrue(s.Panel.IsEntryCodeShown);
            s.Panel.DisableEntryCode();
            Assert.IsTrue(s.Panel.IsVisible, "Disabling the code opens the panel that was waiting.");
            Assert.AreEqual(DebugTabIds.Profiler, s.Navigator.CurrentTabId);
            s.Dispose();
        }

        [Test]
        public void EntryCodeView_SubmitsAfterFourDigits()
        {
            var submitted = new List<string>();
            var cancelled = 0;
            var view = new EntryCodeView(code =>
            {
                submitted.Add(code);
                return code == "2468";
            }, () => cancelled++);
            foreach (var c in "1357")
            {
                view.Type(c);
            }

            CollectionAssert.AreEqual(new[] { "1357" }, submitted);
            Assert.AreEqual("", view.Input, "Cleared after a wrong code.");
            view.Type('x');
            Assert.AreEqual("", view.Input);
            foreach (var c in "2468")
            {
                view.Type(c);
            }

            Assert.AreEqual("2468", submitted.Last());
            Assert.AreEqual(0, cancelled);
        }
    }

    public class CategorySortTests
    {
        [Test]
        public void SortPriority_OrdersCategoriesTabsAndSections()
        {
            var s = TestDebugger.Create(new MemoryDebuggerStorage());
            s.AddOption(OptionDefinition.FromMethod("x", () => { }, "Alpha"));
            s.AddOption(OptionDefinition.FromMethod("y", () => { }, "Beta"));
            CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, s.Options.Root.Children.Select(c => c.Name));

            s.AddOptionContainer(new ZetaOptions());
            CollectionAssert.AreEqual(new[] { "Zeta", "Alpha", "Beta" }, s.Options.Root.Children.Select(c => c.Name),
                "[OptionCategorySort(-10)] on the class puts its category first.");
            CollectionAssert.AreEqual(new[] { "Early", "Late" },
                s.Options.FindCategory("Zeta").Children.Select(c => c.Name));

            var optionTabs = s.Tabs.Tabs.Where(t => t.Id.StartsWith(DebugTabIds.OptionCategoryPrefix))
                .Select(t => t.Title).ToArray();
            CollectionAssert.AreEqual(new[] { "Zeta", "Alpha", "Beta" }, optionTabs);
            var zeta = s.Tabs.Find(DebugTabIds.OptionCategoryPrefix + "Zeta");
            CollectionAssert.AreEqual(new[] { "Early", "Late" }, zeta.Sections.Select(x => x.Title));

            // From code, also after the category exists.
            var reordered = 0;
            s.Options.CategoriesReordered += _ => reordered++;
            s.Options.SetCategorySortPriority("Beta", -20);
            Assert.AreEqual(1, reordered);
            CollectionAssert.AreEqual(new[] { "Beta", "Zeta", "Alpha" }, s.Options.Root.Children.Select(c => c.Name));
            optionTabs = s.Tabs.Tabs.Where(t => t.Id.StartsWith(DebugTabIds.OptionCategoryPrefix)).Select(t => t.Title)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "Beta", "Zeta", "Alpha" }, optionTabs);
            Assert.AreEqual(DebugTabIds.SystemInformation, s.Tabs.Tabs[0].Id, "Built-in tabs keep their places.");

            s.Options.SetCategorySortPriority("Zeta/Early", 10);
            CollectionAssert.AreEqual(new[] { "Late", "Early" }, zeta.Sections.Select(x => x.Title));

            // Applied when a category is created later.
            s.Options.SetCategorySortPriority("Gamma", -100);
            s.AddOption(OptionDefinition.FromMethod("z", () => { }, "Gamma"));
            Assert.AreEqual("Gamma", s.Options.Root.Children[0].Name);
            Assert.AreEqual(-100, s.Options.GetCategorySortPriority("Gamma"));
            s.Dispose();
        }

        [Test]
        public void Reorder_KeepsOtherItemsInPlace()
        {
            var tabs = new DebugTabRegistry();
            tabs.Add(new DebugTabDefinition("a", "A", 0));
            tabs.Add(new DebugTabDefinition("x", "X", 100));
            tabs.Add(new DebugTabDefinition("y", "Y", 100));
            tabs.Add(new DebugTabDefinition("b", "B", 200));
            var changed = 0;
            tabs.Changed += () => changed++;
            Assert.IsTrue(tabs.Reorder(new[] { "y", "x", "missing" }));
            CollectionAssert.AreEqual(new[] { "a", "y", "x", "b" }, tabs.Tabs.Select(t => t.Id));
            Assert.IsFalse(tabs.Reorder(new[] { "y", "x" }));
            Assert.AreEqual(1, changed);
        }

        [OptionCategory("Zeta")]
        [OptionCategorySort(-10)]
        [OptionCategorySort("Zeta/Late", 5)]
        private class ZetaOptions
        {
            [OptionCategory("Late")] public int A { get; set; }
            [OptionCategory("Early")] public int B { get; set; }
        }
    }

    public class LocalizationTests
    {
        [TearDown]
        public void TearDown()
        {
            DebuggerStrings.Language = DebuggerLanguage.Auto;
        }

        [Test]
        public void EveryKeyHasJapanese()
        {
            var missing = DebuggerStrings.Keys.Where(k => !DebuggerStrings.HasJapanese(k)).ToArray();
            CollectionAssert.IsEmpty(missing);
        }

        [Test]
        public void LanguageSelection_AndOverrides()
        {
            DebuggerStrings.Language = DebuggerLanguage.English;
            Assert.AreEqual("Console", DebuggerStrings.Get("tab.console"));
            Assert.AreEqual("Log 3", DebuggerStrings.Format("console.log", 3));
            DebuggerStrings.Language = DebuggerLanguage.Japanese;
            Assert.AreEqual("コンソール", DebuggerStrings.Get("tab.console"));
            Assert.AreEqual("unknown.key", DebuggerStrings.Get("unknown.key"));

            DebuggerStrings.Override(DebuggerLanguage.Japanese, "tab.console", "ログ");
            Assert.AreEqual("ログ", DebuggerStrings.Get("tab.console"));
            DebuggerStrings.Override(DebuggerLanguage.Japanese, "tab.console", null);
            Assert.AreEqual("コンソール", DebuggerStrings.Get("tab.console"));
            Assert.Throws<ArgumentException>(() => DebuggerStrings.Override(DebuggerLanguage.Auto, "x", "y"));
        }

        [Test]
        public void SettingsLanguage_AppliesToBuiltInTabs()
        {
            var s = TestDebugger.Create(configure: b => b.UseLanguage(DebuggerLanguage.English));
            Assert.AreEqual("Bug Report", s.Tabs.Find(DebugTabIds.BugReporter).Title);
            s.Dispose();
            s = TestDebugger.Create(configure: b => b.UseLanguage(DebuggerLanguage.Japanese));
            Assert.AreEqual("バグ報告", s.Tabs.Find(DebugTabIds.BugReporter).Title);
            s.Dispose();
        }
    }

    public class SavedStateTests
    {
        [Test]
        public void Clear_DeletesEveryWrittenKey()
        {
            var storage = new MemoryDebuggerStorage();
            var state = new DebuggerState(storage);
            state.Set(DebugPanel.TabStateKey, "console");
            state.Set("value/Player/Hp", "v:10");
            state.Set("value/Player/Hp", "v:11");
            state.Save();

            var reloaded = new DebuggerState(storage);
            CollectionAssert.AreEquivalent(new[] { DebugPanel.TabStateKey, "value/Player/Hp" }, reloaded.Keys);
            Assert.AreEqual(2, reloaded.Clear());
            Assert.IsNull(reloaded.Get(DebugPanel.TabStateKey));
            Assert.IsNull(storage.GetString(DebuggerState.KeyPrefix + "value/Player/Hp"));
            Assert.IsNull(storage.GetString(DebuggerState.KeyPrefix + DebuggerState.IndexKey));
            Assert.AreEqual(0, new DebuggerState(storage).Keys.Count);
        }
    }
}