using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger
{
    /// <summary>Where an overlay sits on the game screen.</summary>
    public enum OverlayAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight,

        /// <summary>Full width along the top edge (e.g. the docked console). Corner overlays move below it.</summary>
        TopEdge,

        /// <summary>Full width along the bottom edge. Corner overlays move above it.</summary>
        BottomEdge
    }

    /// <summary>When an overlay may be on screen.</summary>
    public enum OverlayVisibility
    {
        /// <summary>Only while the panel is closed (trigger, pinned options, docked console ...).</summary>
        WhilePanelClosed,

        /// <summary>Also on top of the open panel.</summary>
        Always
    }

    /// <summary>
    ///     UI drawn over the game screen, outside the panel: the trigger, pinned options, the docked console and profiler are
    ///     all overlays, and users add their own the same way (<see cref="IDebugService.Overlays" />).
    ///     Overlays sharing an anchor are stacked from the screen corner / edge inward, in <see cref="Order" />.
    ///     <para>
    ///         Lifecycle: CreateContent (once, lazily) → OnShow → OnUpdate* → OnHide → ... → OnDispose. The overlay is shown
    ///         while <see cref="IsActive" /> is true and <see cref="Visibility" /> allows it (no overlay is shown while the
    ///         PIN pad
    ///         is up). Exceptions from callbacks are logged and never break the panel.
    ///     </para>
    /// </summary>
    public abstract class DebugOverlay
    {
        private OverlayAnchor _anchor;
        private int _order;

        protected DebugOverlay(string id, OverlayAnchor anchor, int order = 0)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Overlay id is required.", nameof(id));
            }

            Id = id;
            _anchor = anchor;
            _order = order;
        }

        public string Id { get; }

        /// <summary>Screen corner / edge. Can change at any time (the overlay moves).</summary>
        public OverlayAnchor Anchor
        {
            get => _anchor;
            protected set
            {
                if (_anchor == value)
                {
                    return;
                }

                _anchor = value;
                LayoutChanged?.Invoke(this);
            }
        }

        /// <summary>Position in the stack of its anchor: lower values are closer to the screen corner / edge.</summary>
        public int Order
        {
            get => _order;
            protected set
            {
                if (_order == value)
                {
                    return;
                }

                _order = value;
                LayoutChanged?.Invoke(this);
            }
        }

        public virtual OverlayVisibility Visibility => OverlayVisibility.WhilePanelClosed;

        /// <summary>
        ///     Whether the overlay wants to be on screen now (e.g. something is pinned, the docked console is turned on).
        ///     Evaluated every frame, so keep it cheap.
        /// </summary>
        public virtual bool IsActive => true;

        /// <summary>The debug service. Null until the overlay is added to a registry.</summary>
        public IDebugService Debugger { get; private set; }

        /// <summary>Root element. Null until the overlay is shown for the first time.</summary>
        public VisualElement Content { get; private set; }

        public bool IsShown { get; private set; }
        public bool IsDisposed { get; private set; }

        /// <summary>Raised when <see cref="Anchor" /> or <see cref="Order" /> changes.</summary>
        internal event Action<DebugOverlay> LayoutChanged;

        protected abstract VisualElement CreateContent();

        protected virtual void OnShow()
        {
        }

        protected virtual void OnHide()
        {
        }

        /// <summary>Called every frame while shown (unscaled delta time in seconds).</summary>
        protected virtual void OnUpdate(float unscaledDeltaTime)
        {
        }

        protected virtual void OnDispose()
        {
        }

        // ---- Internal lifecycle (driven by DebugOverlayRegistry) ---------------------------

        internal void InternalAttach(IDebugService service)
        {
            if (Debugger != null)
            {
                throw new InvalidOperationException($"Overlay '{Id}' is already registered.");
            }

            if (IsDisposed)
            {
                throw new InvalidOperationException($"Overlay '{Id}' has been disposed.");
            }

            Debugger = service;
        }

        internal bool EvaluateActive()
        {
            try
            {
                return IsActive;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        internal void InternalShow()
        {
            if (IsShown || IsDisposed)
            {
                return;
            }

            EnsureContent();
            IsShown = true;
            Guard(OnShow);
        }

        internal void InternalHide()
        {
            if (!IsShown)
            {
                return;
            }

            IsShown = false;
            Guard(OnHide);
        }

        internal void InternalUpdate(float unscaledDeltaTime)
        {
            if (!IsShown)
            {
                return;
            }

            try
            {
                OnUpdate(unscaledDeltaTime);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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
            Guard(OnDispose);
            Content?.RemoveFromHierarchy();
        }

        private void EnsureContent()
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
                content = DebugPage.CreateErrorElement($"Failed to create overlay '{Id}'", e);
            }

            content.AddToClassList("atsu-overlay");
            Content = content;
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public override string ToString()
        {
            return $"Overlay:{Id}";
        }
    }

    /// <summary>
    ///     The overlays of a debugger (<see cref="IDebugService.Overlays" />). Decides every frame which overlays are shown;
    ///     the view only places their content. Main-thread only.
    /// </summary>
    public sealed class DebugOverlayRegistry
    {
        private readonly List<DebugOverlay> _overlays = new();
        private readonly IDebugService _service;
        private readonly List<DebugOverlay> _snapshot = new();
        private bool _updating;

        internal DebugOverlayRegistry(IDebugService service)
        {
            _service = service;
        }

        /// <summary>Overlays in registration order.</summary>
        public IReadOnlyList<DebugOverlay> Overlays => _overlays;

        /// <summary>True if any overlay is on screen.</summary>
        public bool AnyShown
        {
            get
            {
                foreach (var o in _overlays)
                {
                    if (o.IsShown)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Raised when overlays are added or removed.</summary>
        public event Action Changed;

        internal event Action<DebugOverlay> OverlayShown;
        internal event Action<DebugOverlay> OverlayHidden;
        internal event Action<DebugOverlay> OverlayDisposed;
        internal event Action<DebugOverlay> OverlayLayoutChanged;

        /// <summary>Adds an overlay. An overlay with the same id is replaced (and disposed).</summary>
        public void Add(DebugOverlay overlay)
        {
            if (overlay == null)
            {
                throw new ArgumentNullException(nameof(overlay));
            }

            RemoveInternal(overlay.Id);
            overlay.InternalAttach(_service);
            overlay.LayoutChanged += OnLayoutChanged;
            _overlays.Add(overlay);
            Changed?.Invoke();
        }

        public bool Remove(string id)
        {
            if (!RemoveInternal(id))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        public DebugOverlay Find(string id)
        {
            foreach (var o in _overlays)
            {
                if (o.Id == id)
                {
                    return o;
                }
            }

            return null;
        }

        public T Find<T>() where T : DebugOverlay
        {
            foreach (var o in _overlays)
            {
                if (o is T t)
                {
                    return t;
                }
            }

            return null;
        }

        private bool RemoveInternal(string id)
        {
            var overlay = Find(id);
            if (overlay == null)
            {
                return false;
            }

            _overlays.Remove(overlay);
            overlay.LayoutChanged -= OnLayoutChanged;
            if (overlay.IsShown)
            {
                OverlayHidden?.Invoke(overlay);
            }

            overlay.InternalDispose();
            OverlayDisposed?.Invoke(overlay);
            return true;
        }

        private void OnLayoutChanged(DebugOverlay overlay)
        {
            OverlayLayoutChanged?.Invoke(overlay);
        }

        /// <summary>Shows / hides overlays for the current panel state and updates the shown ones.</summary>
        internal void Update(bool panelVisible, bool entryCodeShown, float unscaledDeltaTime)
        {
            // Iterate over a copy (callbacks may add or remove overlays). The buffer is reused so the per-frame
            // update allocates nothing; a nested Update (from a callback) falls back to its own copy.
            var snapshot = _updating ? new List<DebugOverlay>(_overlays) : _snapshot;
            if (!_updating)
            {
                _snapshot.Clear();
                _snapshot.AddRange(_overlays);
            }

            _updating = true;
            try
            {
                for (var i = 0; i < snapshot.Count; i++)
                {
                    UpdateOne(snapshot[i], panelVisible, entryCodeShown, unscaledDeltaTime);
                }
            }
            finally
            {
                if (snapshot == _snapshot)
                {
                    _updating = false;
                    _snapshot.Clear();
                }
            }
        }

        private void UpdateOne(DebugOverlay o, bool panelVisible, bool entryCodeShown, float unscaledDeltaTime)
        {
            if (o.IsDisposed)
            {
                return;
            }

            var allowed = !entryCodeShown && (o.Visibility == OverlayVisibility.Always || !panelVisible);
            var show = allowed && o.EvaluateActive();
            if (show && !o.IsShown)
            {
                o.InternalShow();
                OverlayShown?.Invoke(o);
            }
            else if (!show && o.IsShown)
            {
                o.InternalHide();
                OverlayHidden?.Invoke(o);
            }

            o.InternalUpdate(unscaledDeltaTime);
        }

        /// <summary>Hides every overlay (e.g. when the panel is destroyed); they come back on the next update.</summary>
        internal void HideAll()
        {
            foreach (var o in _overlays.ToArray())
            {
                if (!o.IsShown)
                {
                    continue;
                }

                o.InternalHide();
                OverlayHidden?.Invoke(o);
            }
        }

        /// <summary>Disposes every overlay (service shutdown).</summary>
        internal void Clear()
        {
            foreach (var o in _overlays.ToArray())
            {
                RemoveInternal(o.Id);
            }

            Changed?.Invoke();
        }
    }
}