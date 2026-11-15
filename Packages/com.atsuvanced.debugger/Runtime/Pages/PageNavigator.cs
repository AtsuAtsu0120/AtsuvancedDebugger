using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>Navigation inside the debug panel: tab selection and the page stack of each tab.</summary>
    public interface IPageNavigator
    {
        string CurrentTabId { get; }

        /// <summary>The top page of the current tab, or null if no tab is selected.</summary>
        DebugPage CurrentPage { get; }

        /// <summary>Number of pages in the current tab's stack (1 = root page only).</summary>
        int Depth { get; }

        bool CanGoBack { get; }

        /// <summary>
        ///     The page the user is looking at: the selected section's page when the current page is a tab's root (the tab's
        ///     sections), otherwise <see cref="CurrentPage" />.
        /// </summary>
        DebugPage ActivePage { get; }

        /// <summary>The tabs in display order (same as <c>Tabs.Tabs</c>).</summary>
        IReadOnlyList<DebugTabDefinition> Tabs { get; }

        /// <summary>Selects the next (+1) or previous (-1) tab, wrapping around.</summary>
        void SelectAdjacentTab(int direction);

        /// <summary>Raised when the tab, the stack or the current page changes.</summary>
        event Action Changed;

        void SelectTab(string tabId);

        /// <summary>Pushes a page on the current tab's stack. A page instance can only be pushed once.</summary>
        void Push(DebugPage page);

        /// <summary>Pops the top page (unless it is the root or it cancels via OnBackRequested).</summary>
        bool Pop();

        void PopToRoot();

        /// <summary>Closes the debug panel.</summary>
        void Close();
    }

    /// <summary>
    ///     UI-independent implementation of the page stacks. The view listens to the Page* events to attach,
    ///     show, hide and remove page content.
    /// </summary>
    internal sealed class PageNavigator : IPageNavigator
    {
        private readonly Action _close;
        private readonly IDebugService _service;
        private readonly Dictionary<string, List<DebugPage>> _stacks = new();
        private readonly DebugTabRegistry _tabs;

        public PageNavigator(DebugTabRegistry tabs, IDebugService service, Action close)
        {
            _tabs = tabs ?? throw new ArgumentNullException(nameof(tabs));
            _service = service;
            _close = close;
            _tabs.Changed += OnTabsChanged;
        }

        /// <summary>True while the panel is visible; the current page is shown only then.</summary>
        public bool IsActive { get; private set; }

        private List<DebugPage> CurrentStack =>
            CurrentTabId != null && _stacks.TryGetValue(CurrentTabId, out var s) ? s : null;

        public string CurrentTabId { get; private set; }
        public DebugPage CurrentPage => CurrentStack is { Count: > 0 } s ? s[s.Count - 1] : null;

        public DebugPage ActivePage =>
            CurrentPage is SectionedPage sectioned ? sectioned.CurrentSectionPage ?? sectioned : CurrentPage;

        public IReadOnlyList<DebugTabDefinition> Tabs => _tabs.Tabs;

        public void SelectAdjacentTab(int direction)
        {
            var tabs = _tabs.Tabs;
            if (tabs.Count == 0)
            {
                return;
            }

            var current = -1;
            for (var i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Id == CurrentTabId)
                {
                    current = i;
                }
            }

            var next = ((current < 0 ? 0 : current + direction) % tabs.Count + tabs.Count) % tabs.Count;
            SelectTab(tabs[next].Id);
        }

        public int Depth => CurrentStack?.Count ?? 0;
        public bool CanGoBack => Depth > 1;

        public event Action Changed;

        public void SelectTab(string tabId)
        {
            var tab = _tabs.Find(tabId);
            if (tab == null)
            {
                Debug.LogWarning($"[AtsuvancedDebugger] Unknown tab '{tabId}'.");
                return;
            }

            if (tabId == CurrentTabId && CurrentStack != null)
            {
                return;
            }

            HidePage(CurrentPage);
            CurrentTabId = tabId;
            EnsureStack(tab);
            ShowPage(CurrentPage);
            Changed?.Invoke();
        }

        public void Push(DebugPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (page.IsAttached)
            {
                throw new InvalidOperationException($"Page '{page.Title}' is already in a page stack.");
            }

            EnsureTabSelected(null);
            var stack = CurrentStack ?? throw new InvalidOperationException("No tab is available to push a page on.");

            HidePage(CurrentPage);
            page.InternalAttach(this, _service);
            stack.Add(page);
            ShowPage(page);
            Changed?.Invoke();
        }

        public bool Pop()
        {
            var stack = CurrentStack;
            if (stack == null || stack.Count <= 1)
            {
                return false;
            }

            var top = stack[stack.Count - 1];
            if (!top.InternalBackRequested())
            {
                return false;
            }

            HidePage(top);
            stack.RemoveAt(stack.Count - 1);
            DisposePage(top);
            ShowPage(CurrentPage);
            Changed?.Invoke();
            return true;
        }

        public void PopToRoot()
        {
            while (CanGoBack)
            {
                if (!Pop())
                {
                    break;
                }
            }
        }

        public void Close()
        {
            _close?.Invoke();
        }

        internal event Action<DebugPage> PageShown;
        internal event Action<DebugPage> PageHidden;
        internal event Action<DebugPage> PageDisposed;

        /// <summary>Selects <paramref name="tabId" />, or keeps the current tab, or falls back to the first tab.</summary>
        public void EnsureTabSelected(string preferredTabId)
        {
            if (CurrentTabId != null && preferredTabId == null)
            {
                return;
            }

            var id = preferredTabId != null && _tabs.Find(preferredTabId) != null
                ? preferredTabId
                : CurrentTabId ?? (_tabs.Tabs.Count > 0 ? _tabs.Tabs[0].Id : null);
            if (id != null)
            {
                SelectTab(id);
            }
        }

        /// <summary>Called when the panel becomes visible / hidden.</summary>
        public void SetActive(bool active)
        {
            if (IsActive == active)
            {
                return;
            }

            IsActive = active;
            if (active)
            {
                ShowPage(CurrentPage);
            }
            else
            {
                HidePage(CurrentPage);
            }
        }

        public void Update(float unscaledDeltaTime)
        {
            if (IsActive)
            {
                CurrentPage?.InternalUpdate(unscaledDeltaTime);
            }
        }

        /// <summary>Disposes every page of every tab.</summary>
        public void Reset()
        {
            HidePage(CurrentPage);
            foreach (var stack in _stacks.Values)
            {
                for (var i = stack.Count - 1; i >= 0; i--)
                {
                    DisposePage(stack[i]);
                }
            }

            _stacks.Clear();
            CurrentTabId = null;
            Changed?.Invoke();
        }

        /// <summary>All pages of a tab, root first (for the view to attach content).</summary>
        public IReadOnlyList<DebugPage> GetStack(string tabId)
        {
            return tabId != null && _stacks.TryGetValue(tabId, out var s)
                ? s
                : Array.Empty<DebugPage>();
        }

        private void EnsureStack(DebugTabDefinition tab)
        {
            if (_stacks.ContainsKey(tab.Id))
            {
                return;
            }

            DebugPage root;
            try
            {
                root = tab.CreateRootPage() ?? throw new InvalidOperationException("CreateRootPage returned null.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var error = e;
                root = new ElementPage(tab.Title,
                    () => DebugPage.CreateErrorElement($"Failed to create tab '{tab.Id}'", error));
            }

            root.InternalAttach(this, _service);
            _stacks.Add(tab.Id, new List<DebugPage> { root });
        }

        private void ShowPage(DebugPage page)
        {
            if (!IsActive || page == null || page.IsShown)
            {
                return;
            }

            page.InternalShow();
            PageShown?.Invoke(page);
        }

        private void HidePage(DebugPage page)
        {
            if (page == null || !page.IsShown)
            {
                return;
            }

            page.InternalHide();
            PageHidden?.Invoke(page);
        }

        private void DisposePage(DebugPage page)
        {
            page.InternalDispose();
            PageDisposed?.Invoke(page);
        }

        private void OnTabsChanged()
        {
            var removed = new List<string>();
            foreach (var id in _stacks.Keys)
            {
                if (_tabs.Find(id) == null)
                {
                    removed.Add(id);
                }
            }

            foreach (var id in removed)
            {
                var stack = _stacks[id];
                if (id == CurrentTabId)
                {
                    HidePage(CurrentPage);
                }

                for (var i = stack.Count - 1; i >= 0; i--)
                {
                    DisposePage(stack[i]);
                }

                _stacks.Remove(id);
                if (id == CurrentTabId)
                {
                    CurrentTabId = null;
                }
            }

            if (CurrentTabId == null && IsActive)
            {
                EnsureTabSelected(null);
            }

            Changed?.Invoke();
        }
    }
}