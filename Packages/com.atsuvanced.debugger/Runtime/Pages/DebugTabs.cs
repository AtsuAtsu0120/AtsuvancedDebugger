using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     A tab of the debug panel. A tab is a list of <see cref="Sections" />: with two or more sections a list is shown
    ///     on the left and the selected section's page fills the rest; with one section its page fills the tab.
    ///     Built-in tabs use the same mechanism, so sections can be added to them as well.
    /// </summary>
    public sealed class DebugTabDefinition
    {
        public const int DefaultOrder = 1000;

        /// <summary>Id of the section created by the single-page constructor.</summary>
        public const string MainSectionId = "main";

        /// <summary>A tab whose sections are added afterwards via <see cref="Sections" />.</summary>
        public DebugTabDefinition(string id, string title, int order = DefaultOrder)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Tab id is required.", nameof(id));
            }

            Id = id;
            Title = string.IsNullOrEmpty(title) ? id : title;
            Order = order;
        }

        /// <summary>A tab with a single page (one section).</summary>
        public DebugTabDefinition(string id, string title, Func<DebugPage> createPage, int order = DefaultOrder)
            : this(id, title, order)
        {
            if (createPage == null)
            {
                throw new ArgumentNullException(nameof(createPage));
            }

            Sections.Add(new DebugSection(MainSectionId, Title, createPage, 0));
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>
        ///     Tabs are sorted by this value (built-in: System 0, Search 50, option categories 100, Console 200, Profiler
        ///     300, Bug Report 400).
        /// </summary>
        public int Order { get; }

        public DebugSectionCollection Sections { get; } = new();

        /// <summary>The page at the bottom of this tab's page stack (hosts the sections).</summary>
        internal DebugPage CreateRootPage()
        {
            return new SectionedPage(this);
        }

        public override string ToString()
        {
            return $"Tab:{Id}";
        }
    }

    public static class DebugTabIds
    {
        /// <summary>Prefix of the tabs generated for top-level option categories ("option:Player").</summary>
        public const string OptionCategoryPrefix = "option:";

        public const string SystemInformation = "system";

        /// <summary>Search across every option.</summary>
        public const string Search = "search";

        /// <summary>Every pinned option, with "Unpin All".</summary>
        public const string Pinned = "pinned";

        public const string Console = "console";
        public const string Profiler = "profiler";
        public const string BugReporter = "bug-reporter";

        /// <summary>The tab of the options gathered in one tab when <c>OptionsFeature.Layout</c> is <c>SingleTab</c>.</summary>
        public const string Options = "options";
    }

    /// <summary>Registered tabs (built-in and custom). Main-thread only.</summary>
    public sealed class DebugTabRegistry
    {
        private readonly List<DebugTabDefinition> _tabs = new();

        /// <summary>Tabs sorted by Order, then by registration order.</summary>
        public IReadOnlyList<DebugTabDefinition> Tabs => _tabs;

        public event Action Changed;

        /// <summary>Adds a tab. A tab with the same id is replaced.</summary>
        public void Add(DebugTabDefinition tab)
        {
            if (tab == null)
            {
                throw new ArgumentNullException(nameof(tab));
            }

            _tabs.RemoveAll(t => t.Id == tab.Id);

            var index = _tabs.Count;
            for (var i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Order > tab.Order)
                {
                    index = i;
                    break;
                }
            }

            _tabs.Insert(index, tab);
            Changed?.Invoke();
        }

        public bool Remove(string id)
        {
            if (_tabs.RemoveAll(t => t.Id == id) == 0)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        public DebugTabDefinition Find(string id)
        {
            foreach (var t in _tabs)
            {
                if (t.Id == id)
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>
        ///     Puts the items with the given ids into this relative order, keeping the slots they occupy
        ///     (other items do not move). Returns true and raises Changed if anything moved.
        /// </summary>
        public bool Reorder(IReadOnlyList<string> ids)
        {
            var slots = new List<int>();
            var items = new List<DebugTabDefinition>();
            foreach (var id in ids)
            {
                var index = _tabs.FindIndex(x => x.Id == id);
                if (index < 0)
                {
                    continue;
                }

                slots.Add(index);
                items.Add(_tabs[index]);
            }

            slots.Sort();
            var changed = false;
            for (var i = 0; i < slots.Count; i++)
            {
                if (ReferenceEquals(_tabs[slots[i]], items[i]))
                {
                    continue;
                }

                _tabs[slots[i]] = items[i];
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }
    }
}