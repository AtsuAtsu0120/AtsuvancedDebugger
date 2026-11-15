using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Bar graph of a <see cref="FrameHistory" /> (one bar per frame, newest on the right), drawn with the vector API.
    ///     Guide lines are drawn at 1000/60 and 1000/30 ms. Colors come from the theme's custom properties.
    /// </summary>
    public sealed class FrameGraph : VisualElement
    {
        private static readonly CustomStyleProperty<Color> AccentProperty = new("--atsu-color-accent");
        private static readonly CustomStyleProperty<Color> WarningProperty = new("--atsu-color-warning");
        private static readonly CustomStyleProperty<Color> ErrorProperty = new("--atsu-color-error");
        private static readonly CustomStyleProperty<Color> GuideProperty = new("--atsu-color-border");
        private Color _accent = new(0.33f, 0.63f, 1f);
        private int _drawnVersion = -1;
        private Color _error = new(0.93f, 0.36f, 0.36f);
        private Color _guide = new(0.24f, 0.25f, 0.29f);

        private FrameHistory _history;
        private Color _warning = new(0.94f, 0.75f, 0.31f);

        public FrameGraph()
        {
            AddToClassList("atsu-frame-graph");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
        }

        /// <summary>Upper bound of the vertical axis in ms (at least this, grows to fit spikes up to 4x).</summary>
        public float MinimumScaleMs { get; set; } = 1000f / 30f;

        /// <summary>Draw the 60 / 30 fps guide lines.</summary>
        public bool ShowGuides { get; set; } = true;

        public FrameHistory History
        {
            get => _history;
            set
            {
                _history = value;
                _drawnVersion = -1;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Repaints when the history changed since the last draw.</summary>
        public void Refresh()
        {
            if (_history == null || _history.Version == _drawnVersion)
            {
                return;
            }

            MarkDirtyRepaint();
        }

        private void OnCustomStyle(CustomStyleResolvedEvent e)
        {
            var s = e.customStyle;
            if (s.TryGetValue(AccentProperty, out var c))
            {
                _accent = c;
            }

            if (s.TryGetValue(WarningProperty, out c))
            {
                _warning = c;
            }

            if (s.TryGetValue(ErrorProperty, out c))
            {
                _error = c;
            }

            if (s.TryGetValue(GuideProperty, out c))
            {
                _guide = c;
            }

            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (_history == null || rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            _drawnVersion = _history.Version;

            _history.GetStats(out _, out _, out var max);
            var scale = Mathf.Clamp(max * 1.1f, MinimumScaleMs, MinimumScaleMs * 4f);
            var p = ctx.painter2D;

            if (ShowGuides)
            {
                p.lineWidth = 1f;
                p.strokeColor = _guide;
                Guide(p, rect, 1000f / 60f, scale);
                Guide(p, rect, 1000f / 30f, scale);
            }

            var count = _history.Count;
            var capacity = _history.Capacity;
            if (count == 0)
            {
                return;
            }

            var barWidth = rect.width / capacity;
            var x0 = rect.xMax - count * barWidth;

            // Three passes (one fill per color) keep the mesh small.
            DrawBars(p, rect, x0, barWidth, scale, 0f, 1000f / 60f + 0.5f, _accent);
            DrawBars(p, rect, x0, barWidth, scale, 1000f / 60f + 0.5f, 1000f / 30f + 0.5f, _warning);
            DrawBars(p, rect, x0, barWidth, scale, 1000f / 30f + 0.5f, float.MaxValue, _error);
        }

        private void DrawBars(Painter2D p, Rect rect, float x0, float barWidth, float scale, float from, float to,
            Color color)
        {
            var any = false;
            for (var i = 0; i < _history.Count; i++)
            {
                var v = _history[i];
                if (v < from || v >= to)
                {
                    continue;
                }

                if (!any)
                {
                    p.fillColor = color;
                    p.BeginPath();
                    any = true;
                }

                var h = Mathf.Min(rect.height, v / scale * rect.height);
                var x = x0 + i * barWidth;
                var w = Mathf.Max(1f, barWidth - (barWidth > 3f ? 1f : 0f));
                p.MoveTo(new Vector2(x, rect.yMax));
                p.LineTo(new Vector2(x + w, rect.yMax));
                p.LineTo(new Vector2(x + w, rect.yMax - h));
                p.LineTo(new Vector2(x, rect.yMax - h));
                p.ClosePath();
            }

            if (any)
            {
                p.Fill();
            }
        }

        private static void Guide(Painter2D p, Rect rect, float ms, float scale)
        {
            if (ms > scale)
            {
                return;
            }

            var y = rect.yMax - ms / scale * rect.height;
            p.BeginPath();
            p.MoveTo(new Vector2(rect.xMin, y));
            p.LineTo(new Vector2(rect.xMax, y));
            p.Stroke();
        }
    }
}