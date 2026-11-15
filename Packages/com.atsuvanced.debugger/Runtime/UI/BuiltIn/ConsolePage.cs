using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Console tab: virtualized log list, severity toggles with counts, text / regex filter, collapse,
    ///     detail view with stack trace, copy, and tail-follow that pauses while the user scrolls up.
    /// </summary>
    /// <remarks>
    ///     Public and inheritable: subclass it and register the subclass in place of the built-in section with the same id
    ///     (
    ///     <c>
    ///         AtsuDebug.Instance.Tabs.Find(DebugTabIds.Console).Sections.Add(new DebugSection("logs", "Logs", () =&gt; new
    ///         MyConsolePage()))
    ///     </c>
    ///     ). When overriding a <c>protected</c> member, call the base implementation
    ///     (e.g. wrap <c>base.CreateContent()</c> to add elements around the built-in UI).
    /// </remarks>
    public class ConsolePage : DebugPage
    {
        private const float RowHeight = 46f;

        private readonly List<ConsoleFilter.Item> _items = new();
        private readonly List<LogEntry> _snapshot = new();
        private readonly LogTextCache _text = new(256);
        private readonly int[] _selection = new int[1];
        private readonly CountText _infoText = new(), _warningText = new(), _errorText = new();
        private VisualElement _detail;
        private Label _detailMessage, _detailStack, _detailMeta;
        private Button _dockButton;
        private bool _filterDirty = true;
        private bool _follow = true;

        private Button _infoButton, _warningButton, _errorButton, _collapseButton, _regexButton;
        private ListView _list;
        private Scroller _scroller;
        private bool _scrollingProgrammatically;
        private TextField _search;
        private int _seenVersion = -1;
        private long _selectedSequence = -1;
        private Label _status;
        private float _statusTimer;

        public ConsolePage() : base(DebuggerStrings.Get("tab.console"))
        {
        }

        /// <summary>Filter state (severity toggles, text / regex, collapse) of this page.</summary>
        protected ConsoleFilter Filter { get; } = new();

        protected override float UpdateInterval => 0.1f;

        /// <summary>The docked console (<see cref="DockedConsoleFeature" />), or null when it is not installed.</summary>
        protected IDockedConsole DockedConsole => Debugger?.GetFeature<DockedConsoleFeature>();

        /// <summary>Re-applies the filter on the next update (call after changing <see cref="Filter" /> from code).</summary>
        protected void InvalidateFilter()
        {
            _filterDirty = true;
        }

        protected override VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.AddToClassList("atsu-console");

            var bar = new VisualElement();
            bar.AddToClassList("atsu-console__bar");
            _infoButton = SeverityButton("atsu-console__sev--info", () => Filter.ShowInfo = !Filter.ShowInfo);
            _warningButton =
                SeverityButton("atsu-console__sev--warning", () => Filter.ShowWarning = !Filter.ShowWarning);
            _errorButton = SeverityButton("atsu-console__sev--error", () => Filter.ShowError = !Filter.ShowError);
            _collapseButton = ToggleButton(DebuggerStrings.Get("console.collapse"),
                () => Filter.Collapse = !Filter.Collapse);
            bar.Add(_infoButton);
            bar.Add(_warningButton);
            bar.Add(_errorButton);
            bar.Add(_collapseButton);

            _search = new TextField();
            _search.AddToClassList("atsu-console__search");
            _search.textEdition.placeholder = DebuggerStrings.Get("console.filter");
            _search.RegisterValueChangedCallback(e =>
            {
                Filter.Text = e.newValue;
                _filterDirty = true;
            });
            bar.Add(_search);
            _regexButton = ToggleButton(".*", () => Filter.UseRegex = !Filter.UseRegex);
            _regexButton.tooltip = DebuggerStrings.Get("console.regex");
            bar.Add(_regexButton);
            root.Add(bar);

            _list = new ListView
            {
                fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                itemsSource = _items,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            _list.AddToClassList("atsu-console__list");
            _list.selectionChanged += _ => OnSelectionChanged();
            _scroller = _list.Q<ScrollView>()?.verticalScroller;
            if (_scroller != null)
            {
                _scroller.valueChanged += OnScrolled;
            }

            root.Add(_list);

            _detail = new VisualElement();
            _detail.AddToClassList("atsu-console__detail");
            _detail.AddToClassList(UssClasses.Hidden);
            var detailBar = new VisualElement();
            detailBar.AddToClassList("atsu-console__detail-bar");
            _detailMeta = new Label();
            _detailMeta.AddToClassList("atsu-console__detail-meta");
            var copy = new Button(CopySelected) { text = DebuggerStrings.Get("common.copy") };
            copy.AddToClassList("atsu-button");
            copy.AddToClassList("atsu-button--small");
            var close = new Button(() => _list.ClearSelection()) { text = "×" };
            close.AddToClassList("atsu-icon-button");
            detailBar.Add(_detailMeta);
            detailBar.Add(copy);
            detailBar.Add(close);
            _detail.Add(detailBar);
            var detailScroll = new ScrollView();
            detailScroll.AddToClassList("atsu-console__detail-scroll");
            _detailMessage = new Label();
            _detailMessage.AddToClassList("atsu-console__detail-message");
            _detailMessage.selection.isSelectable = true;
            _detailStack = new Label();
            _detailStack.AddToClassList("atsu-console__detail-stack");
            _detailStack.selection.isSelectable = true;
            detailScroll.Add(_detailMessage);
            detailScroll.Add(_detailStack);
            _detail.Add(detailScroll);
            root.Add(_detail);

            _status = new Label();
            _status.AddToClassList("atsu-console__status");
            _status.AddToClassList(UssClasses.Hidden);
            _status.selection.isSelectable = true;
            root.Add(_status);

            UpdateButtons();
            return root;
        }

        protected override VisualElement CreateToolbar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            var copyAll = new Button(CopyAll) { text = DebuggerStrings.Get("common.copyAll") };
            copyAll.AddToClassList("atsu-button");
            var clear = new Button(() =>
            {
                Debugger.Console.Clear();
                _list.ClearSelection();
            }) { text = DebuggerStrings.Get("common.clear") };
            clear.AddToClassList("atsu-button");
            _dockButton = new Button(() =>
            {
                var dock = DockedConsole;
                if (dock != null)
                {
                    dock.IsVisible = !dock.IsVisible;
                }
            });
            _dockButton.AddToClassList("atsu-button");
            _dockButton.tooltip = DebuggerStrings.Get("console.dockTooltip");
            var save = new Button(SaveToFile) { text = DebuggerStrings.Get("console.save") };
            save.AddToClassList("atsu-button");
            save.tooltip = DebuggerStrings.Get("console.saveTooltip");
            bar.Add(_dockButton);
            bar.Add(save);
            bar.Add(copyAll);
            bar.Add(clear);
            UpdateDockButton();
            return bar;
        }

        protected override void OnShow()
        {
            _seenVersion = -1;
            _follow = true;
            if (DockedConsole != null)
            {
                DockedConsole.Changed += UpdateDockButton;
            }

            UpdateDockButton();
            Refresh();
        }

        protected override void OnHide()
        {
            if (DockedConsole != null)
            {
                DockedConsole.Changed -= UpdateDockButton;
            }
        }

        private void UpdateDockButton()
        {
            if (_dockButton == null || Debugger == null)
            {
                return;
            }

            // The button exists only when the docked console feature is installed.
            _dockButton.EnableInClassList(UssClasses.Hidden, DockedConsole == null);
            var docked = DockedConsole?.IsVisible ?? false;
            _dockButton.text = DebuggerStrings.Get(docked ? "console.undock" : "console.dock");
            _dockButton.EnableInClassList("atsu-button--primary", docked);
        }

        protected override void OnUpdate()
        {
            if (_statusTimer > 0f)
            {
                _statusTimer -= UpdateInterval;
                if (_statusTimer <= 0f)
                {
                    _status?.AddToClassList(UssClasses.Hidden);
                }
            }

            Refresh();
        }

        // ---- Data -------------------------------------------------------------------------

        private void Refresh()
        {
            var buffer = Debugger.Console;
            if (buffer.Version == _seenVersion && !_filterDirty)
            {
                return;
            }

            var countsChanged = buffer.Version != _seenVersion;
            _seenVersion = buffer.Version;
            _filterDirty = false;

            buffer.CopyTo(_snapshot);
            Filter.Apply(_snapshot, _items);
            _list.RefreshItems();

            if (countsChanged)
            {
                UpdateButtons();
            }

            RestoreSelection();

            if (_follow && _items.Count > 0)
            {
                _scrollingProgrammatically = true;
                _list.ScrollToItem(_items.Count - 1);
                _scrollingProgrammatically = false;
            }
        }

        private void RestoreSelection()
        {
            if (_selectedSequence < 0)
            {
                return;
            }

            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Entry.Sequence != _selectedSequence)
                {
                    continue;
                }

                if (_list.selectedIndex != i)
                {
                    _selection[0] = i;
                    _list.SetSelectionWithoutNotify(_selection);
                }

                return;
            }

            // Selected entry was evicted / filtered out.
            _list.ClearSelection();
        }

        private void OnScrolled(float value)
        {
            if (_scrollingProgrammatically || _scroller == null)
            {
                return;
            }

            // Follow the tail only while the user is at the bottom.
            _follow = value >= _scroller.highValue - RowHeight * 0.5f;
        }

        // ---- Rows -------------------------------------------------------------------------

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("atsu-log");
            var bar = new VisualElement();
            bar.AddToClassList("atsu-log__severity");
            row.Add(bar);
            var body = new VisualElement();
            body.AddToClassList("atsu-log__body");
            var message = new Label();
            message.AddToClassList("atsu-log__message");
            var meta = new Label();
            meta.AddToClassList("atsu-log__meta");
            body.Add(message);
            body.Add(meta);
            row.Add(body);
            var count = new Label();
            count.AddToClassList("atsu-log__count");
            row.Add(count);
            row.userData = new RowParts(message, meta, count);
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            var item = _items[index];
            var e = item.Entry;
            row.EnableInClassList("atsu-log--info", e.Severity == LogSeverity.Info);
            row.EnableInClassList("atsu-log--warning", e.Severity == LogSeverity.Warning);
            row.EnableInClassList("atsu-log--error", e.Severity == LogSeverity.Error);
            // Strings come from per-entry caches, so rebinding rows (every refresh while logs flow) allocates nothing.
            var parts = (RowParts)row.userData;
            parts.Message.text = _text.FirstLine(e);
            parts.Meta.text = _text.Meta(e);
            parts.Count.text = item.Count > 1 ? NumberText.Get(item.Count) : string.Empty;
            parts.Count.EnableInClassList(UssClasses.Hidden, item.Count <= 1);
        }

        private sealed class RowParts
        {
            public readonly Label Count;
            public readonly Label Message;
            public readonly Label Meta;

            public RowParts(Label message, Label meta, Label count)
            {
                Message = message;
                Meta = meta;
                Count = count;
            }
        }

        /// <summary>"Log 12" style button text, formatted only when the count changes (recent results are reused).</summary>
        private sealed class CountText
        {
            private readonly Dictionary<int, string> _cache = new();
            private string _format;

            public string Get(string key, int count)
            {
                var format = DebuggerStrings.Get(key);
                if (!ReferenceEquals(format, _format))
                {
                    _format = format;
                    _cache.Clear();
                }

                if (_cache.TryGetValue(count, out var text))
                {
                    return text;
                }

                if (_cache.Count >= 256)
                {
                    _cache.Clear();
                }

                text = format.Replace("{0}", NumberText.Get(count));
                _cache.Add(count, text);
                return text;
            }
        }

        // ---- Detail -----------------------------------------------------------------------

        private void OnSelectionChanged()
        {
            var index = _list.selectedIndex;
            if (index < 0 || index >= _items.Count)
            {
                _selectedSequence = -1;
                _detail.AddToClassList(UssClasses.Hidden);
                return;
            }

            var item = _items[index];
            _selectedSequence = item.Entry.Sequence;
            _detail.RemoveFromClassList(UssClasses.Hidden);
            _detailMeta.text = $"{item.Entry.Timestamp:HH:mm:ss.fff}  {item.Entry.Type}" +
                               (item.Count > 1 ? $"  ×{item.Count}" : string.Empty);
            _detailMessage.text = item.Entry.Message;
            _detailStack.text = string.IsNullOrEmpty(item.Entry.StackTrace)
                ? DebuggerStrings.Get("console.noStackTrace")
                : item.Entry.StackTrace.TrimEnd();
        }

        private void CopySelected()
        {
            var index = _list.selectedIndex;
            if (index < 0 || index >= _items.Count)
            {
                return;
            }

            GUIUtility.systemCopyBuffer = Format(_items[index]);
        }

        /// <summary>Writes every log in the buffer to a file under persistentDataPath and shows the path (FR-CON-06).</summary>
        protected void SaveToFile()
        {
            try
            {
                var path = ConsoleExport.SaveToFile(Debugger.Console);
                ShowStatus(DebuggerStrings.Format("console.saved", path), false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowStatus(DebuggerStrings.Format("console.saveFailed", e.Message), true);
            }
        }

        private void ShowStatus(string text, bool error)
        {
            if (_status == null)
            {
                return;
            }

            _status.text = text;
            _status.EnableInClassList("atsu-console__status--error", error);
            _status.RemoveFromClassList(UssClasses.Hidden);
            _statusTimer = 6f;
        }

        private void CopyAll()
        {
            var sb = new StringBuilder();
            foreach (var item in _items)
            {
                sb.AppendLine(Format(item));
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        private static string Format(ConsoleFilter.Item item)
        {
            var e = item.Entry;
            var head = $"[{e.Timestamp:HH:mm:ss.fff}] [{e.Type}]" +
                       (item.Count > 1 ? $" (x{item.Count})" : string.Empty);
            return $"{head} {e.Message}\n{e.StackTrace}".TrimEnd();
        }

        // ---- Filter bar -------------------------------------------------------------------

        private Button SeverityButton(string cls, Action toggle)
        {
            var b = new Button(() =>
            {
                toggle();
                _filterDirty = true;
                UpdateButtons();
            });
            b.AddToClassList("atsu-button");
            b.AddToClassList("atsu-console__sev");
            b.AddToClassList(cls);
            return b;
        }

        private Button ToggleButton(string text, Action toggle)
        {
            var b = new Button(() =>
            {
                toggle();
                _filterDirty = true;
                UpdateButtons();
            }) { text = text };
            b.AddToClassList("atsu-button");
            b.AddToClassList("atsu-console__toggle");
            return b;
        }

        private void UpdateButtons()
        {
            if (_infoButton == null)
            {
                return;
            }

            var buffer = Debugger.Console;
            _infoButton.text = _infoText.Get("console.log", buffer.GetCount(LogSeverity.Info));
            _warningButton.text = _warningText.Get("console.warn", buffer.GetCount(LogSeverity.Warning));
            _errorButton.text = _errorText.Get("console.error", buffer.GetCount(LogSeverity.Error));
            _infoButton.EnableInClassList("atsu-toggle--off", !Filter.ShowInfo);
            _warningButton.EnableInClassList("atsu-toggle--off", !Filter.ShowWarning);
            _errorButton.EnableInClassList("atsu-toggle--off", !Filter.ShowError);
            _collapseButton.EnableInClassList("atsu-toggle--on", Filter.Collapse);
            _regexButton.EnableInClassList("atsu-toggle--on", Filter.UseRegex);
            _search.EnableInClassList("atsu-field--invalid", !Filter.IsRegexValid);
        }
    }
}