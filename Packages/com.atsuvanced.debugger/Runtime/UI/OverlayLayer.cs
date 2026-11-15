using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Places overlay content on the screen. Edge overlays (<see cref="OverlayAnchor.TopEdge" /> /
    ///     <see cref="OverlayAnchor.BottomEdge" />) take the full width and push the corner stacks inward; overlays sharing a
    ///     corner are stacked from the corner in <see cref="DebugOverlay.Order" />, so they never overlap.
    ///     <code>
    /// .atsu-overlays
    ///   .atsu-overlays__edge--top
    ///   .atsu-overlays__middle
    ///     .atsu-overlays__stack--top-left / --top-center / --top-right / --bottom-left / --bottom-center / --bottom-right
    ///   .atsu-overlays__edge--bottom
    /// </code>
    ///     Which overlays are shown is decided by <see cref="DebugOverlayRegistry" />; this class only reflects it.
    /// </summary>
    internal sealed class OverlayLayer
    {
        private readonly Dictionary<OverlayAnchor, VisualElement> _containers = new();
        private readonly DebugOverlayRegistry _registry;
        private bool _disposed;

        public OverlayLayer(DebugOverlayRegistry registry)
        {
            _registry = registry;
            Root = Container("atsu-overlays");

            var top = Container("atsu-overlays__edge", "atsu-overlays__edge--top");
            var middle = Container("atsu-overlays__middle");
            var bottom = Container("atsu-overlays__edge", "atsu-overlays__edge--bottom");
            Root.Add(top);
            Root.Add(middle);
            Root.Add(bottom);
            _containers[OverlayAnchor.TopEdge] = top;
            _containers[OverlayAnchor.BottomEdge] = bottom;

            foreach (var anchor in new[]
                     {
                         OverlayAnchor.TopLeft, OverlayAnchor.TopCenter, OverlayAnchor.TopRight,
                         OverlayAnchor.BottomLeft, OverlayAnchor.BottomCenter, OverlayAnchor.BottomRight
                     })
            {
                var stack = Container("atsu-overlays__stack", "atsu-overlays__stack--" + Kebab(anchor));
                middle.Add(stack);
                _containers[anchor] = stack;
            }

            registry.OverlayShown += OnShown;
            registry.OverlayHidden += OnHidden;
            registry.OverlayDisposed += OnDisposed;
            registry.OverlayLayoutChanged += OnLayoutChanged;

            foreach (var o in registry.Overlays)
            {
                if (o.IsShown)
                {
                    OnShown(o);
                }
            }
        }

        public VisualElement Root { get; }

        /// <summary>The container of an anchor (tests / diagnostics).</summary>
        internal VisualElement GetContainer(OverlayAnchor anchor)
        {
            return _containers[anchor];
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _registry.OverlayShown -= OnShown;
            _registry.OverlayHidden -= OnHidden;
            _registry.OverlayDisposed -= OnDisposed;
            _registry.OverlayLayoutChanged -= OnLayoutChanged;
            foreach (var o in _registry.Overlays)
            {
                o.Content?.RemoveFromHierarchy();
            }
        }

        private void OnShown(DebugOverlay overlay)
        {
            if (overlay.Content == null)
            {
                return;
            }

            Place(overlay);
            overlay.Content.RemoveFromClassList(UssClasses.Hidden);
        }

        private static void OnHidden(DebugOverlay overlay)
        {
            overlay.Content?.AddToClassList(UssClasses.Hidden);
        }

        private static void OnDisposed(DebugOverlay overlay)
        {
            overlay.Content?.RemoveFromHierarchy();
        }

        private void OnLayoutChanged(DebugOverlay overlay)
        {
            if (overlay.Content?.parent != null)
            {
                Place(overlay);
            }
        }

        /// <summary>Puts the content in its anchor's container, ordered by <see cref="DebugOverlay.Order" />.</summary>
        private void Place(DebugOverlay overlay)
        {
            var container = _containers[overlay.Anchor];
            var content = overlay.Content;
            content.RemoveFromHierarchy();

            var index = container.childCount;
            for (var i = 0; i < container.childCount; i++)
            {
                var other = Owner(container[i]);
                if (other != null && other.Order > overlay.Order)
                {
                    index = i;
                    break;
                }
            }

            container.Insert(index, content);
        }

        private DebugOverlay Owner(VisualElement content)
        {
            foreach (var o in _registry.Overlays)
            {
                if (o.Content == content)
                {
                    return o;
                }
            }

            return null;
        }

        private static VisualElement Container(params string[] classes)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes)
            {
                e.AddToClassList(c);
            }

            return e;
        }

        private static string Kebab(OverlayAnchor anchor)
        {
            return anchor switch
            {
                OverlayAnchor.TopLeft => "top-left",
                OverlayAnchor.TopCenter => "top-center",
                OverlayAnchor.TopRight => "top-right",
                OverlayAnchor.BottomLeft => "bottom-left",
                OverlayAnchor.BottomCenter => "bottom-center",
                _ => "bottom-right"
            };
        }
    }
}