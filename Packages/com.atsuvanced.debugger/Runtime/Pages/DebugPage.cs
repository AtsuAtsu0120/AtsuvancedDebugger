using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger
{
    /// <summary>Which parts of the panel frame are shown while a page is current.</summary>
    [Flags]
    public enum PageChrome
    {
        /// <summary>The page owns the whole panel area (use Navigator.Pop / Close for navigation).</summary>
        None = 0,
        Header = 1 << 0,
        TabBar = 1 << 1,
        Default = Header | TabBar
    }

    /// <summary>
    ///     A screen of the debug panel. Every tab has a stack of pages: the root page and pages pushed on top
    ///     (with a back button). Subclass this to build any UI with UI Toolkit (code or UXML).
    ///     <para>Lifecycle: CreateContent (once, lazily) → OnShow → OnUpdate* → OnHide → ... → OnDispose.</para>
    ///     Exceptions thrown from any callback are logged and never break the panel.
    /// </summary>
    public abstract class DebugPage
    {
        private string _title;
        private float _updateTimer;

        protected DebugPage(string title = null)
        {
            _title = string.IsNullOrEmpty(title) ? GetType().Name : title;
        }

        public string Title
        {
            get => _title;
            protected set
            {
                if (_title == value)
                {
                    return;
                }

                _title = value;
                AppearanceChanged?.Invoke(this);
            }
        }

        /// <summary>Frame parts shown while this page is current.</summary>
        public virtual PageChrome Chrome => PageChrome.Default;

        /// <summary>Navigator of the stack this page belongs to. Null until pushed.</summary>
        public IPageNavigator Navigator { get; private set; }

        /// <summary>The debug service. Null until pushed.</summary>
        public IDebugService Debugger { get; private set; }

        /// <summary>Root element of the page. Null until the page is shown for the first time.</summary>
        public VisualElement Content { get; private set; }

        /// <summary>Optional element shown on the right side of the header (e.g. a "Send" button).</summary>
        public VisualElement Toolbar { get; private set; }

        public bool IsAttached => Navigator != null;
        public bool IsShown { get; private set; }
        public bool IsDisposed { get; private set; }

        protected virtual float UpdateInterval => 0f;

        /// <summary>Raised when the title or <see cref="Chrome" /> changes (the panel frame refreshes).</summary>
        internal event Action<DebugPage> AppearanceChanged;

        /// <summary>Call after something that affects <see cref="Chrome" /> changed.</summary>
        protected void NotifyChromeChanged()
        {
            AppearanceChanged?.Invoke(this);
        }

        /// <summary>Builds the page UI. Called once, the first time the page is shown.</summary>
        protected abstract VisualElement CreateContent();

        /// <summary>Builds the header toolbar. Called once together with <see cref="CreateContent" />.</summary>
        protected virtual VisualElement CreateToolbar()
        {
            return null;
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void OnHide()
        {
        }

        /// <summary>Called while shown, every <see cref="UpdateInterval" /> seconds (0 = every frame, unscaled time).</summary>
        protected virtual void OnUpdate()
        {
        }

        protected virtual void OnDispose()
        {
        }

        /// <summary>Return false to cancel the back navigation (e.g. confirm discarding unsent input).</summary>
        protected virtual bool OnBackRequested()
        {
            return true;
        }

        // ---- Factories ------------------------------------------------------------------

        /// <summary>A page whose content is created by <paramref name="factory" />.</summary>
        public static DebugPage FromElement(string title, Func<VisualElement> factory)
        {
            return new ElementPage(title, factory);
        }

        /// <summary>A page instantiated from UXML. <paramref name="dataSource" /> enables runtime data binding.</summary>
        public static DebugPage FromUxml(string title, VisualTreeAsset uxml, object dataSource = null,
            Action<VisualElement> onCreated = null)
        {
            return new UxmlPage(title, uxml, dataSource, onCreated);
        }

        // ---- Internal lifecycle (driven by PageNavigator) ---------------------------------

        internal void InternalAttach(IPageNavigator navigator, IDebugService service)
        {
            if (IsAttached)
            {
                throw new InvalidOperationException($"Page '{Title}' is already in a page stack.");
            }

            if (IsDisposed)
            {
                throw new InvalidOperationException($"Page '{Title}' has been disposed.");
            }

            Navigator = navigator;
            Debugger = service;
        }

        internal void InternalShow()
        {
            if (IsShown)
            {
                return;
            }

            EnsureContent();
            IsShown = true;
            _updateTimer = 0f;
            try
            {
                OnShow();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void InternalHide()
        {
            if (!IsShown)
            {
                return;
            }

            IsShown = false;
            try
            {
                OnHide();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void InternalUpdate(float unscaledDeltaTime)
        {
            if (!IsShown)
            {
                return;
            }

            var interval = UpdateInterval;
            if (interval > 0f)
            {
                _updateTimer += unscaledDeltaTime;
                if (_updateTimer < interval)
                {
                    return;
                }

                _updateTimer = 0f;
            }

            // Called directly (no Guard(Action)): a method group conversion allocates a delegate every frame.
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal bool InternalBackRequested()
        {
            try
            {
                return OnBackRequested();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return true;
            }
        }

        internal void InternalDispose()
        {
            if (IsDisposed)
            {
                return;
            }

            InternalHide();
            IsDisposed = true;
            try
            {
                OnDispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Content?.RemoveFromHierarchy();
            Toolbar?.RemoveFromHierarchy();
        }

        internal void EnsureContent()
        {
            if (Content != null)
            {
                return;
            }

            VisualElement content;
            try
            {
                content = CreateContent() ?? new VisualElement();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                content = CreateErrorElement(DebuggerStrings.Format("error.page", Title), e);
            }

            content.AddToClassList("atsu-page");
            Content = content;

            try
            {
                Toolbar = CreateToolbar();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Toolbar = null;
            }
        }

        /// <summary>
        ///     The element shown in place of UI that failed (a message and the exception). Use it in custom pages and views so
        ///     one failing part never breaks the rest of the panel.
        /// </summary>
        public static VisualElement CreateErrorElement(string message, Exception e)
        {
            var box = new VisualElement();
            box.AddToClassList("atsu-error-box");
            var title = new Label(message);
            title.AddToClassList("atsu-error-box__title");
            box.Add(title);
            if (e != null)
            {
                var detail = new Label($"{e.GetType().Name}: {e.Message}");
                detail.AddToClassList("atsu-error-box__detail");
                box.Add(detail);
            }

            return box;
        }

        public override string ToString()
        {
            return $"Page:{Title}";
        }
    }

    /// <summary>Page backed by a factory delegate.</summary>
    public sealed class ElementPage : DebugPage
    {
        private readonly Func<VisualElement> _factory;

        public ElementPage(string title, Func<VisualElement> factory) : base(title)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        protected override VisualElement CreateContent()
        {
            return _factory();
        }
    }

    /// <summary>Page instantiated from a UXML asset, optionally data-bound to <c>dataSource</c>.</summary>
    public class UxmlPage : DebugPage
    {
        private readonly object _dataSource;
        private readonly Action<VisualElement> _onCreated;
        private readonly VisualTreeAsset _uxml;

        public UxmlPage(string title, VisualTreeAsset uxml, object dataSource = null,
            Action<VisualElement> onCreated = null)
            : base(title)
        {
            _uxml = uxml ? uxml : throw new ArgumentNullException(nameof(uxml));
            _dataSource = dataSource;
            _onCreated = onCreated;
        }

        protected override VisualElement CreateContent()
        {
            var root = _uxml.Instantiate();
            if (_dataSource != null)
            {
                root.dataSource = _dataSource;
            }

            _onCreated?.Invoke(root);
            return root;
        }
    }

    /// <summary>Simple page with a message. Used for not-yet-implemented built-in tabs.</summary>
    internal sealed class PlaceholderPage : DebugPage
    {
        private readonly string _message;

        public PlaceholderPage(string title, string message) : base(title)
        {
            _message = message;
        }

        protected override VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.AddToClassList("atsu-placeholder");
            root.Add(new Label(_message));
            return root;
        }
    }
}