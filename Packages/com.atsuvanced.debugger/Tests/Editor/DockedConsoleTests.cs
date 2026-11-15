using System.Collections.Generic;
using System.Linq;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    public class DockedConsoleTests
    {
        [Test]
        public void CopyLast_ReturnsNewestOldestFirst_AcrossWrap()
        {
            var b = new ConsoleLogBuffer(4);
            for (var i = 0; i < 6; i++)
            {
                b.Add(LogType.Log, "m" + i, null);
            }

            var list = new List<LogEntry>();

            b.CopyLast(3, list);
            CollectionAssert.AreEqual(new[] { "m3", "m4", "m5" }, list.Select(e => e.Message));
            b.CopyLast(10, list);
            CollectionAssert.AreEqual(new[] { "m2", "m3", "m4", "m5" }, list.Select(e => e.Message));
            b.CopyLast(0, list);
            Assert.AreEqual(0, list.Count);
        }

        [Test]
        public void State_UsesSettingsFirst_ThenPersistedValues()
        {
            var storage = new MemoryDebuggerStorage();
            var settings = new DockedConsoleSettings { VisibleAtStart = false, Alignment = ConsoleAlignment.Bottom };
            var s1 = new DockedConsoleState(new DebuggerState(storage), settings);
            Assert.IsFalse(s1.IsVisible);
            Assert.AreEqual(ConsoleAlignment.Bottom, s1.Alignment);

            var changes = 0;
            s1.Changed += () => changes++;
            s1.IsVisible = true;
            s1.IsExpanded = false;
            s1.Alignment = ConsoleAlignment.Top;
            s1.Alignment = ConsoleAlignment.Top; // no change
            Assert.AreEqual(3, changes);

            var s2 = new DockedConsoleState(new DebuggerState(storage), settings);
            Assert.IsTrue(s2.IsVisible);
            Assert.IsFalse(s2.IsExpanded);
            Assert.AreEqual(ConsoleAlignment.Top, s2.Alignment);
        }

        [Test]
        public void Model_TracksLatestLinesAndCounts_OnlyWhenChanged()
        {
            var b = new ConsoleLogBuffer(100);
            var m = new DockedConsoleModel(b);
            Assert.IsTrue(m.Update(3));
            Assert.IsNull(m.Latest);
            Assert.IsFalse(m.Update(3), "No change, no snapshot.");

            b.Add(LogType.Log, "a", null);
            b.Add(LogType.Warning, "b", null);
            b.Add(LogType.Error, "c", null);
            b.Add(LogType.Exception, "d", null);
            Assert.IsTrue(m.Update(3));
            CollectionAssert.AreEqual(new[] { "b", "c", "d" }, m.Lines.Select(e => e.Message));
            Assert.AreEqual("d", m.Latest.Value.Message);
            Assert.AreEqual(1, m.InfoCount);
            Assert.AreEqual(1, m.WarningCount);
            Assert.AreEqual(2, m.ErrorCount);
            Assert.IsTrue(m.Update(2), "Changing the line count takes a new snapshot.");
            Assert.AreEqual(2, m.Lines.Count);
        }

        private static List<string> LineTexts(DockedConsoleView v)
        {
            return v.Query<Label>(className: "atsu-dock__line-text").ToList().Select(l => l.text).ToList();
        }

        [Test]
        public void View_PutsNewestLineNextToTheBar()
        {
            var b = new ConsoleLogBuffer(100);
            b.Add(LogType.Log, "old", null);
            b.Add(LogType.Log, "new\nsecond line", null);
            var settings = new DockedConsoleSettings { Lines = 3 };
            var state = new DockedConsoleState(null, settings) { IsVisible = true, IsExpanded = true };

            var v = new DockedConsoleView(b, state, settings, null);
            v.Tick(true);
            var bottom = LineTexts(v);
            Assert.AreEqual(3, bottom.Count);
            Assert.AreEqual("", bottom[0]);
            StringAssert.EndsWith("old", bottom[1]);
            StringAssert.EndsWith("new", bottom[2], "Bottom dock: newest at the bottom, first line only.");
            Assert.Greater(v.IndexOf(v.Q(className: "atsu-dock__bar")), v.IndexOf(v.Q(className: "atsu-dock__lines")));

            state.Alignment = ConsoleAlignment.Top;
            v.Tick(true);
            var top = LineTexts(v);
            StringAssert.EndsWith("new", top[0], "Top dock: newest at the top.");
            Assert.Less(v.IndexOf(v.Q(className: "atsu-dock__bar")), v.IndexOf(v.Q(className: "atsu-dock__lines")));

            state.IsExpanded = false;
            v.Tick(true);
            Assert.IsNull(v.Q(className: "atsu-dock__lines"), "Collapsed: no line list.");
            Assert.AreEqual("new", v.Q<Label>(className: "atsu-dock__latest").text);
            v.Dispose();
        }

        [Test]
        public void Service_DockedConsoleState_SurvivesRestart()
        {
            var storage = new MemoryDebuggerStorage();
            var s1 = TestDebugger.Create(storage);
            Assert.IsFalse(s1.GetFeature<DockedConsoleFeature>().IsVisible, "Hidden by default.");
            s1.GetFeature<DockedConsoleFeature>().IsVisible = true;
            s1.Dispose();

            var s2 = TestDebugger.Create(storage);
            Assert.IsTrue(s2.GetFeature<DockedConsoleFeature>().IsVisible);
            s2.Dispose();
        }
    }
}