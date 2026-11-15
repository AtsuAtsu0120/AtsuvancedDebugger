using System;
using System.Linq;
using System.Text.RegularExpressions;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Atsuvanced.Debugger.Tests
{
    public class FrameHistoryTests
    {
        [Test]
        public void RingBuffer_KeepsNewest_AndComputesStats()
        {
            var h = new FrameHistory(3);
            Assert.AreEqual(0, h.Count);
            Assert.AreEqual(0f, h.Latest);

            h.Add(10);
            h.Add(20);
            h.Add(30);
            h.Add(40);
            Assert.AreEqual(3, h.Count);
            CollectionAssert.AreEqual(new[] { 20f, 30f, 40f }, Enumerable.Range(0, h.Count).Select(i => h[i]));
            Assert.AreEqual(40f, h.Latest);

            h.GetStats(out var min, out var avg, out var max);
            Assert.AreEqual(20f, min);
            Assert.AreEqual(30f, avg, 1e-4);
            Assert.AreEqual(40f, max);

            h.GetStats(out min, out avg, out max, 2);
            Assert.AreEqual(30f, min);
            Assert.AreEqual(35f, avg, 1e-4);

            var version = h.Version;
            h.Resize(2);
            CollectionAssert.AreEqual(new[] { 30f, 40f }, new[] { h[0], h[1] });
            Assert.AreNotEqual(version, h.Version);

            h.Clear();
            Assert.AreEqual(0, h.Count);
        }

        [Test]
        public void SumNewest_StopsAtTheWindow()
        {
            var h = new FrameHistory(10);
            for (var i = 0; i < 10; i++)
            {
                h.Add(100f);
            }

            Assert.AreEqual(5, h.SumNewest(500f, out var sum));
            Assert.AreEqual(500f, sum);
        }
    }

    public class DebugProfilerTests
    {
        [Test]
        public void Fps_IsComputedFromTheLastHalfSecond()
        {
            var p = new DebugProfiler(100);
            for (var i = 0; i < 60; i++)
            {
                p.AddFrame(1000f / 60f);
            }

            Assert.AreEqual(60f, p.Fps, 0.5f);
            for (var i = 0; i < 30; i++)
            {
                p.AddFrame(1000f / 30f);
            }

            Assert.AreEqual(30f, p.Fps, 0.5f, "Only the newest 0.5 s count.");
        }

        [Test]
        public void RecordsOnlyWhileARecordingHandleIsAlive()
        {
            var p = new DebugProfiler(100);
            p.Sample(0.016f);
            Assert.AreEqual(0, p.FrameTimes.Count, "Nothing is recorded while no view is shown (NFR-01).");

            var a = p.BeginRecording();
            var b = p.BeginRecording();
            p.Sample(0.016f);
            Assert.AreEqual(1, p.FrameTimes.Count);
            a.Dispose();
            Assert.IsTrue(p.IsRecording, "Recording continues while another view uses it.");
            a.Dispose(); // disposing the same handle twice does not release another user
            Assert.IsTrue(p.IsRecording);
            b.Dispose();
            Assert.IsFalse(p.IsRecording);
            p.Sample(0.016f);
            Assert.AreEqual(1, p.FrameTimes.Count);
        }

        [Test]
        public void CustomCounters_AreSampledWhileRecording_AndFailuresShowNotAvailable()
        {
            var p = new DebugProfiler(10);
            var value = 5.0;
            var changed = 0;
            p.Changed += () => changed++;
            var counter = p.AddCounter("Enemies", () => value, ProfilerUnit.Count, "Game");
            var broken = p.AddCounter("Broken", () => throw new InvalidOperationException("boom"));
            Assert.AreEqual(2, changed);
            Assert.AreEqual("Game", counter.Group);

            var recording = p.BeginRecording();
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            p.Sample(0.016f);
            Assert.AreEqual(5.0, counter.Value);
            Assert.AreEqual("5", counter.FormatValue());
            Assert.IsFalse(broken.IsAvailable);
            Assert.AreEqual(DebuggerStrings.Get("profiler.unavailable"), broken.FormatValue());

            Assert.IsTrue(p.RemoveCounter(counter));
            Assert.IsFalse(p.RemoveCounter(counter));
            Assert.AreEqual(3, changed);
            recording.Dispose();
        }

        [Test]
        public void RecorderCounters_StartAndStopWithTheViews()
        {
            var p = new DebugProfiler(10);
            p.AddDefaultCounters();
            var used = p.Find("Total Used");
            Assert.IsNotNull(used);
            Assert.IsTrue(used.IsBuiltIn);
            var missing = p.AddCounter("Missing", ProfilerCategory.Scripts, "No Such Counter 12345");

            var recording = p.BeginRecording();
            p.Sample(0.1f);
            Assert.IsTrue(used.IsAvailable, "Total Used Memory exists in the editor.");
            Assert.GreaterOrEqual(used.Value, 0, "Recorders report a value from the next frame on (0 until then).");
            Assert.IsFalse(missing.IsAvailable);
            recording.Dispose();
        }

        [Test]
        public void Format_UsesUnits_IndependentOfCulture()
        {
            Assert.AreEqual("512 B", ProfilerCounter.Format(512, ProfilerUnit.Bytes));
            Assert.AreEqual("1.5 KB", ProfilerCounter.Format(1536, ProfilerUnit.Bytes));
            Assert.AreEqual("2.0 MB", ProfilerCounter.Format(2 * 1024 * 1024, ProfilerUnit.Bytes));
            Assert.AreEqual("1.25 ms", ProfilerCounter.Format(1_250_000, ProfilerUnit.Nanoseconds));
            Assert.AreEqual("50.0 %", ProfilerCounter.Format(0.5, ProfilerUnit.Percent));
            Assert.AreEqual("1,234", ProfilerCounter.Format(1234, ProfilerUnit.Count));
        }

        [Test]
        public void ProfilerTab_RecordsWhileShown_AndDockStateIsPersisted()
        {
            var storage = new MemoryDebuggerStorage();
            var s = TestDebugger.Create(storage);
            Assert.IsNotNull(s.Tabs.Find(DebugTabIds.Profiler));
            s.ShowDebugPanel(DebugTabIds.Profiler);
            var page = ((SectionedPage)s.Pages.CurrentPage).CurrentSectionPage;
            Assert.IsInstanceOf<ProfilerPage>(page);
            Assert.IsTrue(s.Profiler.IsRecording);

            s.HideDebugPanel();
            Assert.IsFalse(s.Profiler.IsRecording, "Closing the panel stops recording.");

            var raised = 0;
            var dock = s.GetFeature<DockedProfilerFeature>();
            dock.Changed += () => raised++;
            dock.IsDocked = true;
            dock.IsDocked = true;
            Assert.AreEqual(1, raised);
            s.Dispose();

            var s2 = TestDebugger.Create(storage);
            Assert.IsTrue(s2.GetFeature<DockedProfilerFeature>().IsDocked, "Restored from storage.");
            s2.Dispose();

            var s3 = TestDebugger.Create(settings: x => x.Profiler.DockedAtStart = true);
            Assert.IsTrue(s3.GetFeature<DockedProfilerFeature>().IsDocked, "Initial value comes from the settings.");
            s3.Dispose();
        }
    }
}