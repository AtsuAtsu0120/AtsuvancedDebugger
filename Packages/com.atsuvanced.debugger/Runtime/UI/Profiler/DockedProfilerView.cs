using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     FPS / frame time / memory shown in a screen corner while the panel is closed (FR-PRF-05).
    ///     Tap to open the Profiler tab. Records only while it is on screen.
    /// </summary>
    internal sealed class DockedProfilerView : VisualElement
    {
        private const float TextInterval = 0.25f;
        private readonly Label _fps, _ms, _memory;
        private readonly FrameGraph _graph;

        private readonly DebugProfiler _profiler;
        private readonly ProfilerCounter _totalUsed;
        private readonly ValueTextCache _fpsText = new(0, " FPS");
        private readonly ValueTextCache _msText = new(1, " ms");
        private readonly ValueTextCache _memoryText = new();
        private IDisposable _recording;
        private float _timer;

        public DockedProfilerView(DebugProfiler profiler, ProfilerSettings settings, Action open)
        {
            _profiler = profiler;
            name = "atsu-docked-profiler";
            AddToClassList("atsu-docked-profiler");
            style.opacity = Mathf.Clamp(settings.DockedOpacity, 0.1f, 1f);

            var text = new VisualElement();
            text.AddToClassList("atsu-docked-profiler__text");
            _fps = Value("atsu-docked-profiler__fps");
            _ms = Value("atsu-docked-profiler__ms");
            _memory = Value("atsu-docked-profiler__memory");
            text.Add(_fps);
            text.Add(_ms);
            text.Add(_memory);
            Add(text);

            _graph = new FrameGraph { History = profiler.FrameTimes, ShowGuides = false };
            _graph.AddToClassList("atsu-docked-profiler__graph");
            Add(_graph);

            _totalUsed = profiler.Find("Total Used");
            this.AddManipulator(new Clickable(() => open?.Invoke()));
        }

        /// <param name="shown">Whether the view is on screen (recording runs only then).</param>
        public void Tick(float unscaledDeltaTime, bool shown)
        {
            if (shown != (_recording != null))
            {
                if (shown)
                {
                    _recording = _profiler.BeginRecording();
                    _timer = 0f;
                }
                else
                {
                    _recording.Dispose();
                    _recording = null;
                }
            }

            if (!shown)
            {
                return;
            }

            _timer -= unscaledDeltaTime;
            if (_timer > 0f)
            {
                return;
            }

            _timer = TextInterval;

            // Cached by displayed value: refreshing allocates only for values not shown recently.
            Set(_fps, _fpsText.Get(_profiler.Fps));
            Set(_ms, _msText.Get(_profiler.FrameTimes.Latest));
            Set(_memory,
                _totalUsed != null && _totalUsed.IsAvailable
                    ? _memoryText.GetCounter(_totalUsed.Value, ProfilerUnit.Bytes)
                    : string.Empty);
            _memory.EnableInClassList(UssClasses.Hidden, _totalUsed == null || !_totalUsed.IsAvailable);
            _graph.Refresh();
        }

        public void Dispose()
        {
            _recording?.Dispose();
            _recording = null;
        }

        private static void Set(Label label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static Label Value(string cls)
        {
            var l = new Label();
            l.AddToClassList("atsu-docked-profiler__value");
            l.AddToClassList(cls);
            return l;
        }
    }
}