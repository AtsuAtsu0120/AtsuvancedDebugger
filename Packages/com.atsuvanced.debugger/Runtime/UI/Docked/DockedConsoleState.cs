using System;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>UI-independent docked console state, persisted in <see cref="DebuggerState" />.</summary>
    internal sealed class DockedConsoleState : IDockedConsole
    {
        internal const string VisibleKey = "dock/visible";
        internal const string ExpandedKey = "dock/expanded";
        internal const string AlignmentKey = "dock/alignment";

        private readonly DebuggerState _state;
        private ConsoleAlignment _alignment;
        private bool _expanded;
        private bool _visible;

        public DockedConsoleState(DebuggerState state, DockedConsoleSettings settings)
        {
            _state = state;
            _visible = Read(VisibleKey, settings.VisibleAtStart);
            _expanded = Read(ExpandedKey, settings.Expanded);
            var align = state?.Get(AlignmentKey);
            _alignment = align == nameof(ConsoleAlignment.Top) ? ConsoleAlignment.Top
                : align == nameof(ConsoleAlignment.Bottom) ? ConsoleAlignment.Bottom
                : settings.Alignment;
        }

        public event Action Changed;

        public bool IsVisible
        {
            get => _visible;
            set => Set(ref _visible, value, VisibleKey, value ? "1" : "0");
        }

        public bool IsExpanded
        {
            get => _expanded;
            set => Set(ref _expanded, value, ExpandedKey, value ? "1" : "0");
        }

        public ConsoleAlignment Alignment
        {
            get => _alignment;
            set => Set(ref _alignment, value, AlignmentKey, value.ToString());
        }

        private bool Read(string key, bool fallback)
        {
            var v = _state?.Get(key);
            return v == null ? fallback : v == "1";
        }

        private void Set<T>(ref T field, T value, string key, string stored)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            _state?.Set(key, stored);
            _state?.Save(); // toggled rarely, usually while the panel is closed
            Changed?.Invoke();
        }
    }
}