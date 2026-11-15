using System;
using System.Collections.Generic;
using System.Linq;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace Atsuvanced.Debugger.Tests
{
    /// <summary>
    ///     The per-frame paths must not allocate. Allocations are read from the
    ///     "GC Allocated In Frame" counter's running value, which also works in edit mode.
    /// </summary>
    public class AllocationTests
    {
        /// <summary>
        ///     Bytes allocated by <paramref name="iterations" /> calls. Measured three times and the smallest result is
        ///     returned: the counter also sees allocations made elsewhere in the editor during the run (e.g. leftovers of
        ///     earlier tests), while a real allocation in <paramref name="body" /> shows up every time.
        /// </summary>
        private static long Allocated(Action body, int iterations = 200)
        {
            for (var i = 0; i < 5; i++)
            {
                body(); // warm up (lazy creation, caches, list capacities)
            }

            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            if (!recorder.Valid)
            {
                Assert.Inconclusive("GC Allocated In Frame counter is not available.");
            }

            var min = long.MaxValue;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var before = recorder.CurrentValue;
                for (var i = 0; i < iterations; i++)
                {
                    body();
                }

                min = Math.Min(min, recorder.CurrentValue - before);
            }

            return min;
        }

        [Test]
        public void Measurement_DetectsAllocations()
        {
            var sink = new List<object>();
            Assert.Greater(Allocated(() =>
            {
                sink.Clear();
                sink.Add(new byte[64]);
            }), 0);
        }

        [Test]
        public void Tick_PanelClosed_AllocatesNothing()
        {
            var service = TestDebugger.Create();
            Assert.Greater(service.Overlays.Overlays.Count, 0, "Built-in overlays are registered.");
            Assert.AreEqual(0, Allocated(() => service.Tick(0.016f)));
            service.Dispose();
        }

        [Test]
        public void Tick_ConsoleTabOpen_AllocatesNothing()
        {
            var service = TestDebugger.Create();
            service.Console.Add(LogType.Warning, "hello\nworld", "stack");
            service.Panel.Show(DebugTabIds.Console, false);
            Assert.IsTrue(service.Panel.IsVisible);
            Assert.AreEqual(0, Allocated(() => service.Tick(0.016f)));
            service.Dispose();
        }

        // ---- Typed option reads -------------------------------------------------------------

        private enum Mode
        {
            A,
            B = 5
        }

        private sealed class Values
        {
            public int Int { get; set; } = 7;
            public float Float { get; set; } = 1.5f;
            public bool Bool { get; set; } = true;
            public string Text { get; set; } = "x";
            public Vector3 Vector { get; set; } = new(1, 2, 3);
            public Color Color { get; set; } = Color.red;
            public Mode Mode { get; set; } = Mode.B;
            public byte Byte { get; set; } = 3;
        }

        [Test]
        public void ScannedProperties_ReadTypedWithoutBoxing()
        {
            var values = new Values();
            var options = OptionScanner.Scan(values).ToDictionary(o => o.MemberName);

            Assert.IsTrue(options["Int"].TryGetValue(out int i));
            Assert.AreEqual(7, i);
            Assert.IsTrue(options["Float"].TryGetValue(out float f));
            Assert.AreEqual(1.5f, f);
            Assert.IsTrue(options["Bool"].TryGetValue(out bool b));
            Assert.IsTrue(b);
            Assert.IsTrue(options["Text"].TryGetValue(out string s));
            Assert.AreEqual("x", s);
            Assert.IsTrue(options["Vector"].TryGetValue(out Vector3 v));
            Assert.AreEqual(new Vector3(1, 2, 3), v);
            Assert.IsTrue(options["Color"].TryGetValue(out Color c));
            Assert.AreEqual(Color.red, c);
            Assert.IsTrue(options["Mode"].TryGetEnumValue(out var bits));
            Assert.AreEqual(5, bits);
            Assert.IsTrue(options["Byte"].TryGetValue(out byte by), "Other types fall back to the boxing getter.");
            Assert.AreEqual(3, by);
            Assert.IsFalse(options["Int"].TryGetValue(out float _), "Wrong type.");

            values.Int = 9;
            values.Mode = Mode.A;
            Assert.IsTrue(options["Int"].TryGetValue(out i));
            Assert.AreEqual(9, i, "Reads the live value.");
            Assert.IsTrue(options["Mode"].TryGetEnumValue(out bits));
            Assert.AreEqual(0, bits);

            var intOption = options["Int"];
            var modeOption = options["Mode"];
            var vectorOption = options["Vector"];
            Assert.AreEqual(0, Allocated(() =>
            {
                intOption.TryGetValue(out int _);
                modeOption.TryGetEnumValue(out _);
                vectorOption.TryGetValue(out Vector3 _);
            }));
        }

        [Test]
        public void CreatedOptions_ReadTypedWithoutBoxing()
        {
            var value = 3.25;
            var option = OptionDefinition.Create("D", () => value, v => value = v);
            var mode = Mode.B;
            var enumOption = OptionDefinition.Create("M", () => mode);

            Assert.IsTrue(option.TryGetValue(out double d));
            Assert.AreEqual(3.25, d);
            Assert.IsTrue(enumOption.TryGetEnumValue(out var bits));
            Assert.AreEqual(5, bits);
            Assert.AreEqual(0, Allocated(() =>
            {
                option.TryGetValue(out double _);
                enumOption.TryGetEnumValue(out _);
            }));
        }

        [Test]
        public void Context_ReadsPendingGroupValue()
        {
            var value = 7;
            var option = OptionDefinition.Create("Value", () => value, v => value = v, "G",
                group: new OptionGroupAttribute("g"));
            var apply = OptionDefinition.FromMethod("Apply", () => { }, "G", 1, group: new OptionGroupAttribute("g"));
            var registry = new OptionRegistry();
            OptionGroup group = null;
            registry.GroupAdded += g => group = g;
            registry.AddOption(option);
            registry.AddOption(apply);
            Assert.AreEqual(OptionGroupCommit.OnAction, group.EffectiveCommit);
            var ctx = new OptionContext(option, group, OptionPlacement.Panel);

            Assert.IsTrue(ctx.TryGetValue(out int current));
            Assert.AreEqual(7, current);
            group.SetValue(option, 42);
            Assert.IsTrue(ctx.TryGetValue(out int pending));
            Assert.AreEqual(42, pending, "Pending value of an OnAction group.");
            Assert.AreEqual(7, value);
        }

        // ---- Text caches --------------------------------------------------------------------

        [Test]
        public void LogTextCache_BuildsEachStringOnce()
        {
            var buffer = new ConsoleLogBuffer(10);
            buffer.Add(LogType.Warning, "first\r\nsecond", null);
            buffer.Add(LogType.Log, "single", null);
            var entries = new List<LogEntry>();
            buffer.CopyTo(entries);
            var cache = new LogTextCache(16);

            Assert.AreEqual("first", cache.FirstLine(entries[0]));
            Assert.AreSame(entries[1].Message, cache.FirstLine(entries[1]), "A single line is the message itself.");
            StringAssert.EndsWith("  Warning", cache.Meta(entries[0]));
            StringAssert.IsMatch(@"^\d\d:\d\d:\d\d\.\d\d\d  Warning$", cache.Meta(entries[0]));
            StringAssert.IsMatch(@"^\d\d:\d\d:\d\d$", cache.Time(entries[0]));
            Assert.AreSame(cache.Meta(entries[0]), cache.Meta(entries[0]));

            var e = entries[0];
            Assert.AreEqual(0, Allocated(() =>
            {
                cache.FirstLine(e);
                cache.Meta(e);
                cache.Time(e);
            }));
        }

        [Test]
        public void ValueTextCache_MatchesProfilerFormat()
        {
            var cache = new ValueTextCache();
            var samples = new[] { 0, 512, 1023.4, 2048, 5.5 * 1024 * 1024, 3.25 * 1024 * 1024 * 1024, -4096 };
            foreach (var v in samples)
            {
                Assert.AreEqual(ProfilerCounter.Format(v, ProfilerUnit.Bytes), cache.GetCounter(v, ProfilerUnit.Bytes));
            }

            foreach (var unit in new[] { ProfilerUnit.Milliseconds, ProfilerUnit.Percent, ProfilerUnit.Count, ProfilerUnit.Raw })
            {
                Assert.AreEqual(ProfilerCounter.Format(0.1234, unit), cache.GetCounter(0.1234, unit));
                Assert.AreEqual(ProfilerCounter.Format(1234.5, unit), cache.GetCounter(1234.5, unit));
            }

            var ms = new ValueTextCache(2, " ms");
            Assert.AreEqual("16.67 ms", ms.Get(16.6666));
            Assert.AreSame(ms.Get(16.6666), ms.Get(16.6701), "Same displayed text, same string.");
            Assert.AreEqual(0, Allocated(() => ms.Get(16.6666)));
        }

        [Test]
        public void NumberText_IsCached()
        {
            Assert.AreEqual("123", NumberText.Get(123));
            Assert.AreSame(NumberText.Get(123), NumberText.Get(123));
            Assert.AreEqual("-5", NumberText.Get(-5));
            Assert.AreEqual(0, Allocated(() => NumberText.Get(42)));
        }

        [Test]
        public void ConsoleBuffer_CopyTo_DoesNotReallocateWhileFilling()
        {
            var buffer = new ConsoleLogBuffer(2000);
            var snapshot = new List<LogEntry>();
            buffer.Add(LogType.Log, "m", null);
            buffer.CopyTo(snapshot);
            Assert.AreEqual(0, Allocated(() =>
            {
                buffer.Add(LogType.Log, "m", null);
                buffer.CopyTo(snapshot);
            }, 100), "The snapshot list grows to the buffer capacity once, not by one entry each time.");
        }

        [Test]
        public void ConsoleFilter_Collapse_ReusesItsIndex()
        {
            var source = new List<LogEntry>();
            var buffer = new ConsoleLogBuffer(100);
            for (var i = 0; i < 50; i++)
            {
                buffer.Add(i % 2 == 0 ? LogType.Log : LogType.Error, "m" + i % 5, "s");
            }

            buffer.CopyTo(source);
            var filter = new ConsoleFilter { Collapse = true };
            var items = new List<ConsoleFilter.Item>();
            filter.Apply(source, items);
            Assert.AreEqual(10, items.Count, "Type (2) x message (5).");
            Assert.AreEqual(0, Allocated(() => filter.Apply(source, items)));
        }
    }
}
