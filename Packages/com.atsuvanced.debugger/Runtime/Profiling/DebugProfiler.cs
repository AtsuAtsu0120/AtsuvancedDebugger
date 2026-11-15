using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>How a counter value is formatted.</summary>
    public enum ProfilerUnit
    {
        /// <summary>Plain number ("1,234").</summary>
        Count,

        /// <summary>Bytes, shown as KB / MB / GB.</summary>
        Bytes,

        /// <summary>Milliseconds.</summary>
        Milliseconds,

        /// <summary>Nanoseconds, shown as milliseconds (ProfilerRecorder time counters report ns).</summary>
        Nanoseconds,

        /// <summary>0..1 shown as a percentage.</summary>
        Percent,

        /// <summary>Number with two decimals, no unit.</summary>
        Raw
    }

    /// <summary>
    ///     A value shown in the Profiler tab: either a Unity profiler counter read with <see cref="ProfilerRecorder" />,
    ///     or any value returned by a delegate. Counters are only read while the Profiler tab or the docked profiler is shown.
    /// </summary>
    public sealed class ProfilerCounter
    {
        private readonly ProfilerCategory _category;
        private readonly Func<double> _getter;
        private readonly string _statName;
        private bool _getterFailed;
        private ProfilerRecorder _recorder;

        internal ProfilerCounter(string name, string group, ProfilerUnit unit, ProfilerCategory category,
            string statName, bool builtIn)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Group = string.IsNullOrEmpty(group) ? "Custom" : group;
            Unit = unit;
            _category = category;
            _statName = statName ?? throw new ArgumentNullException(nameof(statName));
            IsBuiltIn = builtIn;
        }

        internal ProfilerCounter(string name, string group, ProfilerUnit unit, Func<double> getter, bool builtIn)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Group = string.IsNullOrEmpty(group) ? "Custom" : group;
            Unit = unit;
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            IsBuiltIn = builtIn;
            IsAvailable = true;
        }

        public string Name { get; }

        /// <summary>Heading the counter is listed under ("Memory", "Rendering", or your own).</summary>
        public string Group { get; }

        public ProfilerUnit Unit { get; }

        /// <summary>The latest value (raw, in <see cref="Unit" />).</summary>
        public double Value { get; private set; }

        /// <summary>False when the counter does not exist on this platform / build (e.g. render counters in a release player).</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>True for counters added by the debugger itself.</summary>
        public bool IsBuiltIn { get; }

        internal bool UsesRecorder => _getter == null;

        internal void Start()
        {
            if (_getter != null)
            {
                return;
            }

            try
            {
                _recorder = ProfilerRecorder.StartNew(_category, _statName);
                IsAvailable = _recorder.Valid;
            }
            catch (Exception)
            {
                IsAvailable = false;
            }
        }

        internal void Stop()
        {
            if (_getter != null)
            {
                return;
            }

            if (_recorder.Valid)
            {
                _recorder.Dispose();
            }

            _recorder = default;
        }

        internal void Sample()
        {
            if (_getter != null)
            {
                if (_getterFailed)
                {
                    return;
                }

                try
                {
                    Value = _getter();
                }
                catch (Exception e)
                {
                    _getterFailed = true; // log once, then show n/a
                    IsAvailable = false;
                    Debug.LogException(e);
                }

                return;
            }

            if (!_recorder.Valid)
            {
                return;
            }

            Value = _recorder.LastValue;
        }

        /// <summary>The value formatted with its unit (invariant culture).</summary>
        public string FormatValue()
        {
            return IsAvailable ? Format(Value, Unit) : DebuggerStrings.Get("profiler.unavailable");
        }

        public static string Format(double value, ProfilerUnit unit)
        {
            var c = CultureInfo.InvariantCulture;
            switch (unit)
            {
                case ProfilerUnit.Bytes: return FormatBytes(value);
                case ProfilerUnit.Milliseconds: return value.ToString("F2", c) + " ms";
                case ProfilerUnit.Nanoseconds: return (value / 1_000_000.0).ToString("F2", c) + " ms";
                case ProfilerUnit.Percent: return (value * 100.0).ToString("F1", c) + " %";
                case ProfilerUnit.Raw: return value.ToString("F2", c);
                default: return value.ToString("N0", c);
            }
        }

        public static string FormatBytes(double bytes)
        {
            var c = CultureInfo.InvariantCulture;
            var abs = Math.Abs(bytes);
            if (abs >= 1024.0 * 1024 * 1024)
            {
                return (bytes / (1024.0 * 1024 * 1024)).ToString("F2", c) + " GB";
            }

            if (abs >= 1024.0 * 1024)
            {
                return (bytes / (1024.0 * 1024)).ToString("F1", c) + " MB";
            }

            if (abs >= 1024.0)
            {
                return (bytes / 1024.0).ToString("F1", c) + " KB";
            }

            return bytes.ToString("F0", c) + " B";
        }

        public override string ToString()
        {
            return $"Counter:{Group}/{Name}";
        }
    }

    /// <summary>
    ///     Frame timing and counters of the Profiler tab / docked profiler (FR-PRF-01〜04).
    ///     Nothing is recorded while no profiler UI is shown (NFR-01); recording starts when a view acquires it.
    ///     Main-thread only.
    /// </summary>
    public sealed class DebugProfiler
    {
        private const float CounterInterval = 0.25f;
        private const float FpsWindow = 0.5f;

        private readonly List<ProfilerCounter> _counters = new();
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private float _counterTimer;
        private ProfilerRecorder _mainThread;
        private int _users;

        internal DebugProfiler(int historyFrames)
        {
            FrameTimes = new FrameHistory(Math.Max(2, historyFrames));
        }

        /// <summary>Wall-clock time of each frame in milliseconds (newest last).</summary>
        public FrameHistory FrameTimes { get; }

        /// <summary>Frames per second over the last half second.</summary>
        public float Fps { get; private set; }

        /// <summary>CPU main thread time of the latest frame in ms, or -1 if unavailable.</summary>
        public float CpuMainThreadMs { get; private set; } = -1f;

        /// <summary>CPU render thread time of the latest frame in ms, or -1 if unavailable.</summary>
        public float CpuRenderThreadMs { get; private set; } = -1f;

        /// <summary>
        ///     GPU time of the latest frame in ms, or -1 if unavailable
        ///     (needs Player Settings &gt; Frame Timing Stats and a supported graphics API).
        /// </summary>
        public float GpuMs { get; private set; } = -1f;

        public IReadOnlyList<ProfilerCounter> Counters => _counters;

        /// <summary>True while at least one <see cref="BeginRecording" /> handle is alive (e.g. a profiler view is shown).</summary>
        public bool IsRecording => _users > 0;

        /// <summary>Whether FrameTimingManager returned data (Player Settings &gt; Frame Timing Stats).</summary>
        public bool IsFrameTimingAvailable { get; private set; }

        /// <summary>Raised when counters are added or removed.</summary>
        public event Action Changed;

        /// <summary>
        ///     Adds a Unity profiler counter (FR-PRF-04), e.g.
        ///     <c>AddCounter("Physics Bodies", ProfilerCategory.Physics, "Active Dynamic Bodies")</c>.
        /// </summary>
        public ProfilerCounter AddCounter(string name, ProfilerCategory category, string statName,
            ProfilerUnit unit = ProfilerUnit.Count, string group = null)
        {
            return Add(new ProfilerCounter(name, group ?? DebuggerStrings.Get("profiler.custom"), unit, category,
                statName, false));
        }

        /// <summary>Adds a value read from a delegate (called a few times per second while shown).</summary>
        public ProfilerCounter AddCounter(string name, Func<double> getter, ProfilerUnit unit = ProfilerUnit.Count,
            string group = null)
        {
            return Add(new ProfilerCounter(name, group ?? DebuggerStrings.Get("profiler.custom"), unit, getter, false));
        }

        public bool RemoveCounter(ProfilerCounter counter)
        {
            if (counter == null || !_counters.Remove(counter))
            {
                return false;
            }

            if (IsRecording)
            {
                counter.Stop();
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Clears the frame history.</summary>
        public void ResetHistory()
        {
            FrameTimes.Clear();
            Fps = 0f;
        }

        internal ProfilerCounter AddBuiltIn(string name, string group, ProfilerUnit unit, ProfilerCategory category,
            string statName)
        {
            return Add(new ProfilerCounter(name, group, unit, category, statName, true));
        }

        private ProfilerCounter Add(ProfilerCounter counter)
        {
            _counters.Add(counter);
            if (IsRecording)
            {
                counter.Start();
                counter.Sample();
            }

            Changed?.Invoke();
            return counter;
        }

        internal void AddDefaultCounters()
        {
            var memory = DebuggerStrings.Get("profiler.memory");
            AddBuiltIn("Total Used", memory, ProfilerUnit.Bytes, ProfilerCategory.Memory, "Total Used Memory");
            AddBuiltIn("Total Reserved", memory, ProfilerUnit.Bytes, ProfilerCategory.Memory, "Total Reserved Memory");
            AddBuiltIn("GC Used", memory, ProfilerUnit.Bytes, ProfilerCategory.Memory, "GC Used Memory");
            AddBuiltIn("GC Reserved", memory, ProfilerUnit.Bytes, ProfilerCategory.Memory, "GC Reserved Memory");
            AddBuiltIn("GC Alloc / Frame", memory, ProfilerUnit.Bytes, ProfilerCategory.Memory,
                "GC Allocated In Frame");
            AddBuiltIn("System Used", memory, ProfilerUnit.Bytes, ProfilerCategory.Memory, "System Used Memory");

            var rendering = DebuggerStrings.Get("profiler.rendering");
            AddBuiltIn("Draw Calls", rendering, ProfilerUnit.Count, ProfilerCategory.Render, "Draw Calls Count");
            AddBuiltIn("Batches", rendering, ProfilerUnit.Count, ProfilerCategory.Render, "Batches Count");
            AddBuiltIn("SetPass Calls", rendering, ProfilerUnit.Count, ProfilerCategory.Render, "SetPass Calls Count");
            AddBuiltIn("Triangles", rendering, ProfilerUnit.Count, ProfilerCategory.Render, "Triangles Count");
            AddBuiltIn("Vertices", rendering, ProfilerUnit.Count, ProfilerCategory.Render, "Vertices Count");
        }

        /// <summary>Finds a counter by name (first match).</summary>
        public ProfilerCounter Find(string name)
        {
            foreach (var c in _counters)
            {
                if (c.Name == name)
                {
                    return c;
                }
            }

            return null;
        }

        // ---- Recording (driven by the views) --------------------------------------------

        /// <summary>
        ///     Starts recording frame times and counters until the returned handle is disposed. Recording is reference-counted:
        ///     it starts with the first handle (clearing the history) and stops when the last one is disposed. Take a handle while
        ///     a view that shows profiler data is visible (e.g. from <c>DebugPage.OnShow</c>, disposing it in <c>OnHide</c>).
        /// </summary>
        public IDisposable BeginRecording()
        {
            Acquire();
            return new RecordingHandle(this);
        }

        private void Acquire()
        {
            if (_users++ > 0)
            {
                return;
            }

            FrameTimes.Clear();
            Fps = 0f;
            _counterTimer = 0f;
            foreach (var c in _counters)
            {
                c.Start();
            }

            try
            {
                _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            }
            catch (Exception)
            {
                _mainThread = default;
            }
        }

        private void Release()
        {
            if (_users == 0 || --_users > 0)
            {
                return;
            }

            foreach (var c in _counters)
            {
                c.Stop();
            }

            if (_mainThread.Valid)
            {
                _mainThread.Dispose();
            }

            _mainThread = default;
        }

        /// <summary>Called once per frame (unscaled delta time in seconds). Does nothing while not recording.</summary>
        internal void Sample(float unscaledDeltaTime)
        {
            if (!IsRecording)
            {
                return;
            }

            AddFrame(unscaledDeltaTime * 1000f);
            SampleFrameTiming();

            _counterTimer -= unscaledDeltaTime;
            if (_counterTimer > 0f)
            {
                return;
            }

            _counterTimer = CounterInterval;
            foreach (var c in _counters)
            {
                c.Sample();
            }
        }

        /// <summary>Adds one frame time (ms) and updates the FPS. Exposed for tests.</summary>
        internal void AddFrame(float frameMs)
        {
            if (frameMs <= 0f)
            {
                return;
            }

            FrameTimes.Add(frameMs);
            var n = FrameTimes.SumNewest(FpsWindow * 1000f, out var sum);
            Fps = sum > 0f ? n * 1000f / sum : 0f;
        }

        private void SampleFrameTiming()
        {
            var main = -1f;
            var render = -1f;
            var gpu = -1f;

            try
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
                {
                    var t = _timings[0];
                    if (t.cpuMainThreadFrameTime > 0)
                    {
                        main = (float)t.cpuMainThreadFrameTime;
                    }

                    if (t.cpuRenderThreadFrameTime > 0)
                    {
                        render = (float)t.cpuRenderThreadFrameTime;
                    }

                    if (t.gpuFrameTime > 0)
                    {
                        gpu = (float)t.gpuFrameTime;
                    }

                    IsFrameTimingAvailable = true;
                }
            }
            catch (Exception)
            {
                IsFrameTimingAvailable = false;
            }

            // Fall back to the profiler's main thread marker when frame timing stats are off.
            if (main < 0f && _mainThread.Valid && _mainThread.LastValue > 0)
            {
                main = _mainThread.LastValue / 1_000_000f;
            }

            CpuMainThreadMs = main;
            CpuRenderThreadMs = render;
            GpuMs = gpu;
        }

        internal void Dispose()
        {
            if (_users == 0)
            {
                return;
            }

            _users = 1;
            Release();
        }

        private sealed class RecordingHandle : IDisposable
        {
            private DebugProfiler _profiler;

            public RecordingHandle(DebugProfiler profiler)
            {
                _profiler = profiler;
            }

            public void Dispose()
            {
                _profiler?.Release();
                _profiler = null;
            }
        }
    }
}