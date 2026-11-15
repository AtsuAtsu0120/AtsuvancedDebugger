using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>The built-in features in their default order (what <see cref="DebuggerBuilder.CreateDefault" /> installs).</summary>
    public static class BuiltInFeatures
    {
        public static IEnumerable<DebuggerFeature> Create()
        {
            yield return new SystemInfoFeature();
            yield return new OptionsFeature();
            yield return new ConsoleFeature();
            yield return new ProfilerFeature();
            yield return new BugReportFeature();
            yield return new TriggerFeature();
            yield return new PinnedOverlayFeature();
            yield return new DockedConsoleFeature();
            yield return new DockedProfilerFeature();
            yield return new KeyboardShortcutsFeature();
            yield return new PauseTimeFeature();
        }
    }

    /// <summary>Builds the page of an option section.</summary>
    /// <param name="categoryPath">Category shown by the page (e.g. "Player/Status").</param>
    /// <param name="directOptionsOnly">Only the options placed directly in the category (the "General" section).</param>
    /// <param name="title">Section title.</param>
    public delegate DebugPage OptionSectionPageFactory(string categoryPath, bool directOptionsOnly, string title);

    /// <summary>How option categories are laid out as tabs.</summary>
    public enum OptionsLayout
    {
        /// <summary>Each top-level category is a tab; level-2 categories are its sections (default).</summary>
        CategoryTabs,

        /// <summary>One "Options" tab; top-level categories are its sections (like SRDebugger).</summary>
        SingleTab
    }

    /// <summary>A feature that adds one tab with a single page. Base of the built-in Console / Profiler / Bug Report tabs.</summary>
    public abstract class SinglePageTabFeature : DebuggerFeature
    {
        private readonly string _sectionId;
        private readonly string _tabId;
        private DebugTabDefinition _tab;

        protected SinglePageTabFeature(string tabId, string sectionId, int order)
        {
            _tabId = tabId;
            _sectionId = sectionId;
            Order = order;
        }

        /// <summary>Position among the tabs (lower = further left).</summary>
        public int Order { get; set; }

        /// <summary>Creates the page. Null = the built-in page.</summary>
        public Func<DebugPage> PageFactory { get; set; }

        protected abstract string Title { get; }
        protected abstract string SectionTitle { get; }
        protected abstract DebugPage CreateDefaultPage();

        protected override void OnInstall()
        {
            _tab = new DebugTabDefinition(_tabId, Title, Order);
            var factory = PageFactory ?? CreateDefaultPage;
            _tab.Sections.Add(new DebugSection(_sectionId, SectionTitle, factory, 0));
            Debugger.Tabs.Add(_tab);
        }

        protected override void OnUninstall()
        {
            if (_tab != null && Debugger.Tabs.Find(_tabId) == _tab)
            {
                Debugger.Tabs.Remove(_tabId);
            }

            _tab = null;
        }
    }

    /// <summary>The System tab: one section per system info category (Order 0).</summary>
    public sealed class SystemInfoFeature : DebuggerFeature
    {
        private SystemInfoSectionsSync _sync;
        private DebugTabDefinition _tab;

        public int Order { get; set; } = 0;

        /// <summary>Adds <see cref="DefaultSystemInfo" /> (application, device, display ...) on install.</summary>
        public bool IncludeDefaultInfo { get; set; } = true;

        /// <summary>Creates the page of a category. Null = <see cref="SystemInfoPage" />.</summary>
        public Func<string, DebugPage> PageFactory { get; set; }

        protected override void OnInstall()
        {
            if (IncludeDefaultInfo)
            {
                DefaultSystemInfo.Populate(Debugger.SystemInfo);
            }

            _tab = new DebugTabDefinition(DebugTabIds.SystemInformation, DebuggerStrings.Get("tab.system"), Order);
            Debugger.Tabs.Add(_tab);
            _sync = new SystemInfoSectionsSync(Debugger.SystemInfo, _tab, PageFactory ?? (c => new SystemInfoPage(c)));
        }

        protected override void OnUninstall()
        {
            _sync?.Dispose();
            _sync = null;
            if (_tab != null && Debugger.Tabs.Find(_tab.Id) == _tab)
            {
                Debugger.Tabs.Remove(_tab.Id);
            }

            _tab = null;
        }
    }

    /// <summary>One section per system info category ("Application", "Device", ... and user categories).</summary>
    internal sealed class SystemInfoSectionsSync : IDisposable
    {
        private const string Prefix = "info:";
        private readonly Func<string, DebugPage> _createPage;
        private readonly SystemInfoRegistry _registry;
        private readonly DebugTabDefinition _tab;

        public SystemInfoSectionsSync(SystemInfoRegistry registry, DebugTabDefinition tab,
            Func<string, DebugPage> createPage)
        {
            _registry = registry;
            _tab = tab;
            _createPage = createPage;
            registry.Changed += Sync;
            Sync();
        }

        public void Dispose()
        {
            _registry.Changed -= Sync;
        }

        public void Sync()
        {
            var wanted = new HashSet<string>();
            var order = 0;
            foreach (var category in _registry.Categories)
            {
                var id = Prefix + category;
                wanted.Add(id);
                var c = category;
                if (!_tab.Sections.Contains(id))
                {
                    _tab.Sections.Add(new DebugSection(id, category, () => _createPage(c), order,
                        () => _registry.GetEntries(c).Count.ToString()));
                }

                order++;
            }

            var remove = new List<string>();
            foreach (var s in _tab.Sections)
            {
                if (s.Id.StartsWith(Prefix, StringComparison.Ordinal) && !wanted.Contains(s.Id))
                {
                    remove.Add(s.Id);
                }
            }

            foreach (var id in remove)
            {
                _tab.Sections.Remove(id);
            }
        }
    }

    /// <summary>
    ///     Option categories as tabs (Order 100, see <see cref="Layout" />), plus the Search (Order 50) and Pinned (Order 60)
    ///     tabs.
    /// </summary>
    public sealed class OptionsFeature : DebuggerFeature
    {
        private readonly List<string> _ownTabs = new();
        private OptionTabsSync _sync;

        public OptionsLayout Layout { get; set; } = OptionsLayout.CategoryTabs;

        /// <summary>Position of the option tabs among the tabs.</summary>
        public int Order { get; set; } = OptionTabsSync.DefaultTabOrder;

        /// <summary>Creates the page of every option section. Null = <see cref="OptionSectionPage" />.</summary>
        public OptionSectionPageFactory SectionPageFactory { get; set; }

        /// <summary>Adds the Search tab (search across every option).</summary>
        public bool SearchTab { get; set; } = true;

        /// <summary>Creates the Search tab's page. Null = <see cref="OptionSearchPage" />.</summary>
        public Func<DebugPage> SearchPageFactory { get; set; }

        /// <summary>Adds the Pinned tab (every pinned option, "Unpin All").</summary>
        public bool PinnedTab { get; set; } = true;

        /// <summary>Creates the Pinned tab's page. Null = <see cref="PinnedOptionsPage" />.</summary>
        public Func<DebugPage> PinnedPageFactory { get; set; }

        protected override void OnInstall()
        {
            if (SearchTab)
            {
                AddTab(DebugTabIds.Search, DebuggerStrings.Get("tab.search"), 50, "options",
                    SearchPageFactory ?? (() => new OptionSearchPage()));
            }

            if (PinnedTab)
            {
                AddTab(DebugTabIds.Pinned, DebuggerStrings.Get("tab.pinned"), 60, "options",
                    PinnedPageFactory ?? (() => new PinnedOptionsPage()));
            }

            _sync = new OptionTabsSync(Debugger.Options, Debugger.Tabs, Layout, SectionPageFactory, Order);
        }

        protected override void OnUninstall()
        {
            _sync?.Dispose();
            _sync = null;
            foreach (var id in _ownTabs)
            {
                Debugger.Tabs.Remove(id);
            }

            _ownTabs.Clear();
        }

        private void AddTab(string id, string title, int order, string sectionId, Func<DebugPage> factory)
        {
            var tab = new DebugTabDefinition(id, title, order);
            tab.Sections.Add(new DebugSection(sectionId, title, factory, 0));
            Debugger.Tabs.Add(tab);
            _ownTabs.Add(id);
        }
    }

    /// <summary>The Console tab (Order 200, section "logs").</summary>
    public sealed class ConsoleFeature : SinglePageTabFeature
    {
        public ConsoleFeature() : base(DebugTabIds.Console, "logs", 200)
        {
        }

        protected override string Title => DebuggerStrings.Get("tab.console");
        protected override string SectionTitle => DebuggerStrings.Get("section.logs");

        protected override DebugPage CreateDefaultPage()
        {
            return new ConsolePage();
        }
    }

    /// <summary>The Profiler tab (Order 300, section "overview").</summary>
    public sealed class ProfilerFeature : SinglePageTabFeature
    {
        public ProfilerFeature() : base(DebugTabIds.Profiler, "overview", 300)
        {
        }

        protected override string Title => DebuggerStrings.Get("tab.profiler");
        protected override string SectionTitle => DebuggerStrings.Get("section.overview");

        protected override DebugPage CreateDefaultPage()
        {
            return new ProfilerPage();
        }
    }

    /// <summary>The Bug Report tab (Order 400, section "report"). Needed by <see cref="BugReportService.ShowSheet" />.</summary>
    public sealed class BugReportFeature : SinglePageTabFeature
    {
        public BugReportFeature() : base(DebugTabIds.BugReporter, "report", 400)
        {
        }

        protected override string Title => DebuggerStrings.Get("tab.bugReport");
        protected override string SectionTitle => DebuggerStrings.Get("bug.title");

        protected override DebugPage CreateDefaultPage()
        {
            return new BugReportPage();
        }
    }
}