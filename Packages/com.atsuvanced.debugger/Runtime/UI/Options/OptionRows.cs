using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>One display unit in a category (single option or group).</summary>
    internal interface IOptionEntryView
    {
        VisualElement Element { get; }
        void Refresh();
        void UpdatePinState();

        /// <summary>Marks search hits. Visibility is decided by <see cref="OptionListModel" /> (only visible rows are bound).</summary>
        void ApplyHighlight(OptionTreeFilter filter);

        void Dispose();
    }

    /// <summary>Small round pin toggle (drawn with USS, no font glyph needed).</summary>
    internal sealed class PinButton : VisualElement
    {
        public PinButton(Action clicked)
        {
            AddToClassList("atsu-pin");
            var dot = new VisualElement();
            dot.AddToClassList("atsu-pin__dot");
            Add(dot);
            tooltip = DebuggerStrings.Get("options.pin");
            this.AddManipulator(new Clickable(clicked));
        }

        public void SetPinned(bool pinned)
        {
            EnableInClassList("atsu-pin--on", pinned);
        }
    }

    /// <summary>Row for a single option. Also used (compact) for group members.</summary>
    internal sealed class OptionRow : VisualElement, IOptionEntryView
    {
        private readonly PinButton _pin;
        private readonly OptionRegistry _registry;
        private readonly OptionView _view;
        private bool _refreshFailed;

        /// <param name="compact">Group member layout (no pin button / description).</param>
        public OptionRow(OptionDefinition definition, OptionGroup group, OptionRegistry registry,
            OptionViewRegistry views,
            bool compact, OptionPlacement placement = OptionPlacement.Panel)
        {
            _registry = registry;
            Context = new OptionContext(definition, group, placement);
            EnableInClassList("atsu-option--pinned", placement == OptionPlacement.Pinned && !compact);
            AddToClassList("atsu-option");
            EnableInClassList("atsu-option--compact", compact);

            VisualElement element;
            try
            {
                _view = views.Create(definition);
                element = _view.CreateElement(Context) ?? new VisualElement();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _view = null;
                element = DebugPage.CreateErrorElement(DebuggerStrings.Format("error.option", definition.Name), e);
            }

            var main = new VisualElement();
            main.AddToClassList("atsu-option__main");
            Add(main);

            if (_view == null || _view.ShowLabel)
            {
                var label = new Label(definition.Name);
                label.AddToClassList("atsu-option__label");
                main.Add(label);
                AddToClassList("atsu-option--labeled");
            }

            element.AddToClassList("atsu-option__control");
            main.Add(element);

            if (!compact && placement == OptionPlacement.Panel)
            {
                _pin = new PinButton(TogglePin);
                main.Add(_pin);
                if (!string.IsNullOrEmpty(definition.Description))
                {
                    var description = new Label(definition.Description);
                    description.AddToClassList("atsu-option__description");
                    Add(description);
                }
            }

            definition.ValueChanged += OnValueChanged;
            UpdatePinState();
            Refresh();
        }

        public OptionContext Context { get; }
        public VisualElement Element => this;

        public void Refresh()
        {
            EnableInClassList("atsu-option--pending", Context.IsPending);
            if (_view == null || _refreshFailed)
            {
                return;
            }

            if (Context.Definition.Kind == OptionKind.Property && !Context.Definition.CanRead)
            {
                return;
            }

            try
            {
                _view.Refresh(Context);
            }
            catch (Exception e)
            {
                _refreshFailed = true; // log once, then stop polling this row
                Debug.LogException(e);
            }
        }

        public void UpdatePinState()
        {
            _pin?.SetPinned(_registry.IsPinned(Context.Definition));
        }

        public void ApplyHighlight(OptionTreeFilter filter)
        {
            EnableInClassList("atsu-option--hit",
                filter.IsActive && filter.HighlightHits && filter.IsHit(Context.Definition));
        }

        public void Dispose()
        {
            Context.Definition.ValueChanged -= OnValueChanged;
            try
            {
                _view?.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnValueChanged(OptionDefinition _)
        {
            // Rows stay cached (and subscribed) while their page is hidden or scrolled away; those are refreshed when
            // they are bound / shown again, so skip them here (games may notify every frame).
            if (IsDisplayed())
            {
                Refresh();
            }
        }

        private bool IsDisplayed()
        {
            if (panel == null)
            {
                return false;
            }

            for (VisualElement e = this; e != null; e = e.hierarchy.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.ClassListContains(UssClasses.Hidden))
                {
                    return false;
                }
            }

            return true;
        }

        private void TogglePin()
        {
            if (_registry.IsPinned(Context.Definition))
            {
                _registry.Unpin(Context.Definition);
            }
            else
            {
                _registry.Pin(Context.Definition);
            }
        }
    }

    /// <summary>Row for an <see cref="OptionGroup" />: header (label, pending state, pin) + member controls.</summary>
    internal sealed class OptionGroupRow : VisualElement, IOptionEntryView
    {
        private readonly OptionGroup _group;
        private readonly VisualElement _members;
        private readonly VisualElement _pendingBadge;
        private readonly PinButton _pin;

        private readonly OptionPlacement _placement;
        private readonly OptionRegistry _registry;
        private readonly List<OptionRow> _rows = new();
        private readonly Label _title;
        private readonly OptionViewRegistry _views;

        public OptionGroupRow(OptionGroup group, OptionRegistry registry, OptionViewRegistry views,
            OptionPlacement placement = OptionPlacement.Panel)
        {
            _group = group;
            _registry = registry;
            _views = views;
            _placement = placement;
            AddToClassList("atsu-option-group");
            EnableInClassList("atsu-option-group--pinned", placement == OptionPlacement.Pinned);

            var header = new VisualElement();
            header.AddToClassList("atsu-option-group__header");
            _title = new Label(group.Label);
            _title.AddToClassList("atsu-option-group__title");
            header.Add(_title);

            _pendingBadge = new VisualElement();
            _pendingBadge.AddToClassList("atsu-option-group__pending");
            var pendingLabel = new Label(DebuggerStrings.Get("options.pending"));
            _pendingBadge.Add(pendingLabel);
            var revert = new Button(group.DiscardPending) { text = DebuggerStrings.Get("options.revert") };
            revert.AddToClassList("atsu-button");
            revert.AddToClassList("atsu-button--small");
            _pendingBadge.Add(revert);
            header.Add(_pendingBadge);

            if (placement == OptionPlacement.Panel)
            {
                _pin = new PinButton(TogglePin);
                header.Add(_pin);
            }

            Add(header);

            _members = new VisualElement();
            _members.AddToClassList("atsu-option-group__members");
            Add(_members);

            group.MembersChanged += OnMembersChanged;
            group.PendingChanged += OnPendingChanged;
            BuildMembers();
            UpdatePinState();
            Refresh();
        }

        public VisualElement Element => this;

        public void Refresh()
        {
            _pendingBadge.EnableInClassList(UssClasses.Hidden, !_group.HasPendingChanges);
            EnableInClassList("atsu-option-group--pending", _group.HasPendingChanges);
            foreach (var r in _rows)
            {
                r.Refresh();
            }
        }

        public void UpdatePinState()
        {
            _pin?.SetPinned(_group.Members.Count > 0 && _registry.IsPinned(_group.Members[0]));
        }

        public void ApplyHighlight(OptionTreeFilter filter)
        {
            foreach (var r in _rows)
            {
                r.ApplyHighlight(filter);
            }
        }

        public void Dispose()
        {
            _group.MembersChanged -= OnMembersChanged;
            _group.PendingChanged -= OnPendingChanged;
            foreach (var r in _rows)
            {
                r.Dispose();
            }

            _rows.Clear();
        }

        private void TogglePin()
        {
            if (_group.Members.Count == 0)
            {
                return;
            }

            var first = _group.Members[0];
            if (_registry.IsPinned(first))
            {
                _registry.Unpin(first);
            }
            else
            {
                _registry.Pin(first);
            }
        }

        private void BuildMembers()
        {
            foreach (var r in _rows)
            {
                r.Dispose();
            }

            _rows.Clear();
            _members.Clear();
            _members.EnableInClassList("atsu-option-group__members--vertical",
                _group.Layout == OptionGroupLayout.Vertical);
            _title.text = _group.Label;

            foreach (var m in _group.Members)
            {
                var row = new OptionRow(m, _group, _registry, _views, true, _placement);
                _rows.Add(row);
                _members.Add(row);
            }
        }

        private void OnMembersChanged(OptionGroup _)
        {
            BuildMembers();
        }

        private void OnPendingChanged(OptionGroup _)
        {
            Refresh();
        }
    }
}