using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     A node of the option category tree. Nodes are created when the first option under them is added
    ///     and removed automatically when they become empty (no options and no child categories).
    /// </summary>
    public sealed class CategoryNode
    {
        private readonly List<CategoryNode> _children = new();
        private readonly List<OptionEntry> _entries = new();
        private readonly Dictionary<string, OptionGroup> _groups = new();
        private readonly List<OptionDefinition> _options = new();
        private bool _entriesDirty;

        internal CategoryNode(string name, string path, CategoryNode parent, int depth)
        {
            Name = name;
            Path = path;
            Parent = parent;
            Depth = depth;
        }

        /// <summary>Segment name ("Status" for "Player/Status"). Empty for the root.</summary>
        public string Name { get; }

        /// <summary>Full path ("Player/Status"). Empty for the root.</summary>
        public string Path { get; }

        public CategoryNode Parent { get; private set; }

        /// <summary>0 for the root, 1 for top-level categories, ...</summary>
        public int Depth { get; }

        public bool IsRoot => Parent == null && Depth == 0;

        /// <summary>
        ///     Display order among siblings (lower first). Siblings with the same value keep the order in which they first
        ///     appeared.
        ///     Set with <see cref="OptionRegistry.SetCategorySortPriority" /> or <see cref="OptionCategorySortAttribute" />.
        /// </summary>
        public int SortPriority { get; internal set; }

        /// <summary>Child categories ordered by <see cref="SortPriority" />, then by first appearance.</summary>
        public IReadOnlyList<CategoryNode> Children => _children;

        /// <summary>Options directly in this category, ordered by SortPriority (stable).</summary>
        public IReadOnlyList<OptionDefinition> Options => _options;

        /// <summary>
        ///     Display units of this category in order: ungrouped options and groups.
        ///     A group is placed where its first member (lowest SortPriority) would be.
        /// </summary>
        public IReadOnlyList<OptionEntry> Entries
        {
            get
            {
                if (_entriesDirty)
                {
                    RebuildEntries();
                }

                return _entries;
            }
        }

        public IEnumerable<OptionGroup> Groups => _groups.Values;

        /// <summary>Number of options in this category and all descendants.</summary>
        public int TotalOptionCount { get; private set; }

        internal bool IsEmpty => _options.Count == 0 && _children.Count == 0;

        public OptionGroup FindGroup(string key)
        {
            return key != null && _groups.TryGetValue(key, out var g) ? g : null;
        }

        public CategoryNode FindChild(string name)
        {
            foreach (var c in _children)
            {
                if (c.Name == name)
                {
                    return c;
                }
            }

            return null;
        }

        /// <summary>Visits every option in this subtree, depth-first, in display order.</summary>
        public void CollectOptions(List<OptionDefinition> destination)
        {
            destination.AddRange(_options);
            foreach (var c in _children)
            {
                c.CollectOptions(destination);
            }
        }

        /// <summary>Inserts after every child with the same or a lower <see cref="SortPriority" />.</summary>
        internal void AddChild(CategoryNode child)
        {
            var index = _children.Count;
            for (var i = 0; i < _children.Count; i++)
            {
                if (_children[i].SortPriority > child.SortPriority)
                {
                    index = i;
                    break;
                }
            }

            _children.Insert(index, child);
        }

        /// <summary>Re-sorts the children by <see cref="SortPriority" /> (stable). Returns true if the order changed.</summary>
        internal bool SortChildren()
        {
            var changed = false;
            // Insertion sort: stable and allocation free; the lists are short.
            for (var i = 1; i < _children.Count; i++)
            {
                var item = _children[i];
                var j = i - 1;
                while (j >= 0 && _children[j].SortPriority > item.SortPriority)
                {
                    _children[j + 1] = _children[j];
                    j--;
                }

                if (j + 1 == i)
                {
                    continue;
                }

                _children[j + 1] = item;
                changed = true;
            }

            return changed;
        }

        internal void RemoveChild(CategoryNode child)
        {
            _children.Remove(child);
            child.Parent = null;
        }

        /// <summary>Inserts the option; returns the group it joined (and whether that group was just created).</summary>
        internal OptionGroup InsertOption(OptionDefinition option, out bool groupCreated)
        {
            groupCreated = false;
            // Stable insert by SortPriority.
            var index = _options.Count;
            for (var i = 0; i < _options.Count; i++)
            {
                if (_options[i].SortPriority > option.SortPriority)
                {
                    index = i;
                    break;
                }
            }

            _options.Insert(index, option);
            for (var n = this; n != null; n = n.Parent)
            {
                n.TotalOptionCount++;
            }

            _entriesDirty = true;

            var key = option.GroupKey;
            if (key == null)
            {
                return null;
            }

            if (!_groups.TryGetValue(key, out var group))
            {
                group = new OptionGroup(key, this);
                _groups.Add(key, group);
                groupCreated = true;
            }

            group.AddMember(option);
            return group;
        }

        /// <summary>Removes the option; <paramref name="removedGroup" /> is set if its group became empty.</summary>
        internal bool RemoveOption(OptionDefinition option, out OptionGroup removedGroup)
        {
            removedGroup = null;
            if (!_options.Remove(option))
            {
                return false;
            }

            for (var n = this; n != null; n = n.Parent)
            {
                n.TotalOptionCount--;
            }

            _entriesDirty = true;

            var group = FindGroup(option.GroupKey);
            if (group != null && group.RemoveMember(option) && group.Members.Count == 0)
            {
                _groups.Remove(group.Key);
                removedGroup = group;
            }

            return true;
        }

        private void RebuildEntries()
        {
            _entries.Clear();
            HashSet<OptionGroup> emitted = null;
            foreach (var o in _options)
            {
                var group = FindGroup(o.GroupKey);
                if (group == null)
                {
                    _entries.Add(new OptionEntry(o));
                    continue;
                }

                emitted ??= new HashSet<OptionGroup>();
                if (emitted.Add(group))
                {
                    _entries.Add(new OptionEntry(group));
                }
            }

            _entriesDirty = false;
        }

        public override string ToString()
        {
            return IsRoot ? "<root>" : Path;
        }
    }
}