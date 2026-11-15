using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Rows of an option list rooted at one category (UI independent).
    ///     Options directly in the root come first, then each child category as a collapsible header.
    ///     Only one branch is open at a time: opening a category closes everything that is not its ancestor.
    ///     While a search filter is active, every matching branch is expanded instead.
    /// </summary>
    internal sealed class OptionListModel
    {
        public enum RowKind
        {
            Category,
            Entry
        }

        private readonly OptionRegistry _registry;

        /// <param name="rootPath">Category path of the list root ("" = the whole tree).</param>
        /// <param name="entriesOnly">Only the root's own options (the "General" section).</param>
        public OptionListModel(OptionRegistry registry, string rootPath, bool entriesOnly)
        {
            _registry = registry;
            RootPath = CategoryPath.Normalize(rootPath);
            EntriesOnly = entriesOnly;
        }

        public List<Row> Rows { get; } = new();
        public OptionTreeFilter Filter { get; private set; } = OptionTreeFilter.None;

        /// <summary>Path of the deepest open category; its ancestors are open too.</summary>
        public string OpenPath { get; private set; }

        /// <summary>Category path of the list root ("" = the whole tree).</summary>
        public string RootPath { get; }

        /// <summary>Only the root's own options are listed.</summary>
        public bool EntriesOnly { get; }

        public CategoryNode Root => RootPath.Length == 0 ? _registry.Root : _registry.FindCategory(RootPath);

        /// <summary>Raised when the user opens / closes a category (not when a removed category closes itself).</summary>
        public event Action OpenPathChanged;

        /// <summary>Restores the open branch (e.g. from persisted state). Unknown paths are ignored on refresh.</summary>
        public void SetOpenPath(string path)
        {
            OpenPath = string.IsNullOrEmpty(path) ? null : CategoryPath.Normalize(path);
            Refresh();
        }

        public void SetFilter(OptionTreeFilter filter)
        {
            Filter = filter ?? OptionTreeFilter.None;
            Refresh();
        }

        public bool IsExpanded(CategoryNode node)
        {
            var forced = Filter.GetExpanded(node);
            if (forced.HasValue)
            {
                return forced.Value;
            }

            return OpenPath != null && CategoryPath.IsSameOrDescendant(OpenPath, node.Path);
        }

        public void Toggle(CategoryNode node)
        {
            if (Filter.IsActive)
            {
                return; // expansion follows the search while it is active
            }

            if (IsExpanded(node))
            {
                var parent = node.Parent;
                OpenPath = parent != null && parent != Root && !parent.IsRoot ? parent.Path : null;
            }
            else
            {
                OpenPath = node.Path;
            }

            Refresh();
            OpenPathChanged?.Invoke();
        }

        public void Refresh()
        {
            Rows.Clear();
            var root = Root;
            if (root == null)
            {
                return;
            }

            if (OpenPath != null && _registry.FindCategory(OpenPath) == null)
            {
                OpenPath = null;
            }

            AppendEntries(root, 0);
            if (EntriesOnly)
            {
                return;
            }

            foreach (var child in root.Children)
            {
                AppendCategory(child, 0);
            }
        }

        private void AppendCategory(CategoryNode node, int depth)
        {
            if (!Filter.IsVisible(node))
            {
                return;
            }

            Rows.Add(new Row(node, depth));
            if (!IsExpanded(node))
            {
                return;
            }

            AppendEntries(node, depth + 1);
            foreach (var child in node.Children)
            {
                AppendCategory(child, depth + 1);
            }
        }

        private void AppendEntries(CategoryNode node, int depth)
        {
            foreach (var entry in node.Entries)
            {
                if (Filter.IsVisible(entry))
                {
                    Rows.Add(new Row(entry, depth));
                }
            }
        }

        public readonly struct Row
        {
            public readonly RowKind Kind;
            public readonly CategoryNode Node;
            public readonly OptionEntry Entry;
            public readonly int Depth;

            public Row(CategoryNode node, int depth)
            {
                Kind = RowKind.Category;
                Node = node;
                Entry = default;
                Depth = depth;
            }

            public Row(OptionEntry entry, int depth)
            {
                Kind = RowKind.Entry;
                Node = null;
                Entry = entry;
                Depth = depth;
            }

            public object Key => Kind == RowKind.Category ? Node : Entry.IsGroup ? Entry.Group : Entry.Option;
        }
    }
}