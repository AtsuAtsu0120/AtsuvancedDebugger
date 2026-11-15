using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>Ordered category -> entries map shown in the System Info tab. Main-thread only.</summary>
    public sealed class SystemInfoRegistry
    {
        public const string DefaultCategory = "Default";

        private readonly List<string> _categoryOrder = new();
        private readonly Dictionary<string, List<InfoEntry>> _entries = new();

        public IReadOnlyList<string> Categories => _categoryOrder;

        public event Action Changed;

        public IReadOnlyList<InfoEntry> GetEntries(string category)
        {
            return _entries.TryGetValue(category, out var list) ? list : Array.Empty<InfoEntry>();
        }

        /// <summary>Adds an entry. An entry with the same title in the same category is replaced.</summary>
        public void Add(InfoEntry entry, string category = DefaultCategory)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (string.IsNullOrEmpty(category))
            {
                category = DefaultCategory;
            }

            if (!_entries.TryGetValue(category, out var list))
            {
                list = new List<InfoEntry>();
                _entries.Add(category, list);
                _categoryOrder.Add(category);
            }

            var index = list.FindIndex(e => e.Title == entry.Title);
            if (index >= 0)
            {
                list[index] = entry;
            }
            else
            {
                list.Add(entry);
            }

            Changed?.Invoke();
        }

        public bool Remove(string title, string category = DefaultCategory)
        {
            if (!_entries.TryGetValue(category, out var list))
            {
                return false;
            }

            if (list.RemoveAll(e => e.Title == title) == 0)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }
    }
}