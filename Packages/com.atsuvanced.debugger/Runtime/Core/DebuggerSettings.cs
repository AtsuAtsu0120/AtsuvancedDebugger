using System;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    public enum TriggerPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    public enum TriggerBehaviour
    {
        TripleTap,
        DoubleTap,
        Tap,
        LongPress
    }

    /// <summary>Screen corner for overlays such as the pinned options.</summary>
    public enum OverlayCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>Color theme of the built-in UI.</summary>
    public enum DebuggerTheme
    {
        Dark,
        Light
    }

    [Serializable]
    public struct KeyboardShortcut
    {
        public KeyCode Key;
        public bool Control;
        public bool Shift;
        public bool Alt;

        public override string ToString()
        {
            return (Control ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") + Key;
        }
    }

    /// <summary>
    ///     Start-up settings, grouped by the feature that reads them. Normally edited on <see cref="DebuggerSettingsAsset" />
    ///     (loaded via Preloaded Assets, no Resources folder) and changed from code with
    ///     <c>DebuggerBuilder.ConfigureSettings</c>.
    ///     <para>
    ///         Every value is read when the debugger (or the feature using it) initializes; changing it afterwards has no
    ///         effect.
    ///         State that changes at run time has its own API (e.g. <c>TriggerFeature.IsEnabled</c>,
    ///         <c>DockedConsoleFeature.IsVisible</c>, <see cref="IDebugPanel.SetEntryCode" />).
    ///     </para>
    /// </summary>
    [Serializable]
    public sealed class DebuggerSettings
    {
        public GeneralSettings General = new();
        public TriggerSettings Trigger = new();
        public EntryCodeSettings EntryCode = new();
        public ConsoleSettings Console = new();
        public DockedConsoleSettings DockedConsole = new();
        public PinnedSettings Pinned = new();
        public ProfilerSettings Profiler = new();
        public BugReportSettings BugReport = new();
        public KeyboardSettings Keyboard = new();

        /// <summary>A deep copy (the debugger works on a copy, so the asset is never modified at run time).</summary>
        public DebuggerSettings Clone()
        {
            return new DebuggerSettings
            {
                General = General.Clone(),
                Trigger = Trigger.Clone(),
                EntryCode = EntryCode.Clone(),
                Console = Console.Clone(),
                DockedConsole = DockedConsole.Clone(),
                Pinned = Pinned.Clone(),
                Profiler = Profiler.Clone(),
                BugReport = BugReport.Clone(),
                Keyboard = Keyboard.Clone()
            };
        }
    }

    /// <summary>The panel itself.</summary>
    [Serializable]
    public sealed class GeneralSettings
    {
        [Tooltip("Tab shown the first time the panel opens (afterwards the last tab is restored). See DebugTabIds.")]
        public string DefaultTab = DebugTabIds.SystemInformation;

        [Tooltip("Sorting order of the debugger's UI Toolkit panel.")]
        public int SortingOrder = 30000;

        [Tooltip("Set Time.timeScale to 0 while the panel is open (restored when it closes). Needs PauseTimeFeature.")]
        public bool PauseTimeWhenOpen;

        [Tooltip(
            "Language of the built-in UI. Auto = Japanese when the system language is Japanese, otherwise English.")]
        public DebuggerLanguage Language = DebuggerLanguage.Auto;

        [Tooltip("Color theme of the built-in UI (custom style sheets on the settings asset are applied on top).")]
        public DebuggerTheme Theme = DebuggerTheme.Dark;

        [Tooltip("Multiplier applied on top of the DPI-based scaling.")] [Range(0.5f, 3f)]
        public float UiScale = 1f;

        [Tooltip(
            "Remember pinned options, the selected tab / section, open categories and the last search across launches " +
            "(PlayerPrefs by default, replaceable with DebuggerBuilder.UseStorage).")]
        public bool PersistState = true;

        public GeneralSettings Clone()
        {
            return (GeneralSettings)MemberwiseClone();
        }
    }

    /// <summary>The corner hot spot that opens the panel (<c>TriggerFeature</c>).</summary>
    [Serializable]
    public sealed class TriggerSettings
    {
        [Tooltip("Show the trigger at start-up.")]
        public bool Enabled = true;

        public TriggerPosition Position = TriggerPosition.TopLeft;
        public TriggerBehaviour Behaviour = TriggerBehaviour.TripleTap;

        [Tooltip("Size of the trigger area in UI points.")] [Min(16)]
        public float Size = 48f;

        [Tooltip("Tint the trigger red when an error is logged while the panel is closed.")]
        public bool ErrorNotification = true;

        public TriggerSettings Clone()
        {
            return (TriggerSettings)MemberwiseClone();
        }
    }

    /// <summary>The PIN asked before the panel opens.</summary>
    [Serializable]
    public sealed class EntryCodeSettings
    {
        [Tooltip("Ask for a 4-digit PIN before the panel opens (SRDebugger's entry code).")]
        public bool Required;

        [Tooltip("The 4-digit PIN.")] public string Code = "1234";

        [Tooltip("Ask for the PIN every time the panel opens (otherwise only once per session).")]
        public bool EveryTime;

        public EntryCodeSettings Clone()
        {
            return (EntryCodeSettings)MemberwiseClone();
        }
    }

    /// <summary>The log buffer (shared by the Console tab, the docked console and bug reports).</summary>
    [Serializable]
    public sealed class ConsoleSettings
    {
        [Min(1)] public int MaximumEntries = 2000;

        public ConsoleSettings Clone()
        {
            return (ConsoleSettings)MemberwiseClone();
        }
    }

    /// <summary>The console docked to the top / bottom of the game screen (<c>DockedConsoleFeature</c>).</summary>
    [Serializable]
    public sealed class DockedConsoleSettings
    {
        [Tooltip(
            "Show the docked console on the first launch (afterwards the last state is restored when Persist State is on).")]
        public bool VisibleAtStart;

        [Tooltip("Initial edge of the docked console.")]
        public ConsoleAlignment Alignment = ConsoleAlignment.Bottom;

        [Tooltip("Start expanded (log lines listed) instead of a single bar.")]
        public bool Expanded = true;

        [Tooltip("Number of log lines shown when expanded (this also decides its height).")] [Range(1, 30)]
        public int Lines = 6;

        [Tooltip("Opacity of the docked console.")] [Range(0.1f, 1f)]
        public float Opacity = 0.9f;

        public DockedConsoleSettings Clone()
        {
            return (DockedConsoleSettings)MemberwiseClone();
        }
    }

    /// <summary>Pinned options drawn over the game (<c>PinnedOverlayFeature</c>).</summary>
    [Serializable]
    public sealed class PinnedSettings
    {
        [Tooltip("Screen corner of the pinned options shown over the game while the panel is closed.")]
        public OverlayCorner Corner = OverlayCorner.BottomLeft;

        [Tooltip("Width of the pinned options in UI points.")] [Range(160f, 800f)]
        public float Width = 320f;

        [Tooltip("Maximum height of the pinned options as a fraction of the screen height (scrolls beyond this).")]
        [Range(0.1f, 1f)]
        public float MaxHeight = 0.5f;

        [Tooltip("Opacity of the pinned options.")] [Range(0.1f, 1f)]
        public float Opacity = 0.9f;

        public PinnedSettings Clone()
        {
            return (PinnedSettings)MemberwiseClone();
        }
    }

    /// <summary>Frame timing (<see cref="DebugProfiler" />) and the docked profiler (<c>DockedProfilerFeature</c>).</summary>
    [Serializable]
    public sealed class ProfilerSettings
    {
        [Tooltip("Number of frames kept for the frame time graph.")] [Range(30, 1000)]
        public int HistoryFrames = 240;

        [Tooltip(
            "Show the docked profiler (FPS / frame time / memory) on the first launch (afterwards the last state is restored when Persist State is on).")]
        public bool DockedAtStart;

        [Tooltip("Screen corner of the docked profiler.")]
        public OverlayCorner DockedCorner = OverlayCorner.TopRight;

        [Tooltip("Opacity of the docked profiler.")] [Range(0.1f, 1f)]
        public float DockedOpacity = 0.85f;

        public ProfilerSettings Clone()
        {
            return (ProfilerSettings)MemberwiseClone();
        }
    }

    /// <summary>Bug reports (<see cref="BugReportService" />).</summary>
    [Serializable]
    public sealed class BugReportSettings
    {
        [Tooltip("Maximum number of console entries attached to a bug report (newest first).")] [Min(0)]
        public int MaxLogEntries = 500;

        [Tooltip("Folder under Application.persistentDataPath where the default reporter saves reports.")]
        public string Folder = "AtsuvancedDebugger/BugReports";

        public BugReportSettings Clone()
        {
            return (BugReportSettings)MemberwiseClone();
        }
    }

    /// <summary>Keyboard / gamepad shortcuts (<c>KeyboardShortcutsFeature</c>).</summary>
    [Serializable]
    public sealed class KeyboardSettings
    {
        public bool Enabled = true;
        public KeyboardShortcut Toggle = new() { Key = KeyCode.F1 };

        [Tooltip("Opens the Search tab and focuses the search field (opens the panel if needed).")]
        public KeyboardShortcut Search = new() { Key = KeyCode.F, Control = true };

        [Tooltip("Selects the previous tab while the panel is open (gamepad: left shoulder).")]
        public KeyboardShortcut PreviousTab = new() { Key = KeyCode.PageUp };

        [Tooltip("Selects the next tab while the panel is open (gamepad: right shoulder).")]
        public KeyboardShortcut NextTab = new() { Key = KeyCode.PageDown };

        public KeyboardSettings Clone()
        {
            return (KeyboardSettings)MemberwiseClone();
        }
    }
}