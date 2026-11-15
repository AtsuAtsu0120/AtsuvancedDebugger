using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Mirrors the option tree into panel tabs (<see cref="OptionsFeature" />).
    ///     <list type="bullet">
    ///         <item>
    ///             <see cref="OptionsLayout.CategoryTabs" />: every top-level category becomes a tab ("option:Player") whose
    ///             sections are "General" (options placed directly in it) and its level-2 categories.
    ///         </item>
    ///         <item>
    ///             <see cref="OptionsLayout.SingleTab" />: one "options" tab whose sections are the top-level categories.
    ///         </item>
    ///     </list>
    ///     Deeper levels are shown inside the section page. Only sections created here (id prefix "opt:") are managed, so
    ///     user-added sections are left alone.
    /// </summary>
    internal sealed class OptionTabsSync : IDisposable
    {
        public const int DefaultTabOrder = 100;
        public const string GeneralSectionId = "opt:<general>";
        private const string SectionPrefix = "opt:";

        private readonly OptionSectionPageFactory _createPage;
        private readonly OptionsLayout _layout;
        private readonly int _order;
        private readonly HashSet<string> _ownedTabs = new();
        private readonly OptionRegistry _registry;
        private readonly DebugTabRegistry _tabs;

        public OptionTabsSync(OptionRegistry registry, DebugTabRegistry tabs,
            OptionsLayout layout = OptionsLayout.CategoryTabs,
            OptionSectionPageFactory createPage = null, int order = DefaultTabOrder)
        {
            _registry = registry;
            _tabs = tabs;
            _layout = layout;
            _order = order;
            _createPage = createPage ?? ((path, direct, title) => new OptionSectionPage(path, direct, title));
            registry.CategoryAdded += OnCategoryChanged;
            registry.CategoryRemoved += OnCategoryChanged;
            registry.OptionAdded += OnOptionChanged;
            registry.OptionRemoved += OnOptionChanged;
            registry.CategoriesReordered += OnCategoryChanged;
            Sync();
        }

        public void Dispose()
        {
            _registry.CategoryAdded -= OnCategoryChanged;
            _registry.CategoryRemoved -= OnCategoryChanged;
            _registry.OptionAdded -= OnOptionChanged;
            _registry.OptionRemoved -= OnOptionChanged;
            _registry.CategoriesReordered -= OnCategoryChanged;
            foreach (var id in new List<string>(_ownedTabs))
            {
                _tabs.Remove(id);
            }

            _ownedTabs.Clear();
        }

        public static string TabId(CategoryNode top)
        {
            return DebugTabIds.OptionCategoryPrefix + top.Name;
        }

        private void OnCategoryChanged(CategoryNode _)
        {
            Sync();
        }

        private void OnOptionChanged(OptionDefinition o)
        {
            // Only options directly in a top-level category affect the section list ("General").
            if (_layout == OptionsLayout.CategoryTabs && o.CategorySegments.Count == 1)
            {
                Sync();
            }
        }

        public void Sync()
        {
            if (_layout == OptionsLayout.SingleTab)
            {
                SyncSingleTab();
            }
            else
            {
                SyncCategoryTabs();
            }
        }

        private void SyncSingleTab()
        {
            var id = DebugTabIds.Options;
            if (_registry.Root.Children.Count == 0)
            {
                if (_ownedTabs.Remove(id))
                {
                    _tabs.Remove(id);
                }

                return;
            }

            var tab = _tabs.Find(id);
            if (tab == null)
            {
                tab = new DebugTabDefinition(id, DebuggerStrings.Get("tab.options"), _order);
                SyncSections(tab, _registry.Root.Children, false);
                _tabs.Add(tab);
                _ownedTabs.Add(id);
            }
            else
            {
                SyncSections(tab, _registry.Root.Children, false);
            }
        }

        private void SyncCategoryTabs()
        {
            var alive = new HashSet<string>();
            var order = new List<string>();
            foreach (var top in _registry.Root.Children)
            {
                var id = TabId(top);
                alive.Add(id);
                order.Add(id);
                var tab = _tabs.Find(id);
                if (tab == null)
                {
                    tab = new DebugTabDefinition(id, top.Name, _order);
                    SyncTopLevelTab(tab, top);
                    _tabs.Add(tab);
                    _ownedTabs.Add(id);
                }
                else
                {
                    SyncTopLevelTab(tab, top);
                }
            }

            foreach (var id in new List<string>(_ownedTabs))
            {
                if (alive.Contains(id))
                {
                    continue;
                }

                _ownedTabs.Remove(id);
                _tabs.Remove(id);
            }

            // Category order (FR-CAT-08) decides the order of the option tabs among themselves.
            _tabs.Reorder(order);
        }

        private void SyncTopLevelTab(DebugTabDefinition tab, CategoryNode top)
        {
            var path = top.Path;
            if (top.Options.Count > 0)
            {
                if (!tab.Sections.Contains(GeneralSectionId))
                {
                    tab.Sections.Add(new DebugSection(GeneralSectionId, DebuggerStrings.Get("section.general"),
                        () => _createPage(path, true, top.Name), 0,
                        () => _registry.FindCategory(path)?.Options.Count.ToString()));
                }
            }
            else
            {
                tab.Sections.Remove(GeneralSectionId);
            }

            SyncSections(tab, top.Children, true);
        }

        /// <summary>One section per category in <paramref name="categories" /> (removes the "opt:" sections that are gone).</summary>
        private void SyncSections(DebugTabDefinition tab, IReadOnlyList<CategoryNode> categories, bool keepGeneral)
        {
            var wanted = new HashSet<string>();
            if (keepGeneral && tab.Sections.Contains(GeneralSectionId))
            {
                wanted.Add(GeneralSectionId);
            }

            var order = 1;
            foreach (var child in categories)
            {
                var id = SectionPrefix + child.Name;
                var childPath = child.Path;
                var title = child.Name;
                wanted.Add(id);
                if (!tab.Sections.Contains(id))
                {
                    tab.Sections.Add(new DebugSection(id, title, () => _createPage(childPath, false, title), order,
                        () => _registry.FindCategory(childPath)?.TotalOptionCount.ToString()));
                }

                order++;
            }

            var remove = new List<string>();
            foreach (var s in tab.Sections)
            {
                if (s.Id.StartsWith(SectionPrefix, StringComparison.Ordinal) && !wanted.Contains(s.Id))
                {
                    remove.Add(s.Id);
                }
            }

            foreach (var id in remove)
            {
                tab.Sections.Remove(id);
            }

            var sectionOrder = new List<string>();
            foreach (var child in categories)
            {
                sectionOrder.Add(SectionPrefix + child.Name);
            }

            tab.Sections.Reorder(sectionOrder);
        }
    }
}