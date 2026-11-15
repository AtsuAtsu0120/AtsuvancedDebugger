using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Pinned options drawn over the game while the panel is closed (FR-VIEW-07: views get
    ///     <see cref="OptionPlacement.Pinned" />). Rows are cached per option / group and reused across rebuilds.
    ///     A header with the pin count minimizes / restores the list (tap anywhere on it); the state is persisted.
    /// </summary>
    internal sealed class PinnedOptionsView : VisualElement
    {
        internal const string MinimizedStateKey = "pinned/minimized";
        private const float PollInterval = 0.3f;
        private readonly Label _count;
        private readonly PinnedOptionsModel _model;
        private readonly OptionViewRegistry _optionViews;

        private readonly OptionRegistry _registry;
        private readonly ScrollView _scroll;
        private readonly PinnedSettings _settings;
        private readonly DebuggerState _state;
        private readonly Label _toggle;
        private readonly Dictionary<object, IOptionEntryView> _views = new();
        private bool _dirty = true;
        private float _poll;

        public PinnedOptionsView(OptionRegistry registry, OptionViewRegistry optionViews, PinnedSettings settings,
            DebuggerState state = null)
        {
            _registry = registry;
            _optionViews = optionViews;
            _settings = settings;
            _state = state;
            _model = new PinnedOptionsModel(registry);
            name = "atsu-pinned";
            AddToClassList("atsu-pinned");
            style.maxHeight = Length.Percent(Mathf.Clamp01(settings.MaxHeight) * 100f);
            style.opacity = Mathf.Clamp(settings.Opacity, 0.1f, 1f);

            var header = new VisualElement();
            header.AddToClassList("atsu-pinned__header");
            var title = new Label(DebuggerStrings.Get("pinned.title"));
            title.AddToClassList("atsu-pinned__title");
            _count = new Label();
            _count.AddToClassList("atsu-pinned__count");
            // The whole header is the toggle (a nested Button would also receive the click and toggle twice).
            _toggle = new Label();
            _toggle.AddToClassList("atsu-pinned__toggle");
            header.Add(title);
            header.Add(_count);
            header.Add(_toggle);
            header.AddManipulator(new Clickable(ToggleMinimized));
            Add(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("atsu-pinned__scroll");
            Add(_scroll);

            SetMinimized(state?.Get(MinimizedStateKey) == "1");

            registry.PinStateChanged += OnPinStateChanged;
            registry.GroupRemoved += OnGroupRemoved;
        }

        /// <summary>True if there is anything pinned (after the pending rebuild).</summary>
        public bool HasEntries
        {
            get
            {
                if (_dirty)
                {
                    Rebuild();
                }

                return _model.Entries.Count > 0;
            }
        }

        internal int EntryCount => _model.Entries.Count;

        /// <summary>Only the header is shown. Values are not polled while minimized.</summary>
        public bool IsMinimized { get; private set; }

        public void ToggleMinimized()
        {
            SetMinimized(!IsMinimized);
        }

        public void SetMinimized(bool minimized)
        {
            IsMinimized = minimized;
            EnableInClassList("atsu-pinned--minimized", minimized);
            _scroll.EnableInClassList(UssClasses.Hidden, minimized);
            // Minimized: shrink to the header instead of keeping the configured width.
            style.width = minimized ? new StyleLength(StyleKeyword.Auto) : _settings.Width;
            _toggle.text = minimized ? "+" : "−";
            tooltip = minimized ? "Expand pinned options" : "Minimize pinned options";
            _state?.Set(MinimizedStateKey, minimized ? "1" : null);
            _poll = 0f; // refresh values right away when restored
        }

        /// <param name="shown">Whether the overlay is currently on screen (values are only polled then).</param>
        public void Tick(float unscaledDeltaTime, bool shown)
        {
            if (_dirty)
            {
                Rebuild();
            }

            if (!shown || IsMinimized)
            {
                return;
            }

            _poll -= unscaledDeltaTime;
            if (_poll > 0f)
            {
                return;
            }

            _poll = PollInterval;
            foreach (var entry in _model.Entries)
            {
                if (_views.TryGetValue(Key(entry), out var view))
                {
                    view.Refresh();
                }
            }
        }

        public void Dispose()
        {
            _registry.PinStateChanged -= OnPinStateChanged;
            _registry.GroupRemoved -= OnGroupRemoved;
            foreach (var v in _views.Values)
            {
                DisposeView(v);
            }

            _views.Clear();
            _scroll.Clear();
        }

        private void OnPinStateChanged(OptionDefinition option, bool pinned)
        {
            _dirty = true;
        }

        private void OnGroupRemoved(OptionGroup group)
        {
            _dirty = true;
        }

        private void Rebuild()
        {
            _dirty = false;
            _model.Refresh();

            var alive = new HashSet<object>();
            _scroll.Clear();
            foreach (var entry in _model.Entries)
            {
                var key = Key(entry);
                alive.Add(key);
                if (!_views.TryGetValue(key, out var view))
                {
                    view = CreateView(entry);
                    _views.Add(key, view);
                }

                _scroll.Add(view.Element);
            }

            List<object> stale = null;
            foreach (var key in _views.Keys)
            {
                if (!alive.Contains(key))
                {
                    (stale ??= new List<object>()).Add(key);
                }
            }

            if (stale != null)
            {
                foreach (var key in stale)
                {
                    DisposeView(_views[key]);
                    _views.Remove(key);
                }
            }

            _poll = 0f; // refresh values right away
            _count.text = _model.Entries.Count.ToString();
        }

        private IOptionEntryView CreateView(OptionEntry entry)
        {
            try
            {
                return entry.IsGroup
                    ? new OptionGroupRow(entry.Group, _registry, _optionViews, OptionPlacement.Pinned)
                    : new OptionRow(entry.Option, null, _registry, _optionViews, false, OptionPlacement.Pinned);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new ErrorEntryView(DebugPage.CreateErrorElement($"Failed to draw pinned '{entry}'", e));
            }
        }

        private static void DisposeView(IOptionEntryView view)
        {
            try
            {
                view.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static object Key(OptionEntry entry)
        {
            return entry.IsGroup ? entry.Group : entry.Option;
        }

        private sealed class ErrorEntryView : IOptionEntryView
        {
            public ErrorEntryView(VisualElement element)
            {
                Element = element;
            }

            public VisualElement Element { get; }

            public void Refresh()
            {
            }

            public void UpdatePinState()
            {
            }

            public void ApplyHighlight(OptionTreeFilter filter)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}