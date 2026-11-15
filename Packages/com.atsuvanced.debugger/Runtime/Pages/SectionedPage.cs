using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Root page of every tab: a left list of the tab's sections (only when there are two or more)
    ///     and the selected section's page. Section pages are created lazily, kept while the tab lives,
    ///     and receive the normal page lifecycle (OnShow / OnUpdate / OnHide / OnDispose).
    /// </summary>
    internal sealed class SectionedPage : DebugPage
    {
        private readonly Dictionary<string, DebugPage> _pages = new();
        private VisualElement _content;
        private Label _empty;

        private readonly List<string> _removed = new();
        private readonly List<(DebugSection section, VisualElement row, Label badge)> _sidebarRows = new();
        private VisualElement _sidebar;
        private bool _sidebarDirty = true;
        private bool _sidebarStale = true; // structure / badges must be rebuilt (not just the selection)
        private VisualElement _toolbar;

        public SectionedPage(DebugTabDefinition tab) : base(tab.Title)
        {
            Tab = tab;
        }

        public DebugTabDefinition Tab { get; }

        public string SelectedSectionId { get; private set; }

        /// <summary>The page of the selected section (null before the tab is first shown).</summary>
        public DebugPage CurrentSectionPage { get; private set; }

        /// <summary>True when the tab has two or more sections (the left list is shown).</summary>
        internal bool ShowSidebar => Tab.Sections.Count > 1;

        /// <summary>A single-section tab behaves like its page (e.g. PageChrome.None is honoured).</summary>
        public override PageChrome Chrome => !ShowSidebar && CurrentSectionPage != null
            ? CurrentSectionPage.Chrome
            : PageChrome.Default;

        /// <summary>State key of the section selected last time in a tab.</summary>
        internal static string StateKey(string tabId)
        {
            return "section/" + tabId;
        }

        protected override VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.AddToClassList("atsu-sectioned");

            _sidebar = new ScrollView(ScrollViewMode.Vertical);
            _sidebar.AddToClassList("atsu-sectioned__sidebar");
            root.Add(_sidebar);

            _content = new VisualElement();
            _content.AddToClassList("atsu-sectioned__content");
            root.Add(_content);

            _empty = new Label(DebuggerStrings.Get("section.empty"));
            _empty.AddToClassList("atsu-empty");
            _content.Add(_empty);
            return root;
        }

        protected override VisualElement CreateToolbar()
        {
            _toolbar = new VisualElement();
            _toolbar.AddToClassList("atsu-sectioned__toolbar");
            return _toolbar;
        }

        protected override void OnShow()
        {
            // First show: restore the section selected last time (falls back to the first one if it is gone).
            if (SelectedSectionId == null)
            {
                SelectedSectionId = Debugger?.State?.Get(StateKey(Tab.Id));
            }

            Tab.Sections.Changed += OnSectionsChanged;
            SyncSections();
            if (CurrentSectionPage != null)
            {
                ShowPage(CurrentSectionPage);
            }
        }

        protected override void OnHide()
        {
            Tab.Sections.Changed -= OnSectionsChanged;
            CurrentSectionPage?.InternalHide();
        }

        protected override void OnUpdate()
        {
            if (_sidebarDirty)
            {
                RebuildSidebar();
            }

            CurrentSectionPage?.InternalUpdate(Time.unscaledDeltaTime);
        }

        protected override void OnDispose()
        {
            foreach (var p in _pages.Values)
            {
                p.InternalDispose();
            }

            _pages.Clear();
            CurrentSectionPage = null;
        }

        protected override bool OnBackRequested()
        {
            return true;
        }

        /// <summary>Selects a section by id (no-op if it does not exist).</summary>
        public void Select(string sectionId)
        {
            var section = Tab.Sections.Find(sectionId);
            if (section == null)
            {
                return;
            }

            if (SelectedSectionId == section.Id && CurrentSectionPage != null)
            {
                return;
            }

            if (CurrentSectionPage != null)
            {
                CurrentSectionPage.InternalHide();
                CurrentSectionPage.Content?.AddToClassList(UssClasses.Hidden);
            }

            SelectedSectionId = section.Id;
            Debugger?.State?.Set(StateKey(Tab.Id), section.Id);
            CurrentSectionPage = GetOrCreatePage(section);
            _sidebarDirty = true;
            if (IsShown)
            {
                ShowPage(CurrentSectionPage);
            }

            UpdateTitle(section);
            NotifyChromeChanged();
        }

        private void ShowPage(DebugPage page)
        {
            page.EnsureContent();
            if (page.Content.parent != _content)
            {
                _content.Add(page.Content);
            }

            page.Content.RemoveFromClassList(UssClasses.Hidden);
            page.Content.AddToClassList("atsu-sectioned__page");

            _toolbar.Clear();
            if (page.Toolbar != null)
            {
                _toolbar.Add(page.Toolbar);
            }

            page.InternalShow();
        }

        private DebugPage GetOrCreatePage(DebugSection section)
        {
            if (_pages.TryGetValue(section.Id, out var page))
            {
                return page;
            }

            try
            {
                page = section.CreatePage() ?? throw new InvalidOperationException("CreatePage returned null.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var error = e;
                page = new ElementPage(section.Title,
                    () => CreateErrorElement($"Failed to create section '{section.Id}'", error));
            }

            page.InternalAttach(Navigator, Debugger);
            _pages.Add(section.Id, page);
            return page;
        }

        private void OnSectionsChanged()
        {
            _sidebarStale = true;
            SyncSections();
        }

        private void SyncSections()
        {
            // Dispose pages of removed sections.
            var removed = _removed;
            removed.Clear();
            foreach (var id in _pages.Keys)
            {
                if (!Tab.Sections.Contains(id))
                {
                    removed.Add(id);
                }
            }

            foreach (var id in removed)
            {
                var page = _pages[id];
                _pages.Remove(id);
                if (page == CurrentSectionPage)
                {
                    CurrentSectionPage = null;
                    SelectedSectionId = null;
                    _toolbar?.Clear();
                }

                page.InternalDispose();
            }

            removed.Clear();
            if (CurrentSectionPage == null && Tab.Sections.Count > 0)
            {
                var keep = SelectedSectionId != null && Tab.Sections.Contains(SelectedSectionId)
                    ? SelectedSectionId
                    : Tab.Sections[0].Id;
                Select(keep);
            }

            if (SelectedSectionId != null)
            {
                UpdateTitle(Tab.Sections.Find(SelectedSectionId));
            }

            _empty?.EnableInClassList(UssClasses.Hidden, Tab.Sections.Count > 0);
            _sidebarDirty = true;
            if (_sidebar != null)
            {
                RebuildSidebar();
            }

            NotifyChromeChanged();
        }

        private void RebuildSidebar()
        {
            _sidebarDirty = false;
            if (_sidebar == null)
            {
                return;
            }

            _sidebar.EnableInClassList(UssClasses.Hidden, !ShowSidebar);
            if (!ShowSidebar)
            {
                return;
            }

            // Showing the tab again or changing the selection keeps the rows (no allocation); only a changed set of
            // sections or an explicit InvalidateSidebar rebuilds them.
            if (!_sidebarStale && SidebarMatchesSections() && UpdateSidebarInPlace())
            {
                return;
            }

            _sidebarStale = false;
            _sidebarRows.Clear();
            _sidebar.Clear();
            foreach (var section in Tab.Sections)
            {
                var id = section.Id;
                var row = new VisualElement();
                row.AddToClassList("atsu-sidebar-item");
                row.EnableInClassList("atsu-sidebar-item--selected", id == SelectedSectionId);
                var name = new Label(section.Title);
                name.AddToClassList("atsu-sidebar-item__name");
                row.Add(name);

                string badge = null;
                try
                {
                    badge = section.Badge?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                Label count = null;
                if (!string.IsNullOrEmpty(badge))
                {
                    count = new Label(badge);
                    count.AddToClassList("atsu-sidebar-item__count");
                    row.Add(count);
                }

                row.AddManipulator(new Clickable(() => Select(id)));
                _sidebar.Add(row);
                _sidebarRows.Add((section, row, count));
            }
        }

        private bool SidebarMatchesSections()
        {
            var sections = Tab.Sections;
            if (sections.Count != _sidebarRows.Count)
            {
                return false;
            }

            for (var i = 0; i < sections.Count; i++)
            {
                if (!ReferenceEquals(sections[i], _sidebarRows[i].section))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Updates selection and badges of the existing rows. False when a badge appeared / disappeared.</summary>
        private bool UpdateSidebarInPlace()
        {
            for (var i = 0; i < _sidebarRows.Count; i++)
            {
                var (section, row, badgeLabel) = _sidebarRows[i];
                row.EnableInClassList("atsu-sidebar-item--selected", section.Id == SelectedSectionId);
                if (section.Badge == null)
                {
                    continue;
                }

                string badge = null;
                try
                {
                    badge = section.Badge();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                if (string.IsNullOrEmpty(badge) != (badgeLabel == null))
                {
                    return false;
                }

                if (badgeLabel != null && badgeLabel.text != badge)
                {
                    badgeLabel.text = badge;
                }
            }

            return true;
        }

        private void UpdateTitle(DebugSection section)
        {
            // The tab bar sits right above the header and already names the tab, so the header shows the section only.
            Title = ShowSidebar ? section?.Title ?? Tab.Title : CurrentSectionPage?.Title ?? Tab.Title;
        }

        /// <summary>Marks the left list for refresh (e.g. badges changed).</summary>
        public void InvalidateSidebar()
        {
            _sidebarDirty = true;
            _sidebarStale = true;
        }
    }
}