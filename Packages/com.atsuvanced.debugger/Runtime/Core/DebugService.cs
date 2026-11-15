using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     The core: data sources, page / overlay registries and the panel state. Holds no policy of its own; tabs, overlays
    ///     and
    ///     shortcuts come from <see cref="DebuggerFeature" />s installed by <see cref="DebuggerBuilder" />.
    /// </summary>
    internal sealed class DebugService : IDebugService, IDisposable
    {
        private const float DestroyedContainerSweepInterval = 1f;

        private readonly bool _captureLogs;
        private readonly List<DebuggerFeature> _features = new();
        private readonly PinPersistence _pins;
        private readonly ValuePersistence _values;
        private DebuggerRunner _runner;
        private float _sweepTimer;

        public DebugService(DebuggerSettings settings, bool captureLogs, IDebuggerStorage storage = null)
        {
            Settings = settings ?? new DebuggerSettings();
            _captureLogs = captureLogs;
            State = new DebuggerState(Settings.General.PersistState
                ? storage ?? new PlayerPrefsDebuggerStorage()
                : new MemoryDebuggerStorage());

            Options = new OptionRegistry();
            OptionViews = new OptionViewRegistry(Options);
            Console = new ConsoleLogBuffer(Mathf.Max(1, Settings.Console.MaximumEntries));
            Profiler = new DebugProfiler(Settings.Profiler.HistoryFrames);
            Profiler.AddDefaultCounters();

            Navigator = new PageNavigator(Tabs, this, () => PanelState.Hide());
            PanelState = new DebugPanel(Settings, State, Tabs, Navigator);
            PanelState.Opening += () => Options.RemoveDestroyedContainers();
            Overlays = new DebugOverlayRegistry(this);
            BugReports = new BugReportService(Settings.BugReport, PanelState, Tabs);

            _pins = new PinPersistence(Options, State);
            _values = new ValuePersistence(Options, State);

            if (_captureLogs)
            {
                Application.logMessageReceivedThreaded += OnLogMessage;
            }
        }

        internal PageNavigator Navigator { get; }
        internal DebugPanel PanelState { get; }

        /// <summary>The runner's transform (parent for view objects), or null outside play mode.</summary>
        internal Transform RunnerTransform => _runner ? _runner.transform : null;

        public DebuggerSettings Settings { get; }
        public DebuggerState State { get; }
        public OptionRegistry Options { get; }
        public OptionViewRegistry OptionViews { get; }
        public ConsoleLogBuffer Console { get; }
        public SystemInfoRegistry SystemInfo { get; } = new();
        public DebugProfiler Profiler { get; }
        public IDebugPanel Panel => PanelState;
        public DebugTabRegistry Tabs { get; } = new();
        public IPageNavigator Pages => Navigator;
        public DebugOverlayRegistry Overlays { get; }
        public BugReportService BugReports { get; }
        public IReadOnlyList<DebuggerFeature> Features => _features;

        public T GetFeature<T>() where T : DebuggerFeature
        {
            foreach (var f in _features)
            {
                if (f is T t)
                {
                    return t;
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (_captureLogs)
            {
                Application.logMessageReceivedThreaded -= OnLogMessage;
            }

            PanelState.Destroy();
            for (var i = _features.Count - 1; i >= 0; i--)
            {
                _features[i].InternalUninstall();
            }

            _features.Clear();
            Overlays.Clear();
            _pins.Dispose();
            _values.Dispose();
            StopRunner();
            Profiler.Dispose();
            State.Save();
        }

        /// <summary>Installs features in order (called once by the builder).</summary>
        internal void Install(IEnumerable<DebuggerFeature> features)
        {
            foreach (var feature in features)
            {
                if (feature == null)
                {
                    continue;
                }

                _features.Add(feature);
                feature.InternalInstall(this);
            }
        }

        /// <summary>Attaches the rendering view (UI Toolkit in play mode).</summary>
        internal void SetView(IDebugPanelView view)
        {
            PanelState.SetView(view);
        }

        /// <summary>
        ///     Creates the GameObject that drives <see cref="Tick" /> every frame (play mode only). It runs whether or not the
        ///     panel has ever been shown, so sampling and housekeeping never depend on the view existing.
        /// </summary>
        internal void StartRunner()
        {
            if (_runner || !Application.isPlaying)
            {
                return;
            }

            var host = new GameObject("[AtsuvancedDebugger]");
            Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<DebuggerRunner>();
            _runner.Service = this;
        }

        private void StopRunner()
        {
            if (!_runner)
            {
                return;
            }

            _runner.Service = null;
            Object.Destroy(_runner.gameObject);
            _runner = null;
        }

        /// <summary>Per-frame update, driven by <see cref="DebuggerRunner" /> (tests call it directly).</summary>
        internal void Tick(float unscaledDeltaTime)
        {
            Profiler.Sample(unscaledDeltaTime);
            _sweepTimer -= unscaledDeltaTime;
            if (_sweepTimer <= 0f)
            {
                _sweepTimer = DestroyedContainerSweepInterval;
                Options.RemoveDestroyedContainers();
            }

            for (var i = 0; i < _features.Count; i++)
            {
                _features[i].InternalUpdate(unscaledDeltaTime);
            }

            Overlays.Update(PanelState.IsVisible, PanelState.IsEntryCodeShown, unscaledDeltaTime);
            PanelState.View.Tick(unscaledDeltaTime);
            Navigator.Update(unscaledDeltaTime);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            Console.Add(type, condition, stackTrace);
        }
    }
}