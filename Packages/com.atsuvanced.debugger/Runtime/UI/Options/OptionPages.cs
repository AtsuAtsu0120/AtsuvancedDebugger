using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>Content of one option section: a level-2 category (or the "General" bucket of a top-level category).</summary>
    /// <remarks>
    ///     Public and inheritable: subclass it and register the subclass in place of the built-in section with the same id
    ///     (option sections are <c>"opt:" + level-2 name</c> and <c>"opt:&lt;general&gt;"</c> in the
    ///     <c>"option:" + top-level name</c> tab; note the option sync re-adds its own section if the id is free). When
    ///     overriding a <c>protected</c> member, call the base implementation
    ///     (e.g. wrap <c>base.CreateContent()</c> to add elements around the built-in UI).
    /// </remarks>
    public class OptionSectionPage : DebugPage
    {
        /// <param name="categoryPath">Category shown by the list (e.g. "Player/Status").</param>
        /// <param name="general">Only the options placed directly in the category (no sub-categories).</param>
        /// <param name="title">Page title.</param>
        public OptionSectionPage(string categoryPath, bool general, string title) : base(title)
        {
            CategoryPath = categoryPath;
            IsGeneral = general;
        }

        /// <summary>Category shown by the list.</summary>
        public string CategoryPath { get; }

        /// <summary>True if only the category's own options are listed.</summary>
        public bool IsGeneral { get; }

        /// <summary>The option list of this page (null until the page is first shown).</summary>
        public OptionListView View { get; private set; }

        protected override VisualElement CreateContent()
        {
            View = new OptionListView(Debugger, CategoryPath, IsGeneral,
                DebuggerStrings.Get("options.emptySection"));
            OptionListPersistence.Bind(View, Debugger.State);
            return View;
        }

        protected override VisualElement CreateToolbar()
        {
            var search = new Button(() => Navigator.SelectTab(DebugTabIds.Search))
                { text = DebuggerStrings.Get("options.search") };
            search.AddToClassList("atsu-button");
            return search;
        }

        protected override void OnShow()
        {
            View.Attach();
        }

        protected override void OnHide()
        {
            View.Detach();
        }

        protected override void OnUpdate()
        {
            View.Tick(Time.unscaledDeltaTime);
        }

        protected override void OnDispose()
        {
            View?.Dispose();
        }
    }

    /// <summary>
    ///     Search across every option category. Results keep the tree shape (all categories as headers) and only
    ///     matching branches are shown and expanded.
    /// </summary>
    /// <remarks>
    ///     Public and inheritable: subclass it and register the subclass in place of the built-in section with the same id
    ///     (the Search tab section is <c>"options"</c> in <c>DebugTabIds.Search</c>). When overriding a <c>protected</c>
    ///     member, call the base implementation
    ///     (e.g. wrap <c>base.CreateContent()</c> to add elements around the built-in UI).
    /// </remarks>
    public class OptionSearchPage : DebugPage
    {
        /// <summary>State key of the last search text.</summary>
        internal const string StateKey = "search";

        private const float SearchDebounce = 0.15f;
        private Label _hits;
        private string _pendingQuery;

        private DebuggerState _state;
        private float _timer;

        public OptionSearchPage() : base(DebuggerStrings.Get("tab.search"))
        {
        }

        /// <summary>The current search text. Setting it applies the search on the next update.</summary>
        public string Query
        {
            get => SearchField?.value ?? _pendingQuery ?? string.Empty;
            set
            {
                value ??= string.Empty;
                if (SearchField != null)
                {
                    SearchField.SetValueWithoutNotify(value);
                }

                _pendingQuery = value;
                _timer = 0f;
            }
        }

        /// <summary>The option list of this page (null until the page is first shown).</summary>
        public OptionListView View { get; private set; }

        internal TextField SearchField { get; private set; }

        protected override VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.AddToClassList("atsu-option-search");

            var bar = new VisualElement();
            bar.AddToClassList("atsu-searchbar");
            SearchField = new TextField();
            SearchField.AddToClassList("atsu-searchbar__field");
            SearchField.textEdition.placeholder = DebuggerStrings.Get("options.searchPlaceholder");
            SearchField.RegisterValueChangedCallback(e =>
            {
                _pendingQuery = e.newValue;
                _timer = SearchDebounce;
            });
            _hits = new Label();
            _hits.AddToClassList("atsu-searchbar__hits");
            var clear = new Button(() => SearchField.value = string.Empty) { text = "×" };
            clear.AddToClassList("atsu-icon-button");
            bar.Add(SearchField);
            bar.Add(_hits);
            bar.Add(clear);
            root.Add(bar);

            _state = Debugger.State;
            View = new OptionListView(Debugger, string.Empty, false, DebuggerStrings.Get("options.noMatches"));
            OptionListPersistence.Bind(View, _state);
            root.Add(View);

            // Restore the last search (applied on the first update) unless Query was set before the page was built.
            var last = _pendingQuery ?? _state?.Get(StateKey);
            if (!string.IsNullOrEmpty(last))
            {
                SearchField.SetValueWithoutNotify(last);
                _pendingQuery = last;
                _timer = 0f;
            }

            return root;
        }

        protected override void OnShow()
        {
            View.Attach();
            FocusSearch();
        }

        /// <summary>Focuses the search field (on the next UI update, once it is attached and visible).</summary>
        public void FocusSearch()
        {
            SearchField?.schedule.Execute(() => SearchField.Focus());
        }

        protected override void OnHide()
        {
            View.Detach();
        }

        protected override void OnDispose()
        {
            View?.Dispose();
        }

        protected override void OnUpdate()
        {
            if (_pendingQuery != null)
            {
                _timer -= Time.unscaledDeltaTime;
                if (_timer <= 0f)
                {
                    var q = _pendingQuery;
                    _pendingQuery = null;
                    var filter = Debugger.Options.CreateFilter(q);
                    View.SetFilter(filter);
                    _state?.Set(StateKey, q);
                    _hits.text = filter.IsActive
                        ? filter.HitCount == 1
                            ? DebuggerStrings.Get("options.searchHit")
                            : DebuggerStrings.Format("options.searchHits", filter.HitCount)
                        : string.Empty;
                }
            }

            View.Tick(Time.unscaledDeltaTime);
        }
    }
}

namespace Atsuvanced.Debugger.UI
{
    /// <summary>Restores and saves which category branch is open in an option list (public API only).</summary>
    internal static class OptionListPersistence
    {
        /// <summary>State key of the open branch of the list rooted at <paramref name="listRootPath" />.</summary>
        internal static string StateKey(string listRootPath)
        {
            return "open/" + CategoryPath.Normalize(listRootPath);
        }

        public static void Bind(OptionListView view, DebuggerState state)
        {
            if (state == null)
            {
                return;
            }

            var key = StateKey(view.CategoryPath);
            var stored = state.Get(key);
            if (!string.IsNullOrEmpty(stored))
            {
                view.OpenCategoryPath = stored;
            }

            view.OpenCategoryChanged += () => state.Set(key, view.OpenCategoryPath);
        }
    }
}

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Every pinned option in tree shape (all branches open), unpinnable one by one with the row's pin button,
    ///     plus "Unpin All" in the header (press twice to confirm). Unpinned rows stay listed (so they can be pinned
    ///     again) until the panel is closed; newly pinned options appear immediately.
    /// </summary>
    /// <remarks>
    ///     Public and inheritable: register a subclass as the <c>"options"</c> section of the <c>DebugTabIds.Pinned</c> tab.
    ///     When overriding a <c>protected</c> member, call the base implementation.
    /// </remarks>
    public class PinnedOptionsPage : DebugPage
    {
        private const float ConfirmSeconds = 3f;
        private readonly HashSet<OptionDefinition> _listed = new();
        private float _confirmTimer;
        private bool _dirty = true;
        private bool _listedValid;
        private Button _unpinAll;

        public PinnedOptionsPage() : base(DebuggerStrings.Get("pinned.title"))
        {
        }

        /// <summary>The option list of this page (null until the page is first shown).</summary>
        public OptionListView View { get; private set; }

        /// <summary>True while "Unpin All" waits for the confirming second press.</summary>
        public bool IsConfirmingUnpinAll => _confirmTimer > 0f;

        /// <summary>Options currently listed: pinned ones plus those unpinned since the panel was opened.</summary>
        public IReadOnlyCollection<OptionDefinition> ListedOptions => _listed;

        protected override VisualElement CreateContent()
        {
            View = new OptionListView(Debugger, string.Empty, false, DebuggerStrings.Get("pinned.empty"));
            return View;
        }

        protected override VisualElement CreateToolbar()
        {
            _unpinAll = new Button(OnUnpinAllClicked);
            _unpinAll.AddToClassList("atsu-button");
            UpdateUnpinAllButton();
            return _unpinAll;
        }

        protected override void OnShow()
        {
            Debugger.Options.PinStateChanged += OnPinStateChanged;
            if (!_listedValid)
            {
                _listed.Clear();
                foreach (var o in Debugger.Options.PinnedOptions)
                {
                    _listed.Add(o);
                }

                _listedValid = true;
            }

            View.Attach();
            ApplyFilter(true);
        }

        protected override void OnHide()
        {
            Debugger.Options.PinStateChanged -= OnPinStateChanged;
            // Closing the panel drops the unpinned rows; switching tabs keeps them.
            if (!Debugger.Panel.IsVisible)
            {
                _listedValid = false;
            }

            View.Detach();
            _confirmTimer = 0f;
            UpdateUnpinAllButton();
        }

        protected override void OnUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            if (_confirmTimer > 0f)
            {
                _confirmTimer -= dt;
                if (_confirmTimer <= 0f)
                {
                    UpdateUnpinAllButton();
                }
            }

            if (_dirty)
            {
                ApplyFilter(false);
            }

            View.Tick(dt);
        }

        protected override void OnDispose()
        {
            View?.Dispose();
        }

        private void OnPinStateChanged(OptionDefinition option, bool pinned)
        {
            if (pinned)
            {
                _listed.Add(option);
            }

            _dirty = true;
        }

        private void ApplyFilter(bool scrollToTop)
        {
            _dirty = false;
            View.SetFilter(OptionTreeFilter.ForOptions(Debugger.Options, _listed), scrollToTop);
            UpdateUnpinAllButton();
        }

        /// <summary>Same as pressing "Unpin All" (first press arms, second press within 3 seconds unpins).</summary>
        internal void PressUnpinAll()
        {
            OnUnpinAllClicked();
        }

        private void OnUnpinAllClicked()
        {
            if (Debugger.Options.PinnedOptions.Count == 0)
            {
                return;
            }

            if (_confirmTimer <= 0f)
            {
                _confirmTimer = ConfirmSeconds;
                UpdateUnpinAllButton();
                return;
            }

            _confirmTimer = 0f;
            Debugger.ClearPinnedOptions();
        }

        private void UpdateUnpinAllButton()
        {
            if (_unpinAll == null || Debugger == null)
            {
                return;
            }

            var count = Debugger.Options.PinnedOptions.Count;
            _unpinAll.text = _confirmTimer > 0f
                ? DebuggerStrings.Format("pinned.unpinConfirm", count)
                : DebuggerStrings.Get("pinned.unpinAll");
            _unpinAll.EnableInClassList("atsu-button--danger", _confirmTimer > 0f);
            _unpinAll.SetEnabled(count > 0);
        }
    }
}