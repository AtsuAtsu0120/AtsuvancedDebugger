using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Several options displayed and operated as one unit (e.g. an input field + the button that submits it).
    ///     Created and destroyed automatically by <see cref="OptionRegistry" /> as members come and go.
    ///     The UI should read and write member values through the group so that the commit mode is honoured.
    /// </summary>
    public sealed class OptionGroup
    {
        private readonly List<OptionDefinition> _members = new();
        private readonly Dictionary<OptionDefinition, object> _pending = new();

        internal OptionGroup(string key, CategoryNode category)
        {
            Key = key;
            Category = category;
            Label = key;
        }

        public string Key { get; }
        public CategoryNode Category { get; }

        /// <summary>"Category/Path/Key".</summary>
        public string Id => Category.IsRoot ? Key : Category.Path + CategoryPath.Separator + Key;

        public string Label { get; private set; }
        public OptionGroupLayout Layout { get; private set; } = OptionGroupLayout.Horizontal;
        public OptionGroupCommit Commit { get; private set; } = OptionGroupCommit.Auto;

        /// <summary>Members ordered by SortPriority (stable).</summary>
        public IReadOnlyList<OptionDefinition> Members => _members;

        public int SortPriority => _members.Count == 0 ? 0 : _members[0].SortPriority;

        /// <summary>Commit mode after resolving <see cref="OptionGroupCommit.Auto" />.</summary>
        public OptionGroupCommit EffectiveCommit
        {
            get
            {
                if (Commit != OptionGroupCommit.Auto)
                {
                    return Commit;
                }

                var hasWritable = false;
                var hasMethod = false;
                foreach (var m in _members)
                {
                    if (m.Kind == OptionKind.Method)
                    {
                        hasMethod = true;
                    }
                    else if (m.CanWrite)
                    {
                        hasWritable = true;
                    }
                }

                return hasWritable && hasMethod ? OptionGroupCommit.OnAction : OptionGroupCommit.Immediate;
            }
        }

        // ---- Values ---------------------------------------------------------------------

        public bool HasPendingChanges => _pending.Count > 0;

        /// <summary>Raised when members are added or removed.</summary>
        public event Action<OptionGroup> MembersChanged;

        /// <summary>Raised when pending values change (set, applied or discarded).</summary>
        public event Action<OptionGroup> PendingChanged;

        public bool Contains(OptionDefinition option)
        {
            return _members.Contains(option);
        }

        public bool HasPending(OptionDefinition option)
        {
            return _pending.ContainsKey(option);
        }

        /// <summary>The pending value if any, otherwise the current value.</summary>
        public object GetDisplayValue(OptionDefinition option)
        {
            EnsureMember(option);
            return _pending.TryGetValue(option, out var v) ? v : option.GetValue();
        }

        /// <summary>Writes immediately or stores a pending value depending on <see cref="EffectiveCommit" />.</summary>
        public void SetValue(OptionDefinition option, object value)
        {
            EnsureMember(option);
            if (option.Kind != OptionKind.Property)
            {
                throw new InvalidOperationException($"'{option.Name}' is not a property.");
            }

            if (EffectiveCommit == OptionGroupCommit.OnAction)
            {
                if (!option.CanWrite)
                {
                    throw new InvalidOperationException($"Option '{option.Name}' is read-only.");
                }

                _pending[option] = value;
                PendingChanged?.Invoke(this);
            }
            else
            {
                option.SetValue(value);
            }
        }

        /// <summary>Writes all pending values to their properties in member order.</summary>
        public void Apply()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            foreach (var m in _members)
            {
                if (_pending.TryGetValue(m, out var v))
                {
                    m.SetValue(v);
                }
            }

            _pending.Clear();
            PendingChanged?.Invoke(this);
        }

        public void DiscardPending()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            _pending.Clear();
            PendingChanged?.Invoke(this);
        }

        /// <summary>Applies pending values, then invokes the method member (the "submit" button).</summary>
        public void Invoke(OptionDefinition action)
        {
            EnsureMember(action);
            if (action.Kind != OptionKind.Method)
            {
                throw new InvalidOperationException($"'{action.Name}' is not a method.");
            }

            Apply();
            action.Invoke();
        }

        // ---- Membership (registry only) ---------------------------------------------------

        internal void AddMember(OptionDefinition option)
        {
            var index = _members.Count;
            for (var i = 0; i < _members.Count; i++)
            {
                if (_members[i].SortPriority > option.SortPriority)
                {
                    index = i;
                    break;
                }
            }

            _members.Insert(index, option);
            MergeSettings(option.GroupSettings);
            MembersChanged?.Invoke(this);
        }

        internal bool RemoveMember(OptionDefinition option)
        {
            if (!_members.Remove(option))
            {
                return false;
            }

            if (_pending.Remove(option))
            {
                PendingChanged?.Invoke(this);
            }

            RebuildSettings();
            MembersChanged?.Invoke(this);
            return true;
        }

        private void MergeSettings(OptionGroupAttribute s)
        {
            if (s == null)
            {
                return;
            }

            if (Label == Key && !string.IsNullOrEmpty(s.Label))
            {
                Label = s.Label;
            }

            if (Layout == OptionGroupLayout.Horizontal && s.Layout != OptionGroupLayout.Horizontal)
            {
                Layout = s.Layout;
            }

            if (Commit == OptionGroupCommit.Auto && s.Commit != OptionGroupCommit.Auto)
            {
                Commit = s.Commit;
            }
        }

        private void RebuildSettings()
        {
            Label = Key;
            Layout = OptionGroupLayout.Horizontal;
            Commit = OptionGroupCommit.Auto;
            foreach (var m in _members)
            {
                MergeSettings(m.GroupSettings);
            }
        }

        private void EnsureMember(OptionDefinition option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            if (!_members.Contains(option))
            {
                throw new ArgumentException($"'{option.Name}' is not a member of group '{Id}'.", nameof(option));
            }
        }

        public override string ToString()
        {
            return $"Group:{Id}({_members.Count})";
        }
    }

    /// <summary>One display unit in a category: a single option or a group.</summary>
    public readonly struct OptionEntry
    {
        public readonly OptionDefinition Option;
        public readonly OptionGroup Group;

        public bool IsGroup => Group != null;
        public int SortPriority => Group?.SortPriority ?? Option.SortPriority;

        public OptionEntry(OptionDefinition option)
        {
            Option = option;
            Group = null;
        }

        public OptionEntry(OptionGroup group)
        {
            Option = null;
            Group = group;
        }

        public override string ToString()
        {
            return IsGroup ? Group.ToString() : Option.ToString();
        }
    }
}