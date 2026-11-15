using System;
using System.Linq;
using System.Text.RegularExpressions;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    public class BuilderTests
    {
        [Test]
        public void Default_InstallsEveryBuiltInFeature_InOrder()
        {
            var s = TestDebugger.Create();
            CollectionAssert.AreEqual(BuiltInFeatures.Create().Select(f => f.GetType()),
                s.Features.Select(f => f.GetType()));
            Assert.IsNotNull(s.GetFeature<ConsoleFeature>());
            Assert.IsNotNull(s.GetFeature<SinglePageTabFeature>(), "GetFeature also finds subclasses.");
            CollectionAssert.AreEqual(
                new[]
                {
                    DebugTabIds.SystemInformation, DebugTabIds.Search, DebugTabIds.Pinned, DebugTabIds.Console,
                    DebugTabIds.Profiler, DebugTabIds.BugReporter
                },
                s.Tabs.Tabs.Select(t => t.Id));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    DebugOverlayIds.Trigger, DebugOverlayIds.Pinned, DebugOverlayIds.DockedConsole,
                    DebugOverlayIds.DockedProfiler
                },
                s.Overlays.Overlays.Select(o => o.Id));
            s.Dispose();
            Assert.AreEqual(0, s.Overlays.Overlays.Count, "Uninstalled on dispose.");
        }

        [Test]
        public void MethodChain_RemovesConfiguresAndAddsFeatures()
        {
            var custom = new CountingFeature();
            var s = TestDebugger.Create(configure: b => b
                .Remove<ProfilerFeature>()
                .Remove<DockedProfilerFeature>()
                .Configure<ConsoleFeature>(f => f.Order = 10)
                .ConfigureSettings(x => x.Trigger.Enabled = false)
                .Add(custom));

            Assert.IsNull(s.Tabs.Find(DebugTabIds.Profiler));
            Assert.IsNull(s.Overlays.Find(DebugOverlayIds.DockedProfiler));
            Assert.AreEqual(DebugTabIds.Console, s.Tabs.Tabs[1].Id,
                "Order 10 puts the console right after System (0).");
            Assert.IsFalse(s.GetFeature<TriggerFeature>().IsEnabled);
            Assert.AreSame(s, custom.Debugger);
            Assert.AreEqual(1, custom.Installs);

            s.Tick(0.1f);
            s.Tick(0.1f);
            Assert.AreEqual(2, custom.Updates);
            s.Dispose();
            Assert.AreEqual(1, custom.Uninstalls);
            Assert.IsFalse(custom.IsInstalled);
        }

        [Test]
        public void Configure_MissingFeature_Throws_AndAddReplacesSameType()
        {
            var b = DebuggerBuilder.CreateDefault();
            b.Remove<ConsoleFeature>();
            Assert.Throws<InvalidOperationException>(() => b.Configure<ConsoleFeature>(_ => { }));
            Assert.IsFalse(b.Has<ConsoleFeature>());

            var index = b.Features.ToList().FindIndex(f => f is OptionsFeature);
            var replacement = new OptionsFeature { Layout = OptionsLayout.SingleTab };
            b.Add(replacement);
            Assert.AreSame(replacement, b.Features[index], "Replaced in place.");
            Assert.AreEqual(1, b.Features.Count(f => f is OptionsFeature));

            Assert.Throws<ArgumentException>(() => b.UseEntryCode("12"));
            Assert.AreEqual(0, new DebuggerBuilder().Features.Count, "A plain builder starts empty.");
        }

        [Test]
        public void EmptyBuilder_HasNoTabsOrOverlays()
        {
            var s = TestDebugger.Create(configure: b => b.ClearFeatures());
            Assert.AreEqual(0, s.Tabs.Tabs.Count);
            Assert.AreEqual(0, s.Overlays.Overlays.Count);
            s.AddOption(OptionDefinition.FromMethod("A", () => { }, "X"));
            Assert.AreEqual(0, s.Tabs.Tabs.Count, "No option tabs without OptionsFeature.");
            s.Dispose();
        }

        [Test]
        public void OptionViews_FromTheBuilder_ArePerService()
        {
            var a = TestDebugger.Create(configure: b => b.AddOptionView<Vector2Int>(() => new DummyView()));
            var b2 = TestDebugger.Create();
            Assert.IsTrue(a.OptionViews.HasTypeView(typeof(Vector2Int)));
            Assert.IsFalse(b2.OptionViews.HasTypeView(typeof(Vector2Int)),
                "Registrations do not leak to other debuggers.");

            a.AddOptionContainer(new WithVector());
            b2.AddOptionContainer(new WithVector());
            Assert.AreEqual(1, a.Options.Options.Count);
            Assert.AreEqual(0, b2.Options.Options.Count);
            a.Dispose();
            b2.Dispose();
        }

        [Test]
        public void SettingsClone_IsDeep()
        {
            var s = new DebuggerSettings();
            var c = s.Clone();
            c.Pinned.Width = 500;
            c.Keyboard.Toggle = new KeyboardShortcut { Key = KeyCode.F2 };
            Assert.AreEqual(320f, s.Pinned.Width);
            Assert.AreEqual(KeyCode.F1, s.Keyboard.Toggle.Key);
        }

        private sealed class CountingFeature : DebuggerFeature
        {
            public int Installs, Uninstalls, Updates;

            protected override void OnInstall()
            {
                Installs++;
            }

            protected override void OnUninstall()
            {
                Uninstalls++;
            }

            protected override void OnUpdate(float unscaledDeltaTime)
            {
                Updates++;
            }
        }

        private sealed class DummyView : OptionView
        {
            public override VisualElement CreateElement(OptionContext context)
            {
                return new Label();
            }
        }

        private sealed class WithVector
        {
            public Vector2Int Cell { get; set; }
        }
    }

    public class OptionsLayoutTests
    {
        [Test]
        public void SingleTab_PutsTopLevelCategoriesInOneTab_AndCustomPagesAreUsed()
        {
            var created = 0;
            var s = TestDebugger.Create(configure: b => b.Configure<OptionsFeature>(f =>
            {
                f.Layout = OptionsLayout.SingleTab;
                f.SectionPageFactory = (path, direct, title) =>
                {
                    created++;
                    return new OptionSectionPage(path, direct, title);
                };
            }));
            Assert.IsNull(s.Tabs.Find(DebugTabIds.Options), "No tab while there are no options.");

            s.AddOption(OptionDefinition.FromMethod("a", () => { }, "Player/Status"));
            s.AddOption(OptionDefinition.FromMethod("b", () => { }, "World"));
            var tab = s.Tabs.Find(DebugTabIds.Options);
            Assert.IsNotNull(tab);
            Assert.IsFalse(s.Tabs.Tabs.Any(t => t.Id.StartsWith(DebugTabIds.OptionCategoryPrefix)));
            CollectionAssert.AreEqual(new[] { "Player", "World" }, tab.Sections.Select(x => x.Title));

            s.ShowDebugPanel(DebugTabIds.Options);
            Assert.AreEqual(1, created);
            var page = (OptionSectionPage)s.Pages.ActivePage;
            Assert.AreEqual("Player", page.CategoryPath);
            Assert.IsFalse(page.IsGeneral);

            s.RemoveOptionCategory("World");
            CollectionAssert.AreEqual(new[] { "Player" }, tab.Sections.Select(x => x.Title));
            s.Dispose();
        }

        [Test]
        public void SearchAndPinnedTabs_CanBeTurnedOff()
        {
            var s = TestDebugger.Create(configure: b =>
                b.Configure<OptionsFeature>(f => f.SearchTab = f.PinnedTab = false));
            Assert.IsNull(s.Tabs.Find(DebugTabIds.Search));
            Assert.IsNull(s.Tabs.Find(DebugTabIds.Pinned));
            s.Dispose();
        }
    }

    public class OverlayTests
    {
        [Test]
        public void Overlays_ShowWhileActive_AndHideWhileThePanelIsOpen()
        {
            var s = TestDebugger.Create(configure: b => b.ClearFeatures());
            var overlay = new TestOverlay("hud", OverlayAnchor.TopLeft);
            var always = new TestOverlay("always", OverlayAnchor.TopLeft, OverlayVisibility.Always);
            s.Overlays.Add(overlay);
            s.Overlays.Add(always);
            Assert.AreSame(s, overlay.Debugger);

            s.Tick(0f);
            Assert.IsTrue(overlay.IsShown);
            Assert.AreEqual(1, overlay.Shows);
            Assert.AreEqual(1, overlay.Updates);

            overlay.Active = false;
            s.Tick(0f);
            Assert.IsFalse(overlay.IsShown);
            overlay.Active = true;

            s.Tabs.Add(new DebugTabDefinition("t", "T", () => DebugPage.FromElement("P", () => new VisualElement())));
            s.ShowDebugPanel();
            s.Tick(0f);
            Assert.IsFalse(overlay.IsShown, "Hidden while the panel is open.");
            Assert.IsTrue(always.IsShown, "OverlayVisibility.Always stays.");

            s.HideDebugPanel();
            s.Panel.SetEntryCode("1234");
            s.ShowDebugPanel();
            s.Tick(0f);
            Assert.IsFalse(always.IsShown, "Nothing is shown while the PIN pad is up.");
            s.PanelState.CancelEntryCode();

            Assert.IsTrue(s.Overlays.Remove("hud"));
            Assert.IsTrue(overlay.IsDisposed);
            Assert.AreEqual(1, overlay.Disposes);
            s.Dispose();
            Assert.IsTrue(always.IsDisposed);
        }

        [Test]
        public void Layer_StacksByOrder_AndMovesWhenTheAnchorChanges()
        {
            var registry = new DebugOverlayRegistry(null);
            var layer = new OverlayLayer(registry);
            var second = new TestOverlay("second", OverlayAnchor.BottomRight, order: 10);
            var first = new TestOverlay("first", OverlayAnchor.BottomRight, order: -5);
            registry.Add(second);
            registry.Add(first);
            registry.Update(false, false, 0f);

            var stack = layer.GetContainer(OverlayAnchor.BottomRight);
            Assert.AreEqual(2, stack.childCount);
            Assert.AreSame(first.Content, stack[0], "Lower order is closer to the corner.");
            Assert.IsTrue(stack.ClassListContains("atsu-overlays__stack--bottom-right"));

            second.MoveTo(OverlayAnchor.TopEdge);
            Assert.AreSame(second.Content, layer.GetContainer(OverlayAnchor.TopEdge)[0]);
            Assert.AreEqual(1, stack.childCount);

            first.Active = false;
            registry.Update(false, false, 0f);
            Assert.IsTrue(first.Content.ClassListContains(UssClasses.Hidden));
            layer.Dispose();
        }

        [Test]
        public void FailingOverlay_ShowsAnErrorInsteadOfThrowing()
        {
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            var registry = new DebugOverlayRegistry(null);
            var broken = new TestOverlay("broken", OverlayAnchor.TopLeft) { Throw = true };
            registry.Add(broken);
            registry.Update(false, false, 0f);
            Assert.IsTrue(broken.IsShown);
            Assert.IsNotNull(broken.Content.Q(className: "atsu-error-box"));
        }

        [Test]
        public void BuiltInOverlays_FollowTheirFeatures()
        {
            var s = TestDebugger.Create(settings: x => x.Pinned.Corner = OverlayCorner.TopRight);
            s.Tick(0f);
            Assert.IsTrue(s.Overlays.Find(DebugOverlayIds.Trigger).IsShown, "Trigger on by default.");
            Assert.IsFalse(s.Overlays.Find(DebugOverlayIds.Pinned).IsShown, "Nothing pinned.");
            Assert.AreEqual(OverlayAnchor.TopRight, s.Overlays.Find(DebugOverlayIds.Pinned).Anchor);

            s.AddOption(OptionDefinition.FromMethod("A", () => { }, "X"));
            s.PinOption("A");
            var dock = s.GetFeature<DockedConsoleFeature>();
            dock.IsVisible = true;
            dock.Alignment = ConsoleAlignment.Top;
            s.GetFeature<TriggerFeature>().IsEnabled = false;
            s.Tick(0f);
            Assert.IsTrue(s.Overlays.Find(DebugOverlayIds.Pinned).IsShown);
            Assert.IsTrue(s.Overlays.Find(DebugOverlayIds.DockedConsole).IsShown);
            Assert.AreEqual(OverlayAnchor.TopEdge, s.Overlays.Find(DebugOverlayIds.DockedConsole).Anchor);
            Assert.IsFalse(s.Overlays.Find(DebugOverlayIds.Trigger).IsShown);

            s.GetFeature<DockedProfilerFeature>().IsDocked = true;
            s.Tick(0f);
            Assert.IsTrue(s.Profiler.IsRecording, "The docked profiler records while shown.");
            s.GetFeature<DockedProfilerFeature>().IsDocked = false;
            s.Tick(0f);
            Assert.IsFalse(s.Profiler.IsRecording);
            s.Dispose();
        }

        private sealed class TestOverlay : DebugOverlay
        {
            public bool Active = true;
            public int Shows, Updates, Disposes;
            public bool Throw;

            public TestOverlay(string id, OverlayAnchor anchor,
                OverlayVisibility visibility = OverlayVisibility.WhilePanelClosed,
                int order = 0) : base(id, anchor, order)
            {
                Visibility = visibility;
            }

            public override OverlayVisibility Visibility { get; }

            public override bool IsActive => Active;

            public void MoveTo(OverlayAnchor anchor)
            {
                Anchor = anchor;
            }

            protected override VisualElement CreateContent()
            {
                if (Throw)
                {
                    throw new InvalidOperationException("boom");
                }

                return new Label(Id);
            }

            protected override void OnShow()
            {
                Shows++;
            }

            protected override void OnUpdate(float unscaledDeltaTime)
            {
                Updates++;
            }

            protected override void OnDispose()
            {
                Disposes++;
            }
        }
    }

    public class PauseTimeTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        [Test]
        public void PausesOnlyWhenTheSettingIsOn_AndRestores()
        {
            Time.timeScale = 0.5f;
            var s = TestDebugger.Create(settings: x => x.General.PauseTimeWhenOpen = true);
            s.ShowDebugPanel();
            Assert.AreEqual(0f, Time.timeScale);
            s.HideDebugPanel();
            Assert.AreEqual(0.5f, Time.timeScale);
            s.Dispose();

            var off = TestDebugger.Create();
            off.ShowDebugPanel();
            Assert.AreEqual(0.5f, Time.timeScale);
            off.Dispose();
        }
    }
}