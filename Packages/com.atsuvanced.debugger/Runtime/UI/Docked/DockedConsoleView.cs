using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Console docked to the top / bottom of the game screen while the panel is closed (FR-CON-08).
    ///     <code>
    /// .atsu-dock
    ///   .atsu-dock__bar     [counts | latest message (collapsed) | ▲▼]  Open  ×
    ///   .atsu-dock__lines   newest N lines (expanded); tap opens the Console tab
    /// </code>
    /// </summary>
    internal sealed class DockedConsoleView : VisualElement
    {
        internal const float LineHeight = 18f;
        private readonly Label _info, _warning, _error, _latest, _arrow;
        private readonly List<Line> _lineLabels = new();
        private readonly LogTextCache _text = new(64);
        private readonly VisualElement _lines;
        private readonly Action _openConsole;
        private readonly DockedConsoleSettings _settings;

        private readonly IDockedConsole _state;
        private bool _layoutDirty = true;

        // What is on screen now, so a render only touches what changed (and allocates nothing).
        private int _shownInfo = -1, _shownWarning = -1, _shownError = -1;
        private long _shownLatest = long.MinValue;

        public DockedConsoleView(ConsoleLogBuffer buffer, IDockedConsole state, DockedConsoleSettings settings,
            Action openConsole)
        {
            _state = state;
            _settings = settings;
            _openConsole = openConsole;
            Model = new DockedConsoleModel(buffer);
            name = "atsu-dock";
            AddToClassList("atsu-dock");
            style.opacity = Mathf.Clamp(settings.Opacity, 0.1f, 1f);

            var bar = new VisualElement();
            bar.AddToClassList("atsu-dock__bar");

            // The toggle area is a sibling of the buttons, so a button press is never also a toggle.
            var toggle = new VisualElement();
            toggle.AddToClassList("atsu-dock__toggle");
            _info = Count("atsu-dock__count--info");
            _warning = Count("atsu-dock__count--warning");
            _error = Count("atsu-dock__count--error");
            _latest = new Label();
            _latest.AddToClassList("atsu-dock__latest");
            _arrow = new Label();
            _arrow.AddToClassList("atsu-dock__arrow");
            toggle.Add(_info);
            toggle.Add(_warning);
            toggle.Add(_error);
            toggle.Add(_latest);
            toggle.Add(_arrow);
            toggle.AddManipulator(new Clickable(() => _state.IsExpanded = !_state.IsExpanded));
            bar.Add(toggle);

            var open = new Button(() => _openConsole?.Invoke()) { text = DebuggerStrings.Get("common.open") };
            open.AddToClassList("atsu-button");
            open.AddToClassList("atsu-button--small");
            bar.Add(open);
            var close = new Button(() => _state.IsVisible = false)
                { text = "×", tooltip = DebuggerStrings.Get("dock.undock") };
            close.AddToClassList("atsu-icon-button");
            close.AddToClassList("atsu-dock__close");
            bar.Add(close);

            Add(bar);

            _lines = new VisualElement();
            _lines.AddToClassList("atsu-dock__lines");
            _lines.AddManipulator(new Clickable(() => _openConsole?.Invoke()));

            state.Changed += OnStateChanged;
        }

        /// <summary>Whether the view wants to be on screen (the panel decides together with its own visibility).</summary>
        public bool WantsVisible => _state.IsVisible;

        public ConsoleAlignment Alignment => _state.Alignment;

        internal DockedConsoleModel Model { get; }

        public void Dispose()
        {
            _state.Changed -= OnStateChanged;
        }

        private void OnStateChanged()
        {
            _layoutDirty = true;
        }

        /// <param name="shown">Whether the view is currently on screen (logs are only read then).</param>
        public void Tick(bool shown)
        {
            if (_layoutDirty)
            {
                ApplyLayout();
            }

            if (!shown)
            {
                return;
            }

            if (Model.Update(_settings.Lines))
            {
                Render();
            }
        }

        private void ApplyLayout()
        {
            _layoutDirty = false;
            var top = _state.Alignment == ConsoleAlignment.Top;
            EnableInClassList("atsu-dock--top", top);
            EnableInClassList("atsu-dock--bottom", !top);
            EnableInClassList("atsu-dock--expanded", _state.IsExpanded);

            // Lines sit between the bar and the screen edge's opposite side: below the bar at the top, above it at the bottom.
            _lines.RemoveFromHierarchy();
            var bar = this.Q(className: "atsu-dock__bar");
            if (_state.IsExpanded)
            {
                if (top)
                {
                    Add(_lines);
                }
                else
                {
                    Insert(IndexOf(bar), _lines);
                }
            }

            var count = Mathf.Clamp(_settings.Lines, 1, 30);
            _lines.style.height = count * LineHeight;
            while (_lineLabels.Count < count)
            {
                var line = new Line();
                _lineLabels.Add(line);
                _lines.Add(line.Root);
            }

            for (var i = 0; i < _lineLabels.Count; i++)
            {
                _lineLabels[i].Root.EnableInClassList(UssClasses.Hidden, i >= count);
            }

            _arrow.text = _state.IsExpanded == top ? "▴" : "▾";
            Render();
        }

        private void Render()
        {
            SetCount(_info, ref _shownInfo, Model.InfoCount);
            SetCount(_warning, ref _shownWarning, Model.WarningCount);
            SetCount(_error, ref _shownError, Model.ErrorCount);

            var latest = Model.Latest;
            var latestKey = !_state.IsExpanded && latest.HasValue ? latest.Value.Sequence : -1;
            if (latestKey != _shownLatest)
            {
                _shownLatest = latestKey;
                _latest.text = latestKey >= 0 ? _text.FirstLine(latest.Value) : string.Empty;
                SetSeverity(_latest, latestKey >= 0 ? (LogSeverity?)latest.Value.Severity : null);
            }

            // Collapsed: the lines are not in the hierarchy, so they are not touched at all.
            if (!_state.IsExpanded)
            {
                return;
            }

            // Newest line next to the bar: at the bottom of the list when docked at the bottom, at the top when docked at the top.
            var lines = Model.Lines;
            var top = _state.Alignment == ConsoleAlignment.Top;
            var count = Mathf.Min(_lineLabels.Count, Mathf.Clamp(_settings.Lines, 1, 30));
            for (var i = 0; i < count; i++)
            {
                var line = _lineLabels[i];
                // i is the visual slot (top to bottom); fromNewest counts from the bar side.
                var fromNewest = top ? i : count - 1 - i;
                var index = lines.Count - 1 - fromNewest;
                if (index < 0)
                {
                    line.Clear();
                    continue;
                }

                var e = lines[index];
                if (line.Sequence == e.Sequence)
                {
                    continue;
                }

                line.Sequence = e.Sequence;
                line.Time.text = _text.Time(e);
                line.Text.text = _text.FirstLine(e);
                SetSeverity(line.Time, e.Severity);
                SetSeverity(line.Text, e.Severity);
            }
        }

        private static void SetCount(Label label, ref int shown, int value)
        {
            if (shown == value)
            {
                return;
            }

            shown = value;
            label.text = NumberText.Get(value);
        }

        private static void SetSeverity(VisualElement e, LogSeverity? severity)
        {
            e.EnableInClassList("atsu-dock--warning", severity == LogSeverity.Warning);
            e.EnableInClassList("atsu-dock--error", severity == LogSeverity.Error);
        }

        private static Label Count(string cls)
        {
            var l = new Label("0");
            l.AddToClassList("atsu-dock__count");
            l.AddToClassList(cls);
            return l;
        }

        /// <summary>One docked line: time and first line of the message in separate labels (no per-line string building).</summary>
        private sealed class Line
        {
            public readonly VisualElement Root = new();
            public readonly Label Text = new();
            public readonly Label Time = new();
            public long Sequence = -1;

            public Line()
            {
                Root.AddToClassList("atsu-dock__line");
                Time.AddToClassList("atsu-dock__line-time");
                Text.AddToClassList("atsu-dock__line-text");
                Root.Add(Time);
                Root.Add(Text);
            }

            public void Clear()
            {
                if (Sequence == -1)
                {
                    return;
                }

                Sequence = -1;
                Time.text = string.Empty;
                Text.text = string.Empty;
                SetSeverity(Time, null);
                SetSeverity(Text, null);
            }
        }
    }
}
