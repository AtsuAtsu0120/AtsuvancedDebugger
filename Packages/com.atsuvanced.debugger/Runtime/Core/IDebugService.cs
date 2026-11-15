using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     A debugger: data sources, the panel and its extension points. Everything the built-in features use is here, so
    ///     custom features, pages and overlays have the same reach. Shortcuts for common calls are in
    ///     <see cref="DebugServiceExtensions" /> (<c>ShowDebugPanel</c>, <c>AddOptionContainer</c> ...).
    /// </summary>
    public interface IDebugService
    {
        /// <summary>The settings the debugger was built with (a copy; changing them later has no effect).</summary>
        DebuggerSettings Settings { get; }

        /// <summary>
        ///     Persisted key-value state of the debugger and its pages (pins, selected tab / section, ...). Custom pages can
        ///     store their own state here; prefix the keys (see <see cref="DebuggerState" />).
        /// </summary>
        DebuggerState State { get; }

        // ---- Data sources ---------------------------------------------------------------

        OptionRegistry Options { get; }

        /// <summary>Custom drawing of options (per type, UXML templates).</summary>
        OptionViewRegistry OptionViews { get; }

        ConsoleLogBuffer Console { get; }
        SystemInfoRegistry SystemInfo { get; }

        /// <summary>Frame timing and counters. Add your own counters with <c>AddCounter</c>.</summary>
        DebugProfiler Profiler { get; }

        // ---- Panel ----------------------------------------------------------------------

        IDebugPanel Panel { get; }

        /// <summary>Tabs of the panel. Add a <see cref="DebugTabDefinition" /> to create a tab, or sections to any tab.</summary>
        DebugTabRegistry Tabs { get; }

        IPageNavigator Pages { get; }

        /// <summary>UI drawn over the game screen (trigger, pinned options, docked console ... and your own).</summary>
        DebugOverlayRegistry Overlays { get; }

        BugReportService BugReports { get; }

        // ---- Features -------------------------------------------------------------------

        /// <summary>The features the debugger was built with, in installation order.</summary>
        IReadOnlyList<DebuggerFeature> Features { get; }

        /// <summary>The installed feature of type <typeparamref name="T" /> (or a subclass), or null.</summary>
        T GetFeature<T>() where T : DebuggerFeature;
    }
}