using System;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>Ids of the built-in overlays (<see cref="IDebugService.Overlays" />).</summary>
    public static class DebugOverlayIds
    {
        public const string Trigger = "trigger";
        public const string Pinned = "pinned";
        public const string DockedConsole = "docked-console";
        public const string DockedProfiler = "docked-profiler";

        internal static OverlayAnchor ToAnchor(OverlayCorner corner)
        {
            return corner switch
            {
                OverlayCorner.TopLeft => OverlayAnchor.TopLeft,
                OverlayCorner.TopRight => OverlayAnchor.TopRight,
                OverlayCorner.BottomLeft => OverlayAnchor.BottomLeft,
                _ => OverlayAnchor.BottomRight
            };
        }
    }

    // ---- Trigger ------------------------------------------------------------------------------

    /// <summary>The corner hot spot that opens the panel (<see cref="TriggerSettings" />).</summary>
    public sealed class TriggerFeature : DebuggerFeature
    {
        private bool? _enabled;

        /// <summary>
        ///     Whether the trigger is on screen (while the panel is closed). Starts from
        ///     <see cref="TriggerSettings.Enabled" />.
        /// </summary>
        public bool IsEnabled
        {
            get => _enabled ?? Debugger?.Settings.Trigger.Enabled ?? true;
            set => _enabled = value;
        }

        protected override void OnInstall()
        {
            _enabled ??= Debugger.Settings.Trigger.Enabled;
            Debugger.Overlays.Add(new TriggerOverlay(this, Debugger.Settings.Trigger));
        }

        protected override void OnUninstall()
        {
            Debugger.Overlays.Remove(DebugOverlayIds.Trigger);
        }
    }

    /// <summary>Overlay of <see cref="TriggerFeature" />: always closest to its corner. Turns red when an error is logged.</summary>
    internal sealed class TriggerOverlay : DebugOverlay
    {
        private readonly TriggerFeature _feature;
        private readonly TriggerSettings _settings;
        private long _seenErrors;

        public TriggerOverlay(TriggerFeature feature, TriggerSettings settings)
            : base(DebugOverlayIds.Trigger, (OverlayAnchor)(int)settings.Position, -100)
        {
            _feature = feature;
            _settings = settings;
        }

        public override bool IsActive => _feature.IsEnabled;

        internal TriggerElement Element { get; private set; }

        protected override VisualElement CreateContent()
        {
            Element = new TriggerElement(() => Debugger.Panel.Show());
            Element.Configure(_settings.Behaviour, _settings.Size);
            return Element;
        }

        protected override void OnShow()
        {
            // Errors seen while the panel was open do not count.
            _seenErrors = Debugger.Console.GetTotalReceived(LogSeverity.Error);
            Element.SetErrorState(false);
        }

        protected override void OnUpdate(float unscaledDeltaTime)
        {
            if (_settings.ErrorNotification)
            {
                Element.SetErrorState(Debugger.Console.GetTotalReceived(LogSeverity.Error) > _seenErrors);
            }
        }
    }

    // ---- Pinned options -----------------------------------------------------------------------

    /// <summary>Pinned options drawn over the game while the panel is closed (<see cref="PinnedSettings" />).</summary>
    public sealed class PinnedOverlayFeature : DebuggerFeature
    {
        protected override void OnInstall()
        {
            Debugger.Overlays.Add(new PinnedOptionsOverlay(DebugOverlayIds.ToAnchor(Debugger.Settings.Pinned.Corner)));
        }

        protected override void OnUninstall()
        {
            Debugger.Overlays.Remove(DebugOverlayIds.Pinned);
        }
    }

    internal sealed class PinnedOptionsOverlay : DebugOverlay
    {
        public PinnedOptionsOverlay(OverlayAnchor anchor) : base(DebugOverlayIds.Pinned, anchor, 20)
        {
        }

        internal PinnedOptionsView View { get; private set; }

        public override bool IsActive => View?.HasEntries ?? Debugger.Options.PinnedOptions.Count > 0;

        protected override VisualElement CreateContent()
        {
            View = new PinnedOptionsView(Debugger.Options, Debugger.OptionViews, Debugger.Settings.Pinned,
                Debugger.State);
            return View;
        }

        protected override void OnUpdate(float unscaledDeltaTime)
        {
            View.Tick(unscaledDeltaTime, true);
        }

        protected override void OnDispose()
        {
            View?.Dispose();
        }
    }

    // ---- Docked console -----------------------------------------------------------------------

    /// <summary>
    ///     The console docked to the top / bottom edge of the game screen (<see cref="DockedConsoleSettings" />). Its state is
    ///     persisted; change it at run time through this feature (<c>Debugger.GetFeature&lt;DockedConsoleFeature&gt;()</c>).
    /// </summary>
    public sealed class DockedConsoleFeature : DebuggerFeature, IDockedConsole
    {
        private DockedConsoleOverlay _overlay;
        private DockedConsoleState _state;

        private DockedConsoleState State =>
            _state ?? throw new InvalidOperationException("DockedConsoleFeature is not installed yet.");

        public bool IsVisible
        {
            get => _state?.IsVisible ?? false;
            set => State.IsVisible = value;
        }

        public bool IsExpanded
        {
            get => _state?.IsExpanded ?? false;
            set => State.IsExpanded = value;
        }

        public ConsoleAlignment Alignment
        {
            get => _state?.Alignment ?? ConsoleAlignment.Bottom;
            set => State.Alignment = value;
        }

        public event Action Changed;

        protected override void OnInstall()
        {
            _state = new DockedConsoleState(Debugger.State, Debugger.Settings.DockedConsole);
            _state.Changed += OnChanged;
            _overlay = new DockedConsoleOverlay(this);
            Debugger.Overlays.Add(_overlay);
        }

        protected override void OnUninstall()
        {
            Debugger.Overlays.Remove(DebugOverlayIds.DockedConsole);
            if (_state != null)
            {
                _state.Changed -= OnChanged;
            }

            _state = null;
            _overlay = null;
        }

        private void OnChanged()
        {
            _overlay?.UpdateAnchor();
            Changed?.Invoke();
        }
    }

    internal sealed class DockedConsoleOverlay : DebugOverlay
    {
        private readonly DockedConsoleFeature _feature;

        public DockedConsoleOverlay(DockedConsoleFeature feature)
            : base(DebugOverlayIds.DockedConsole, AnchorOf(feature.Alignment))
        {
            _feature = feature;
        }

        internal DockedConsoleView View { get; private set; }

        public override bool IsActive => _feature.IsVisible;

        internal void UpdateAnchor()
        {
            Anchor = AnchorOf(_feature.Alignment);
        }

        private static OverlayAnchor AnchorOf(ConsoleAlignment alignment)
        {
            return alignment == ConsoleAlignment.Top ? OverlayAnchor.TopEdge : OverlayAnchor.BottomEdge;
        }

        protected override VisualElement CreateContent()
        {
            View = new DockedConsoleView(Debugger.Console, _feature, Debugger.Settings.DockedConsole,
                () => Debugger.Panel.Show(DebugTabIds.Console));
            return View;
        }

        protected override void OnUpdate(float unscaledDeltaTime)
        {
            View.Tick(true);
        }

        protected override void OnDispose()
        {
            View?.Dispose();
        }
    }

    // ---- Docked profiler ----------------------------------------------------------------------

    /// <summary>
    ///     FPS / frame time / memory in a screen corner while the panel is closed (<see cref="ProfilerSettings" />).
    ///     <see cref="IsDocked" /> is persisted.
    /// </summary>
    public sealed class DockedProfilerFeature : DebuggerFeature
    {
        internal const string StateKey = "profiler/docked";
        private bool _docked;

        /// <summary>Whether the docked profiler is shown (while the panel is closed).</summary>
        public bool IsDocked
        {
            get => _docked;
            set
            {
                if (_docked == value)
                {
                    return;
                }

                _docked = value;
                if (Debugger != null)
                {
                    Debugger.State.Set(StateKey, value ? "1" : "0");
                    Debugger.State.Save();
                }

                Changed?.Invoke();
            }
        }

        public event Action Changed;

        protected override void OnInstall()
        {
            var stored = Debugger.State.Get(StateKey);
            _docked = stored == null ? Debugger.Settings.Profiler.DockedAtStart : stored == "1";
            Debugger.Overlays.Add(new DockedProfilerOverlay(this));
        }

        protected override void OnUninstall()
        {
            Debugger.Overlays.Remove(DebugOverlayIds.DockedProfiler);
        }
    }

    internal sealed class DockedProfilerOverlay : DebugOverlay
    {
        private readonly DockedProfilerFeature _feature;

        public DockedProfilerOverlay(DockedProfilerFeature feature)
            : base(DebugOverlayIds.DockedProfiler,
                DebugOverlayIds.ToAnchor(feature.Debugger.Settings.Profiler.DockedCorner), 10)
        {
            _feature = feature;
        }

        internal DockedProfilerView View { get; private set; }

        public override bool IsActive => _feature.IsDocked;

        protected override VisualElement CreateContent()
        {
            View = new DockedProfilerView(Debugger.Profiler, Debugger.Settings.Profiler,
                () => Debugger.Panel.Show(DebugTabIds.Profiler));
            return View;
        }

        protected override void OnUpdate(float unscaledDeltaTime)
        {
            View.Tick(unscaledDeltaTime, true);
        }

        protected override void OnHide()
        {
            View.Tick(0f, false); // stops recording
        }

        protected override void OnDispose()
        {
            View?.Dispose();
        }
    }
}