using System;
using System.Collections.Generic;
using Atsuvanced.Debugger;
using Atsuvanced.Debugger.UI;
using UnityEngine;
using CoreOptionDefinition = Atsuvanced.Debugger.OptionDefinition;
using SROptionDefinition = SRDebugger.OptionDefinition;

namespace SRDebugger.Services
{
    /// <summary>SRDebugger compatible docked console.</summary>
    public interface IDockConsoleService
    {
        bool IsVisible { get; set; }
        bool IsExpanded { get; set; }
        ConsoleAlignment Alignment { get; set; }
    }

    /// <summary>SRDebugger compatible service interface (subset; see Documentation~/Migration.md).</summary>
    public interface IDebugService
    {
        IDockConsoleService DockConsole { get; }

        /// <summary>Shows FPS / frame time / memory on the game screen while the panel is closed.</summary>
        bool IsProfilerDocked { get; set; }

        bool IsDebugPanelVisible { get; }
        bool IsTriggerEnabled { get; set; }

        event VisibilityChangedDelegate PanelVisibilityChanged;

        void ShowDebugPanel(bool requireEntryCode = true);
        void ShowDebugPanel(DefaultTabs tab, bool requireEntryCode = true);
        void HideDebugPanel();
        void DestroyDebugPanel();

        void AddSystemInfo(InfoEntry entry, string category = "Default");

        void AddOptionContainer(object container);
        void RemoveOptionContainer(object container);
        void AddOption(SROptionDefinition option);
        bool RemoveOption(SROptionDefinition option);

        void PinAllOptions(string category);
        void UnpinAllOptions(string category);
        void PinOption(string name);
        void UnpinOption(string name);
        void ClearPinnedOptions();

        void ShowBugReportSheet(ActionCompleteCallback onComplete = null, bool takeScreenshot = true,
            string descriptionContent = null);

        /// <summary>Sends bug reports through <paramref name="handler" /> (null = save locally).</summary>
        void SetBugReporterHandler(IBugReporterHandler handler);

        void SetEntryCode(EntryCode newCode);
        void DisableEntryCode();

        /// <summary>Not supported (uGUI only). Never raised.</summary>
        [Obsolete(
            "Atsuvanced Debugger renders with UI Toolkit; there is no pinned uGUI canvas. This event is never raised.")]
        event Action<RectTransform> PinnedUiCanvasCreated;

        /// <summary>Not supported (uGUI only). Logs a warning and returns null.</summary>
        [Obsolete("World space mode is not supported by Atsuvanced Debugger (UI Toolkit). Returns null.")]
        RectTransform EnableWorldSpaceMode();
    }

    internal sealed class SRDebugServiceAdapter : IDebugService
    {
        // SR container -> core container adapter, so RemoveOptionContainer can find the registered instance.
        private readonly Dictionary<IOptionContainer, ContainerAdapter> _containerAdapters = new();

        private readonly Atsuvanced.Debugger.IDebugService _core;

        private readonly Dictionary<VisibilityChangedDelegate, Action<bool>> _visibilityHandlers = new();

        private DockConsoleAdapter _dockConsole;

        public SRDebugServiceAdapter(Atsuvanced.Debugger.IDebugService core)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
        }

        public bool IsDebugPanelVisible => _core.Panel.IsVisible;
        public IDockConsoleService DockConsole => _dockConsole ??= new DockConsoleAdapter(_core);

        /// <summary>Needs <see cref="DockedProfilerFeature" /> (false / ignored without it).</summary>
        public bool IsProfilerDocked
        {
            get => _core.GetFeature<DockedProfilerFeature>()?.IsDocked ?? false;
            set
            {
                var f = _core.GetFeature<DockedProfilerFeature>();
                if (f != null)
                {
                    f.IsDocked = value;
                }
            }
        }

        public void SetBugReporterHandler(IBugReporterHandler handler)
        {
            _core.BugReports.Reporter = handler == null ? null : new BugReporterHandlerAdapter(handler);
        }

        public void SetEntryCode(EntryCode newCode)
        {
            _core.Panel.SetEntryCode(newCode.ToString());
        }

        public void DisableEntryCode()
        {
            _core.Panel.DisableEntryCode();
        }

        /// <summary>Needs <see cref="TriggerFeature" /> (false / ignored without it).</summary>
        public bool IsTriggerEnabled
        {
            get => _core.GetFeature<TriggerFeature>()?.IsEnabled ?? false;
            set
            {
                var f = _core.GetFeature<TriggerFeature>();
                if (f != null)
                {
                    f.IsEnabled = value;
                }
            }
        }

        public event VisibilityChangedDelegate PanelVisibilityChanged
        {
            add
            {
                if (value == null || _visibilityHandlers.ContainsKey(value))
                {
                    return;
                }

                Action<bool> h = v => value(v);
                _visibilityHandlers.Add(value, h);
                _core.Panel.VisibilityChanged += h;
            }
            remove
            {
                if (value == null || !_visibilityHandlers.TryGetValue(value, out var h))
                {
                    return;
                }

                _visibilityHandlers.Remove(value);
                _core.Panel.VisibilityChanged -= h;
            }
        }

        public void ShowDebugPanel(bool requireEntryCode = true)
        {
            _core.Panel.Show(null, requireEntryCode);
        }

        public void ShowDebugPanel(DefaultTabs tab, bool requireEntryCode = true)
        {
            _core.Panel.Show(TabId(tab), requireEntryCode);
        }

        public void HideDebugPanel()
        {
            _core.Panel.Hide();
        }

        public void DestroyDebugPanel()
        {
            _core.Panel.Destroy();
        }

        public void AddSystemInfo(InfoEntry entry, string category = "Default")
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _core.AddSystemInfo(entry.Core, category);
        }

        public void AddOptionContainer(object container)
        {
            if (container is IOptionContainer sr)
            {
                if (_containerAdapters.ContainsKey(sr))
                {
                    return;
                }

                var adapter = new ContainerAdapter(sr);
                _containerAdapters.Add(sr, adapter);
                _core.AddOptionContainer(adapter);
            }
            else
            {
                _core.AddOptionContainer(container);
            }
        }

        public void RemoveOptionContainer(object container)
        {
            if (container is IOptionContainer sr)
            {
                if (!_containerAdapters.TryGetValue(sr, out var adapter))
                {
                    return;
                }

                _containerAdapters.Remove(sr);
                adapter.Detach();
                _core.RemoveOptionContainer(adapter);
            }
            else
            {
                _core.RemoveOptionContainer(container);
            }
        }

        public void AddOption(SROptionDefinition option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            _core.AddOption(option.Core);
        }

        public bool RemoveOption(SROptionDefinition option)
        {
            return option != null && _core.RemoveOption(option.Core);
        }

        public void PinAllOptions(string category)
        {
            _core.PinAllOptions(category);
        }

        public void UnpinAllOptions(string category)
        {
            _core.UnpinAllOptions(category);
        }

        public void PinOption(string name)
        {
            _core.PinOption(name);
        }

        public void UnpinOption(string name)
        {
            _core.UnpinOption(name);
        }

        public void ClearPinnedOptions()
        {
            _core.ClearPinnedOptions();
        }

        public void ShowBugReportSheet(ActionCompleteCallback onComplete = null, bool takeScreenshot = true,
            string descriptionContent = null)
        {
            _core.BugReports.ShowSheet(onComplete == null ? null : new Action<bool>(s => onComplete(s)),
                takeScreenshot, descriptionContent);
        }

        /// <summary>SRDebugger's tabs. "Options" is the single options tab, or the first option category tab.</summary>
        private string TabId(DefaultTabs tab)
        {
            switch (tab)
            {
                case DefaultTabs.Console: return DebugTabIds.Console;
                case DefaultTabs.Profiler: return DebugTabIds.Profiler;
                case DefaultTabs.BugReporter: return DebugTabIds.BugReporter;
                case DefaultTabs.Options:
                    if (_core.Tabs.Find(DebugTabIds.Options) != null)
                    {
                        return DebugTabIds.Options;
                    }

                    foreach (var t in _core.Tabs.Tabs)
                    {
                        if (t.Id.StartsWith(DebugTabIds.OptionCategoryPrefix))
                        {
                            return t.Id;
                        }
                    }

                    return DebugTabIds.SystemInformation;
                default: return DebugTabIds.SystemInformation;
            }
        }

        /// <summary>Forwards to <see cref="DockedConsoleFeature" /> (false / ignored when it is not installed).</summary>
        private sealed class DockConsoleAdapter : IDockConsoleService
        {
            private readonly Atsuvanced.Debugger.IDebugService _service;

            public DockConsoleAdapter(Atsuvanced.Debugger.IDebugService service)
            {
                _service = service;
            }

            private IDockedConsole Core => _service.GetFeature<DockedConsoleFeature>();

            public bool IsVisible
            {
                get => Core?.IsVisible ?? false;
                set
                {
                    if (Core != null)
                    {
                        Core.IsVisible = value;
                    }
                }
            }

            public bool IsExpanded
            {
                get => Core?.IsExpanded ?? false;
                set
                {
                    if (Core != null)
                    {
                        Core.IsExpanded = value;
                    }
                }
            }

            public ConsoleAlignment Alignment
            {
                get => (ConsoleAlignment)(int)(Core?.Alignment ?? Atsuvanced.Debugger.ConsoleAlignment.Bottom);
                set
                {
                    if (Core != null)
                    {
                        Core.Alignment = (Atsuvanced.Debugger.ConsoleAlignment)(int)value;
                    }
                }
            }
        }

        /// <summary>Exposes an SRDebugger.IOptionContainer as a core IOptionContainer.</summary>
        private sealed class ContainerAdapter : Atsuvanced.Debugger.IOptionContainer
        {
            private readonly IOptionContainer _source;

            public ContainerAdapter(IOptionContainer source)
            {
                _source = source;
                if (_source.IsDynamic)
                {
                    _source.OptionAdded += OnAdded;
                    _source.OptionRemoved += OnRemoved;
                }
            }

            public bool IsDynamic => _source.IsDynamic;
            public event Action<CoreOptionDefinition> OptionAdded;
            public event Action<CoreOptionDefinition> OptionRemoved;

            public IEnumerable<CoreOptionDefinition> GetOptions()
            {
                foreach (var o in _source.GetOptions())
                {
                    yield return o.Core;
                }
            }

            public void Detach()
            {
                if (!_source.IsDynamic)
                {
                    return;
                }

                _source.OptionAdded -= OnAdded;
                _source.OptionRemoved -= OnRemoved;
            }

            private void OnAdded(SROptionDefinition o)
            {
                OptionAdded?.Invoke(o.Core);
            }

            private void OnRemoved(SROptionDefinition o)
            {
                OptionRemoved?.Invoke(o.Core);
            }
        }

#pragma warning disable 67, 618
        [Obsolete(
            "Atsuvanced Debugger renders with UI Toolkit; there is no pinned uGUI canvas. This event is never raised.")]
        public event Action<RectTransform> PinnedUiCanvasCreated;

        [Obsolete("World space mode is not supported by Atsuvanced Debugger (UI Toolkit). Returns null.")]
        public RectTransform EnableWorldSpaceMode()
        {
            Debug.LogWarning("[AtsuvancedDebugger] EnableWorldSpaceMode is not supported (UI Toolkit). Ignored.");
            return null;
        }
#pragma warning restore 67, 618
    }
}