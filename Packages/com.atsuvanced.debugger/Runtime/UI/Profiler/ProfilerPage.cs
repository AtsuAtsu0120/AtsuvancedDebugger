using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Profiler tab (FR-PRF-01〜04): frame time graph, FPS, CPU / GPU frame times and the counters of
    ///     <see cref="DebugProfiler" /> grouped by <see cref="ProfilerCounter.Group" />. Recording runs only while the page is
    ///     shown.
    /// </summary>
    /// <remarks>
    ///     Public and inheritable: register a subclass as the <c>"overview"</c> section of the <c>DebugTabIds.Profiler</c>
    ///     tab.
    ///     When overriding a <c>protected</c> member, call the base implementation.
    /// </remarks>
    public class ProfilerPage : DebugPage
    {
        private readonly List<(ProfilerCounter counter, Label value, ValueTextCache text)> _counterRows = new();
        private readonly ValueTextCache _fpsText = new(1, null);
        private readonly ValueTextCache _cpuMainText = new(2, " ms");
        private readonly ValueTextCache _cpuRenderText = new(2, " ms");
        private readonly ValueTextCache _gpuText = new(2, " ms");
        private readonly ValueTextCache _latestText = new(2, " ms");
        private readonly ValueTextCache _minText = new(1, null), _avgText = new(1, null), _maxText = new(1, null);
        private Label _frameLatest, _frameMin, _frameAvg, _frameMax;
        private VisualElement _frameStats;
        private int _legendTarget = -1;
        private VisualElement _counters;
        private bool _countersDirty = true;
        private Button _dockButton;
        private FrameGraph _graph;
        private Label _legend, _fps, _frame, _cpuMain, _cpuRender, _gpu;
        private IDisposable _recording;

        public ProfilerPage() : base(DebuggerStrings.Get("tab.profiler"))
        {
        }

        protected override float UpdateInterval => 0.1f;

        protected override VisualElement CreateContent()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("atsu-profiler");

            var graphBox = new VisualElement();
            graphBox.AddToClassList("atsu-profiler__graph-box");
            _legend = new Label();
            _legend.AddToClassList("atsu-profiler__legend");
            graphBox.Add(_legend);
            _graph = new FrameGraph();
            _graph.AddToClassList("atsu-profiler__graph");
            graphBox.Add(_graph);
            scroll.Add(graphBox);

            var frame = Section(scroll, DebuggerStrings.Get("profiler.frame"));
            _fps = Row(frame, DebuggerStrings.Get("profiler.fps"));
            _frame = Row(frame, DebuggerStrings.Get("profiler.frameTime"));
            BuildFrameStats();
            _cpuMain = Row(frame, DebuggerStrings.Get("profiler.cpuMain"));
            _cpuRender = Row(frame, DebuggerStrings.Get("profiler.cpuRender"));
            _gpu = Row(frame, DebuggerStrings.Get("profiler.gpu"));

            _counters = new VisualElement();
            scroll.Add(_counters);

            var note = new Label(DebuggerStrings.Get("profiler.unavailableNote"));
            note.AddToClassList("atsu-profiler__note");
            scroll.Add(note);
            return scroll;
        }

        protected override VisualElement CreateToolbar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            _dockButton = new Button(() =>
            {
                var dock = Debugger.GetFeature<DockedProfilerFeature>();
                if (dock != null)
                {
                    dock.IsDocked = !dock.IsDocked;
                }
            })
            {
                tooltip = DebuggerStrings.Get("profiler.dockTooltip")
            };
            _dockButton.AddToClassList("atsu-button");
            var reset = new Button(() => Debugger.Profiler.ResetHistory())
                { text = DebuggerStrings.Get("profiler.reset") };
            reset.AddToClassList("atsu-button");
            bar.Add(_dockButton);
            bar.Add(reset);
            return bar;
        }

        protected override void OnShow()
        {
            var profiler = Debugger.Profiler;
            _recording ??= profiler.BeginRecording();

            profiler.Changed += MarkCountersDirty;
            _graph.History = profiler.FrameTimes;
            Refresh();
        }

        protected override void OnHide()
        {
            Debugger.Profiler.Changed -= MarkCountersDirty;
            _recording?.Dispose();
            _recording = null;
        }

        protected override void OnUpdate()
        {
            Refresh();
        }

        private void MarkCountersDirty()
        {
            _countersDirty = true;
        }

        private void Refresh()
        {
            var p = Debugger.Profiler;
            if (_countersDirty)
            {
                RebuildCounters();
            }

            // Every text below comes from a cache keyed by the displayed value, so the 10 Hz refresh allocates
            // only for values not shown recently.
            var target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            if (target != _legendTarget)
            {
                _legendTarget = target;
                _legend.text = DebuggerStrings.Format("profiler.graphLegend",
                    (1000f / target).ToString("F1", CultureInfo.InvariantCulture) + " ms");
            }

            _graph.Refresh();

            SetText(_fps, _fpsText.Get(p.Fps));
            p.FrameTimes.GetStats(out var min, out var avg, out var max);
            var hasFrames = p.FrameTimes.Count > 0;
            SetText(_frame, hasFrames ? string.Empty : "-");
            _frame.EnableInClassList(UssClasses.Hidden, hasFrames);
            _frameStats.EnableInClassList(UssClasses.Hidden, !hasFrames);
            if (hasFrames)
            {
                SetText(_frameLatest, _latestText.Get(p.FrameTimes.Latest));
                SetText(_frameMin, _minText.Get(min));
                SetText(_frameAvg, _avgText.Get(avg));
                SetText(_frameMax, _maxText.Get(max));
            }

            SetText(_cpuMain, Ms(p.CpuMainThreadMs, _cpuMainText));
            SetText(_cpuRender, Ms(p.CpuRenderThreadMs, _cpuRenderText));
            SetText(_gpu, Ms(p.GpuMs, _gpuText));

            for (var i = 0; i < _counterRows.Count; i++)
            {
                var (counter, label, text) = _counterRows[i];
                SetText(label, counter.IsAvailable
                    ? text.GetCounter(counter.Value, counter.Unit)
                    : DebuggerStrings.Get("profiler.unavailable"));
            }

            if (_dockButton != null)
            {
                // The button exists only when the docked profiler feature is installed.
                var dock = Debugger.GetFeature<DockedProfilerFeature>();
                _dockButton.EnableInClassList(UssClasses.Hidden, dock == null);
                var docked = dock?.IsDocked ?? false;
                SetText(_dockButton, DebuggerStrings.Get(docked ? "profiler.undock" : "profiler.dock"));
                _dockButton.EnableInClassList("atsu-button--primary", docked);
            }
        }

        private void RebuildCounters()
        {
            _countersDirty = false;
            _counterRows.Clear();
            _counters.Clear();
            var groups = new Dictionary<string, VisualElement>();
            foreach (var counter in Debugger.Profiler.Counters)
            {
                if (!groups.TryGetValue(counter.Group, out var section))
                {
                    section = Section(_counters, counter.Group);
                    groups.Add(counter.Group, section);
                }

                _counterRows.Add((counter, Row(section, counter.Name), new ValueTextCache()));
            }
        }

        private static string Ms(float ms, ValueTextCache cache)
        {
            return ms < 0f ? DebuggerStrings.Get("profiler.unavailable") : cache.Get(ms);
        }

        /// <summary>
        ///     "16.67 ms  (min 16.1 / avg 16.7 / max 17.2)" as separate labels next to the frame time value, so only the
        ///     numbers change (each from a cache).
        /// </summary>
        private void BuildFrameStats()
        {
            var row = _frame.parent;
            _frameStats = new VisualElement();
            _frameStats.AddToClassList("atsu-info-row__value");
            _frameStats.AddToClassList("atsu-profiler__frame-stats");
            _frameLatest = Part(null);
            Part("(" + DebuggerStrings.Get("profiler.min"));
            _frameMin = Part(null);
            Part("/ " + DebuggerStrings.Get("profiler.avg"));
            _frameAvg = Part(null);
            Part("/ " + DebuggerStrings.Get("profiler.max"));
            _frameMax = Part(null);
            Part(")").AddToClassList("atsu-profiler__frame-part--tight");
            row.Add(_frameStats);

            // Words are separate labels spaced by USS (leading / trailing spaces are not kept by every text setting).
            Label Part(string text)
            {
                var l = new Label(text);
                l.AddToClassList("atsu-profiler__frame-part");
                _frameStats.Add(l);
                return l;
            }
        }

        private static void SetText(TextElement label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static VisualElement Section(VisualElement parent, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("atsu-profiler__section");
            var header = new Label(title);
            header.AddToClassList("atsu-profiler__section-title");
            section.Add(header);
            parent.Add(section);
            return section;
        }

        private static Label Row(VisualElement parent, string title)
        {
            var row = new VisualElement();
            row.AddToClassList("atsu-info-row");
            var t = new Label(title);
            t.AddToClassList("atsu-info-row__title");
            var v = new Label("-");
            v.AddToClassList("atsu-info-row__value");
            row.Add(t);
            row.Add(v);
            parent.Add(row);
            return v;
        }

        protected override void OnDispose()
        {
            _recording?.Dispose();
            _recording = null;
        }
    }
}