using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>System information table of one category (or all categories), refreshed every second.</summary>
    /// <remarks>
    ///     Public and inheritable: subclass it and register the subclass in place of the built-in section with the same id
    ///     (System tab sections are <c>"info:" + category</c>). When overriding a <c>protected</c> member, call the base
    ///     implementation
    ///     (e.g. wrap <c>base.CreateContent()</c> to add elements around the built-in UI).
    /// </remarks>
    public class SystemInfoPage : DebugPage
    {
        private readonly List<Row> _rows = new();
        private bool _dirty = true;
        private ScrollView _scroll;

        /// <param name="category">Category to show, or null for every category.</param>
        public SystemInfoPage(string category = null) : base(category ?? "System Information")
        {
            Category = category;
        }

        /// <summary>The category shown, or null for every category.</summary>
        public string Category { get; }

        protected override float UpdateInterval => 1f;

        protected override VisualElement CreateContent()
        {
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("atsu-info");
            return _scroll;
        }

        protected override VisualElement CreateToolbar()
        {
            var copy = new Button(CopyToClipboard) { text = DebuggerStrings.Get("common.copyAll") };
            copy.AddToClassList("atsu-button");
            return copy;
        }

        protected override void OnShow()
        {
            Debugger.SystemInfo.Changed += MarkDirty;
            Refresh();
        }

        protected override void OnHide()
        {
            Debugger.SystemInfo.Changed -= MarkDirty;
        }

        protected override void OnUpdate()
        {
            Refresh();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void Refresh()
        {
            if (_dirty)
            {
                Rebuild();
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.HasShown && row.Entry.IsConstant)
                {
                    continue;
                }

                // Format only when the value changed (Equals on the boxed / cached value), so polling allocates nothing.
                var value = row.Entry.Value;
                if (row.HasShown && Equals(value, row.Shown))
                {
                    continue;
                }

                row.HasShown = true;
                row.Shown = value;
                var text = Format(value);
                if (row.Label.text != text)
                {
                    row.Label.text = text;
                }
            }
        }

        private void Rebuild()
        {
            _dirty = false;
            _rows.Clear();
            _scroll.Clear();

            var registry = Debugger.SystemInfo;
            foreach (var category in registry.Categories)
            {
                if (Category != null && category != Category)
                {
                    continue;
                }

                VisualElement container;
                if (Category == null)
                {
                    var foldout = new Foldout { text = category, value = true };
                    foldout.AddToClassList("atsu-section");
                    container = foldout;
                }
                else
                {
                    container = new VisualElement();
                }

                foreach (var entry in registry.GetEntries(category))
                {
                    var row = new VisualElement();
                    row.AddToClassList("atsu-info-row");
                    var title = new Label(entry.Title);
                    title.AddToClassList("atsu-info-row__title");
                    var value = new Label();
                    value.AddToClassList("atsu-info-row__value");
                    value.selection.isSelectable = true;
                    row.Add(title);
                    row.Add(value);
                    container.Add(row);
                    _rows.Add(new Row { Entry = entry, Label = value });
                }

                _scroll.Add(container);
            }
        }

        private void CopyToClipboard()
        {
            var sb = new StringBuilder();
            var registry = Debugger.SystemInfo;
            foreach (var category in registry.Categories)
            {
                sb.Append("## ").AppendLine(category);
                foreach (var entry in registry.GetEntries(category))
                {
                    sb.Append(entry.Title).Append(": ").AppendLine(Format(entry.Value));
                }

                sb.AppendLine();
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        private static string Format(object value)
        {
            return value == null ? "-" : value.ToString();
        }

        private sealed class Row
        {
            public InfoEntry Entry;
            public bool HasShown;
            public Label Label;
            public object Shown;
        }
    }
}