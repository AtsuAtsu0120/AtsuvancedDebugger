using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     The panel frame: tab bar (tabs, close), the current page's header (back, title, page toolbar) and the page
    ///     content area, from the outermost choice to the innermost. Reflects the navigator (tabs, page stacks);
    ///     built-in navigation (Escape / gamepad B = back, then close) lives here.
    ///     <code>
    /// .atsu-panel
    ///   .atsu-tabbar          .atsu-tabbar__tabs | .atsu-panel__close
    ///   .atsu-header          back | title | toolbar (| close, while the page hides the tab bar)
    ///   .atsu-content         page contents (one per page, inactive ones hidden)
    /// </code>
    /// </summary>
    internal sealed class PanelShell
    {
        private readonly Button _backButton;
        private readonly Button _closeButton;
        private readonly VisualElement _content;
        private readonly VisualElement _header;
        private readonly PageNavigator _navigator;
        private readonly DebugService _service;
        private readonly VisualElement _tabBar;
        private readonly ScrollView _tabs;
        private readonly Dictionary<string, Button> _tabButtons = new();
        private readonly Label _title;
        private readonly VisualElement _toolbarSlot;
        private bool _disposed;
        private string _selectedTabId;
        private DebugPage _titledPage;
        private bool _visible;

        public PanelShell(DebugService service)
        {
            _service = service;
            _navigator = service.Navigator;

            Root = new VisualElement { name = "atsu-panel", focusable = true };
            Root.AddToClassList("atsu-panel");
            Root.AddToClassList(UssClasses.Hidden);

            _tabBar = new VisualElement();
            _tabBar.AddToClassList("atsu-tabbar");
            _tabs = new ScrollView(ScrollViewMode.Horizontal)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _tabs.AddToClassList("atsu-tabbar__tabs");
            _closeButton = new Button(() => _service.Panel.Hide()) { text = "×" };
            _closeButton.AddToClassList("atsu-icon-button");
            _closeButton.AddToClassList("atsu-panel__close");
            _tabBar.Add(_tabs);
            _tabBar.Add(_closeButton);
            Root.Add(_tabBar);

            _header = new VisualElement();
            _header.AddToClassList("atsu-header");
            _backButton = new Button(() => _navigator.Pop()) { text = "‹" };
            _backButton.AddToClassList("atsu-icon-button");
            _backButton.AddToClassList("atsu-header__back");
            _title = new Label();
            _title.AddToClassList("atsu-header__title");
            _toolbarSlot = new VisualElement();
            _toolbarSlot.AddToClassList("atsu-header__toolbar");
            _header.Add(_backButton);
            _header.Add(_title);
            _header.Add(_toolbarSlot);
            Root.Add(_header);

            _content = new VisualElement();
            _content.AddToClassList("atsu-content");
            Root.Add(_content);

            Root.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                GoBackOrClose();
                evt.StopPropagation();
            });

            _navigator.Changed += RefreshChrome;
            _navigator.PageShown += OnPageShown;
            _navigator.PageHidden += OnPageHidden;
            _navigator.PageDisposed += OnPageDisposed;
            _service.Tabs.Changed += RebuildTabs;

            RebuildTabs();
            // Re-attach a page that was already shown before the host (re)appeared.
            var current = _navigator.CurrentPage;
            if (current != null && current.IsShown)
            {
                OnPageShown(current);
            }
        }

        public VisualElement Root { get; }

        public void Show()
        {
            _visible = true;
            Root.RemoveFromClassList(UssClasses.Hidden);
            RefreshChrome();
            Root.Focus();
            // Give keyboard / gamepad navigation a starting point once the panel is laid out
            // (unless something inside the page took the focus meanwhile, e.g. the search field).
            Root.schedule.Execute(FocusCurrentTabIfIdle);
        }

        public void Hide()
        {
            _visible = false;
            Root.AddToClassList(UssClasses.Hidden);
        }

        /// <summary>Escape while nothing in the panel has focus (with focus, NavigationCancelEvent handles it).</summary>
        public void Tick()
        {
            if (_visible && Root.focusController?.focusedElement == null && ShortcutInput.WasPressed(KeyCode.Escape))
            {
                GoBackOrClose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _navigator.Changed -= RefreshChrome;
            _navigator.PageShown -= OnPageShown;
            _navigator.PageHidden -= OnPageHidden;
            _navigator.PageDisposed -= OnPageDisposed;
            _service.Tabs.Changed -= RebuildTabs;
            if (_titledPage != null)
            {
                _titledPage.AppearanceChanged -= OnAppearanceChanged;
            }

            _titledPage = null;
        }

        private void FocusCurrentTabIfIdle()
        {
            if (!_visible)
            {
                return;
            }

            var focused = Root.focusController?.focusedElement as VisualElement;
            if (focused != null && focused != Root)
            {
                return;
            }

            var id = _navigator.CurrentTabId;
            if (id != null && _tabButtons.TryGetValue(id, out var button))
            {
                button.Focus();
            }
        }

        private void GoBackOrClose()
        {
            if (!_navigator.Pop())
            {
                _service.Panel.Hide();
            }
        }

        // ---- Chrome (header / tabs) -----------------------------------------------------

        private void RebuildTabs()
        {
            _tabs.Clear();
            _tabButtons.Clear();
            foreach (var tab in _service.Tabs.Tabs)
            {
                var id = tab.Id;
                var button = new Button(() => _navigator.SelectTab(id)) { text = tab.Title, focusable = true };
                button.AddToClassList("atsu-tab");
                _tabs.Add(button);
                _tabButtons[id] = button;
            }

            _selectedTabId = null;
            RefreshChrome();
        }

        private void RefreshChrome()
        {
            var page = _navigator.CurrentPage;

            foreach (var pair in _tabButtons)
            {
                pair.Value.EnableInClassList("atsu-tab--selected", pair.Key == _navigator.CurrentTabId);
            }

            if (_selectedTabId != _navigator.CurrentTabId)
            {
                OnTabSelected();
            }

            var chrome = page?.Chrome ?? PageChrome.Default;
            var showTabBar = (chrome & PageChrome.TabBar) != 0;
            // A tab with a single page has no title in the header: the tab bar right above already names it.
            var showTitle = !(showTabBar && page is SectionedPage { ShowSidebar: false });
            var hasToolbar = page?.Toolbar != null && HasToolbarContent(page);
            // The header is left out when it would be an empty strip (no back button, title or toolbar).
            var showHeader = (chrome & PageChrome.Header) != 0 && (_navigator.CanGoBack || showTitle || hasToolbar);
            _header.EnableInClassList(UssClasses.Hidden, !showHeader);
            _tabBar.EnableInClassList(UssClasses.Hidden, !showTabBar);
            // Close lives at the end of the tab bar; while a page hides the tab bar, it moves to the header.
            var closeParent = showTabBar || !showHeader ? _tabBar : _header;
            if (_closeButton.parent != closeParent)
            {
                closeParent.Add(_closeButton);
            }

            _backButton.EnableInClassList(UssClasses.Hidden, !_navigator.CanGoBack);

            if (_titledPage != page)
            {
                if (_titledPage != null)
                {
                    _titledPage.AppearanceChanged -= OnAppearanceChanged;
                }

                _titledPage = page;
                if (page != null)
                {
                    page.AppearanceChanged += OnAppearanceChanged;
                }
            }

            _title.text = showTitle ? page?.Title ?? string.Empty : string.Empty;

            _toolbarSlot.Clear();
            if (hasToolbar)
            {
                _toolbarSlot.Add(page.Toolbar);
            }
        }

        /// <summary>A tab's root page always has a toolbar slot; it counts only while the section page puts a toolbar in it.</summary>
        private static bool HasToolbarContent(DebugPage page)
        {
            return page is not SectionedPage sectioned || sectioned.CurrentSectionPage?.Toolbar != null;
        }

        /// <summary>Keeps the selected tab visible, and moves the focus with it while a tab button has focus.</summary>
        private void OnTabSelected()
        {
            _selectedTabId = _navigator.CurrentTabId;
            if (!_visible || _selectedTabId == null || !_tabButtons.TryGetValue(_selectedTabId, out var button))
            {
                return;
            }

            _tabs.ScrollTo(button);
            if (Root.focusController?.focusedElement is Button b && b != button && b.ClassListContains("atsu-tab"))
            {
                button.Focus();
            }
        }

        private void OnAppearanceChanged(DebugPage page)
        {
            if (page == _navigator.CurrentPage)
            {
                RefreshChrome();
            }
        }

        // ---- Page content ---------------------------------------------------------------

        private void OnPageShown(DebugPage page)
        {
            if (page.Content == null)
            {
                return;
            }

            if (page.Content.parent != _content)
            {
                _content.Add(page.Content);
            }

            page.Content.RemoveFromClassList(UssClasses.Hidden);
            RefreshChrome();
        }

        private static void OnPageHidden(DebugPage page)
        {
            page.Content?.AddToClassList(UssClasses.Hidden);
        }

        private static void OnPageDisposed(DebugPage page)
        {
            page.Content?.RemoveFromHierarchy();
            page.Toolbar?.RemoveFromHierarchy();
        }
    }
}