using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     The option list used by the built-in pages: the options of one category in tree shape (level 3+ as collapsible
    ///     headers,
    ///     one open branch at a time), virtualized so thousands of options stay cheap. Row views are created the first time
    ///     they
    ///     scroll into view and cached; only rows on screen are polled for value changes.
    ///     <para>
    ///         Use it to show options inside a custom page. Drive it from the page lifecycle: <see cref="Attach" /> in
    ///         <c>OnShow</c>,
    ///         <see cref="Detach" /> in <c>OnHide</c>, <see cref="Tick" /> in <c>OnUpdate</c> and <see cref="Dispose" /> in
    ///         <c>OnDispose</c>.
    ///     </para>
    /// </summary>
    public sealed class OptionListView : VisualElement
    {
        private const float PollInterval = 0.3f;
        private const float IndentPerDepth = 14f;
        private readonly HashSet<IOptionEntryView> _bound = new();
        private readonly Label _empty;
        private readonly Dictionary<object, IOptionEntryView> _entryViews = new();
        private readonly Dictionary<CategoryNode, CategoryHeader> _headers = new();
        private readonly ListView _list;

        private readonly OptionRegistry _registry;
        private readonly OptionViewRegistry _views;
        private bool _dirty = true;
        private int _filterVersion = -1;
        private float _pollTimer;

        /// <param name="debugger">The debugger whose options (and option views) are shown (usually <c>Debugger</c> of the page).</param>
        /// <param name="categoryPath">Category at the root of the list (e.g. "Player/Status"); "" = every category.</param>
        /// <param name="directOptionsOnly">Only the options placed directly in the category (no sub-categories).</param>
        /// <param name="emptyText">Text shown when nothing is listed (null = the built-in text).</param>
        public OptionListView(IDebugService debugger, string categoryPath = "", bool directOptionsOnly = false,
            string emptyText = null)
            : this((debugger ?? throw new ArgumentNullException(nameof(debugger))).Options, debugger.OptionViews,
                new OptionListModel(debugger.Options, categoryPath, directOptionsOnly),
                emptyText ?? DebuggerStrings.Get("options.emptySection"))
        {
        }

        internal OptionListView(OptionRegistry registry, OptionViewRegistry views, OptionListModel model,
            string emptyText)
        {
            _registry = registry;
            _views = views;
            Model = model;
            AddToClassList("atsu-option-list");

            _list = new ListView
            {
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None,
                itemsSource = model.Rows,
                makeItem = MakeSlot,
                bindItem = BindSlot,
                unbindItem = UnbindSlot
            };
            _list.AddToClassList("atsu-option-list__list");
            Add(_list);

            _empty = new Label(emptyText);
            _empty.AddToClassList("atsu-empty");
            Add(_empty);
        }

        internal OptionListModel Model { get; }

        /// <summary>Number of entry rows currently in the visual tree (diagnostics / tests).</summary>
        internal int BoundRowCount => _bound.Count;

        /// <summary>Category at the root of the list ("" = every category).</summary>
        public string CategoryPath => Model.RootPath;

        /// <summary>True if only the options placed directly in <see cref="CategoryPath" /> are listed.</summary>
        public bool DirectOptionsOnly => Model.EntriesOnly;

        /// <summary>The active filter (<see cref="OptionTreeFilter.None" /> when nothing is filtered).</summary>
        public OptionTreeFilter Filter => Model.Filter;

        /// <summary>
        ///     Path of the open category branch (its ancestors are open too), or null when every branch is closed.
        ///     Save it with <see cref="OpenCategoryChanged" /> and restore it after creating the list to keep the branch across
        ///     launches.
        /// </summary>
        public string OpenCategoryPath
        {
            get => Model.OpenPath;
            set
            {
                Model.SetOpenPath(value);
                _dirty = true;
            }
        }

        /// <summary>Raised when the user opens or closes a category branch.</summary>
        public event Action OpenCategoryChanged
        {
            add => Model.OpenPathChanged += value;
            remove => Model.OpenPathChanged -= value;
        }

        /// <summary>Starts following registry changes (options / categories added or removed). Call when the list becomes visible.</summary>
        public void Attach()
        {
            _registry.OptionAdded += OnOptionAdded;
            _registry.OptionRemoved += OnOptionRemoved;
            _registry.CategoryAdded += OnCategoryAdded;
            _registry.CategoryRemoved += OnCategoryRemoved;
            _registry.GroupRemoved += OnGroupRemoved;
            _registry.PinStateChanged += OnPinStateChanged;
            _registry.CategoriesReordered += OnCategoriesReordered;
            PruneStaleViews();
            Rebuild();
        }

        /// <summary>Stops following registry changes. Call when the list is hidden.</summary>
        public void Detach()
        {
            _registry.OptionAdded -= OnOptionAdded;
            _registry.OptionRemoved -= OnOptionRemoved;
            _registry.CategoryAdded -= OnCategoryAdded;
            _registry.CategoryRemoved -= OnCategoryRemoved;
            _registry.GroupRemoved -= OnGroupRemoved;
            _registry.PinStateChanged -= OnPinStateChanged;
            _registry.CategoriesReordered -= OnCategoriesReordered;
        }

        /// <summary>Applies pending changes and refreshes the values of the rows on screen. Call every frame while visible.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_dirty)
            {
                Rebuild();
            }

            _pollTimer -= unscaledDeltaTime;
            if (_pollTimer > 0f)
            {
                return;
            }

            _pollTimer = PollInterval;
            foreach (var v in _bound)
            {
                v.Refresh();
            }
        }

        /// <summary>
        ///     Shows only the options matching <paramref name="filter" /> (e.g. <see cref="OptionRegistry.CreateFilter" /> for a
        ///     search,
        ///     <see cref="OptionTreeFilter.ForOptions" /> for a fixed set); matching branches are expanded. Null clears the
        ///     filter.
        /// </summary>
        public void SetFilter(OptionTreeFilter filter, bool scrollToTop = true)
        {
            Model.SetFilter(filter);
            _filterVersion = _registry.Version;
            Rebuild();
            if (scrollToTop)
            {
                _list.ScrollToItem(0);
            }
        }

        /// <summary>
        ///     Detaches and disposes every row view (custom <see cref="OptionView" />s receive Dispose). Call from
        ///     <c>OnDispose</c>.
        /// </summary>
        public void Dispose()
        {
            Detach();
            foreach (var v in _entryViews.Values)
            {
                v.Dispose();
            }

            _entryViews.Clear();
            _headers.Clear();
            _bound.Clear();
        }

        private void Rebuild()
        {
            _dirty = false;
            if (Model.Filter.IsActive && _filterVersion != _registry.Version)
            {
                Model.SetFilter(Model.Filter.Recreate(_registry));
                _filterVersion = _registry.Version;
            }
            else
            {
                Model.Refresh();
            }

            _list.RefreshItems();
            var none = Model.Rows.Count == 0;
            _empty.EnableInClassList(UssClasses.Hidden, !none);
            _list.EnableInClassList(UssClasses.Hidden, none);
        }

        // ---- Slots ------------------------------------------------------------------------

        private static VisualElement MakeSlot()
        {
            var slot = new VisualElement();
            slot.AddToClassList("atsu-options__slot");
            return slot;
        }

        private void BindSlot(VisualElement slot, int index)
        {
            var row = Model.Rows[index];
            slot.style.paddingLeft = row.Depth * IndentPerDepth;

            VisualElement element;
            if (row.Kind == OptionListModel.RowKind.Category)
            {
                var header = GetHeader(row.Node);
                header.Update(row.Node, Model.IsExpanded(row.Node));
                element = header;
            }
            else
            {
                var view = GetEntryView(row);
                view.ApplyHighlight(Model.Filter);
                view.Refresh();
                _bound.Add(view);
                element = view.Element;
            }

            slot.userData = element;
            slot.Add(element);
        }

        private void UnbindSlot(VisualElement slot, int index)
        {
            if (slot.userData is VisualElement element && element.parent == slot)
            {
                element.RemoveFromHierarchy();
                if (element is IOptionEntryView view)
                {
                    _bound.Remove(view);
                }
            }

            slot.userData = null;
        }

        private CategoryHeader GetHeader(CategoryNode node)
        {
            if (_headers.TryGetValue(node, out var header))
            {
                return header;
            }

            header = new CategoryHeader(() =>
            {
                Model.Toggle(node);
                _list.RefreshItems();
            });
            _headers.Add(node, header);
            return header;
        }

        private IOptionEntryView GetEntryView(OptionListModel.Row row)
        {
            var key = row.Key;
            if (_entryViews.TryGetValue(key, out var view))
            {
                return view;
            }

            view = row.Entry.IsGroup
                ? new OptionGroupRow(row.Entry.Group, _registry, _views)
                : new OptionRow(row.Entry.Option, null, _registry, _views, false);
            _entryViews.Add(key, view);
            return view;
        }

        // ---- Registry events --------------------------------------------------------------

        private void OnOptionAdded(OptionDefinition _)
        {
            _dirty = true;
        }

        private void OnCategoryAdded(CategoryNode _)
        {
            _dirty = true;
        }

        private void OnCategoriesReordered(CategoryNode _)
        {
            _dirty = true;
        }

        private void OnOptionRemoved(OptionDefinition o)
        {
            DisposeView(o);
            _dirty = true;
        }

        private void OnGroupRemoved(OptionGroup g)
        {
            DisposeView(g);
            _dirty = true;
        }

        private void OnCategoryRemoved(CategoryNode n)
        {
            _headers.Remove(n);
            _dirty = true;
        }

        private void OnPinStateChanged(OptionDefinition o, bool pinned)
        {
            var group = _registry.GetGroup(o);
            if (_entryViews.TryGetValue(group != null ? group : o, out var view))
            {
                view.UpdatePinState();
            }
        }

        private void DisposeView(object key)
        {
            if (!_entryViews.TryGetValue(key, out var view))
            {
                return;
            }

            _bound.Remove(view);
            view.Element.RemoveFromHierarchy();
            view.Dispose();
            _entryViews.Remove(key);
        }

        private void PruneStaleViews()
        {
            var stale = new List<object>();
            foreach (var key in _entryViews.Keys)
            {
                var alive = key is OptionDefinition o
                    ? _registry.GetCategory(o) != null && _registry.GetGroup(o) == null
                    : key is OptionGroup g && g.Members.Count > 0 && g.Category.FindGroup(g.Key) == g;
                if (!alive)
                {
                    stale.Add(key);
                }
            }

            foreach (var key in stale)
            {
                DisposeView(key);
            }

            var staleNodes = new List<CategoryNode>();
            foreach (var node in _headers.Keys)
            {
                if (_registry.FindCategory(node.Path) != node)
                {
                    staleNodes.Add(node);
                }
            }

            foreach (var node in staleNodes)
            {
                _headers.Remove(node);
            }
        }

        /// <summary>Clickable category row (arrow, name, option count).</summary>
        private sealed class CategoryHeader : VisualElement
        {
            private readonly Label _arrow;
            private readonly Label _count;
            private readonly Label _name;

            public CategoryHeader(Action toggle)
            {
                AddToClassList("atsu-category-header");
                _arrow = new Label();
                _arrow.AddToClassList("atsu-category-header__arrow");
                _name = new Label();
                _name.AddToClassList("atsu-category-header__name");
                _count = new Label();
                _count.AddToClassList("atsu-category-header__count");
                Add(_arrow);
                Add(_name);
                Add(_count);
                this.AddManipulator(new Clickable(toggle));
            }

            public void Update(CategoryNode node, bool expanded)
            {
                _arrow.text = expanded ? "▾" : "▸";
                _name.text = node.Name;
                _count.text = node.TotalOptionCount.ToString();
                EnableInClassList("atsu-category-header--expanded", expanded);
            }
        }
    }
}