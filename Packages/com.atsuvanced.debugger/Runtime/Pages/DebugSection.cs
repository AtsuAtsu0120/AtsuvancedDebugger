using System;
using System.Collections;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     One entry of a tab's left list. Selecting it shows its page in the tab's content area.
    ///     Built-in tabs (System, option categories, Console ...) are made of sections too, so users can add their own
    ///     sections to any tab: <c>AtsuDebug.Instance.Tabs.Find("system").Sections.Add(...)</c>.
    /// </summary>
    public sealed class DebugSection
    {
        public const int DefaultOrder = 1000;

        public DebugSection(string id, string title, Func<DebugPage> createPage, int order = DefaultOrder,
            Func<string> badge = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Section id is required.", nameof(id));
            }

            Id = id;
            Title = string.IsNullOrEmpty(title) ? id : title;
            CreatePage = createPage ?? throw new ArgumentNullException(nameof(createPage));
            Order = order;
            Badge = badge;
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>Sections are sorted by this value, then by registration order.</summary>
        public int Order { get; }

        /// <summary>Creates the section's page. Called the first time the section is selected.</summary>
        public Func<DebugPage> CreatePage { get; }

        /// <summary>Optional short text shown next to the title (e.g. an item count). Evaluated when the list refreshes.</summary>
        public Func<string> Badge { get; }

        public override string ToString()
        {
            return $"Section:{Id}";
        }
    }

    /// <summary>Ordered, observable list of sections. Main-thread only.</summary>
    public sealed class DebugSectionCollection : IReadOnlyList<DebugSection>
    {
        private readonly List<DebugSection> _items = new();

        public int Count => _items.Count;
        public DebugSection this[int index] => _items[index];

        public IEnumerator<DebugSection> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public event Action Changed;

        /// <summary>Adds a section. A section with the same id is replaced.</summary>
        public void Add(DebugSection section)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            _items.RemoveAll(s => s.Id == section.Id);

            var index = _items.Count;
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Order > section.Order)
                {
                    index = i;
                    break;
                }
            }

            _items.Insert(index, section);
            Changed?.Invoke();
        }

        public bool Remove(string id)
        {
            if (_items.RemoveAll(s => s.Id == id) == 0)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        public DebugSection Find(string id)
        {
            foreach (var s in _items)
            {
                if (s.Id == id)
                {
                    return s;
                }
            }

            return null;
        }

        public bool Contains(string id)
        {
            return Find(id) != null;
        }

        /// <summary>
        ///     Puts the items with the given ids into this relative order, keeping the slots they occupy
        ///     (other items do not move). Returns true and raises Changed if anything moved.
        /// </summary>
        public bool Reorder(IReadOnlyList<string> ids)
        {
            var slots = new List<int>();
            var items = new List<DebugSection>();
            foreach (var id in ids)
            {
                var index = _items.FindIndex(x => x.Id == id);
                if (index < 0)
                {
                    continue;
                }

                slots.Add(index);
                items.Add(_items[index]);
            }

            slots.Sort();
            var changed = false;
            for (var i = 0; i < slots.Count; i++)
            {
                if (ReferenceEquals(_items[slots[i]], items[i]))
                {
                    continue;
                }

                _items[slots[i]] = items[i];
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }
    }
}