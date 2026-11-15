using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Search result expressed against the category tree: the tree keeps its shape, only branches that
    ///     contain a hit are visible and expanded. A hit on any group member makes the whole group visible.
    ///     This is a snapshot; rebuild it when the query or <see cref="OptionRegistry.Version" /> changes.
    /// </summary>
    public sealed class OptionTreeFilter
    {
        /// <summary>Inactive filter: everything visible, expansion left to the user.</summary>
        public static readonly OptionTreeFilter None = new(null, false, true, r => None);

        private readonly Dictionary<OptionDefinition, int> _hits = new();

        private readonly Func<OptionRegistry, OptionTreeFilter> _recreate;
        private readonly List<OptionSearchResult> _results = new();
        private readonly HashSet<CategoryNode> _visibleNodes = new();
        private readonly HashSet<OptionDefinition> _visibleOptions = new();

        private Func<OptionRegistry, OptionTreeFilter> _recreateOverride;

        private OptionTreeFilter(string query, bool isActive, bool highlightHits,
            Func<OptionRegistry, OptionTreeFilter> recreate)
        {
            Query = query;
            IsActive = isActive;
            HighlightHits = highlightHits;
            _recreate = recreate;
        }

        public string Query { get; }

        /// <summary>False when the query is empty: show the normal tree.</summary>
        public bool IsActive { get; }

        /// <summary>Direct hits, best first.</summary>
        public IReadOnlyList<OptionSearchResult> Results => _results;

        public int HitCount => _hits.Count;

        /// <summary>Whether matching rows should be highlighted (false for the Pinned tab: every row "matches").</summary>
        public bool HighlightHits { get; }

        /// <summary>Builds the same kind of filter again against the current tree (after options were added / removed).</summary>
        public OptionTreeFilter Recreate(OptionRegistry registry)
        {
            return (_recreateOverride ?? _recreate)(registry);
        }

        public static OptionTreeFilter Create(OptionRegistry registry, string query)
        {
            if (OptionSearch.ParseTerms(query).Length == 0)
            {
                return None;
            }

            var filter = new OptionTreeFilter(query, true, true, r => Create(r, query));
            registry.Search(query, filter._results);

            foreach (var r in filter._results)
            {
                filter._hits[r.Option] = r.Score;
                filter.AddVisible(registry, r.Option);
            }

            return filter;
        }

        /// <summary>Only pinned options (tree shape kept, branches expanded). Active even when nothing is pinned.</summary>
        public static OptionTreeFilter CreatePinned(OptionRegistry registry)
        {
            var filter = ForOptions(registry, registry.PinnedOptions);
            filter._recreateOverride = CreatePinned;
            return filter;
        }

        /// <summary>
        ///     Only the given options (tree shape kept, branches expanded, no highlight). The collection is read again
        ///     by <see cref="Recreate" />, so a live collection keeps the filter up to date. Unregistered options are skipped.
        /// </summary>
        public static OptionTreeFilter ForOptions(OptionRegistry registry,
            IReadOnlyCollection<OptionDefinition> options)
        {
            var filter = new OptionTreeFilter(null, true, false, r => ForOptions(r, options));
            foreach (var o in options)
            {
                if (!registry.Contains(o))
                {
                    continue;
                }

                filter._hits[o] = 0;
                filter.AddVisible(registry, o);
            }

            return filter;
        }

        private void AddVisible(OptionRegistry registry, OptionDefinition option)
        {
            var group = registry.GetGroup(option);
            if (group == null)
            {
                _visibleOptions.Add(option);
            }
            else
            {
                foreach (var m in group.Members)
                {
                    _visibleOptions.Add(m);
                }
            }

            for (var n = registry.GetCategory(option); n != null; n = n.Parent)
            {
                if (!_visibleNodes.Add(n))
                {
                    break; // ancestors already added
                }
            }
        }

        /// <summary>True if the option matched the query itself (use for highlighting).</summary>
        public bool IsHit(OptionDefinition option)
        {
            return !IsActive || _hits.ContainsKey(option);
        }

        public int GetScore(OptionDefinition option)
        {
            return _hits.TryGetValue(option, out var s) ? s : 0;
        }

        public bool IsVisible(OptionDefinition option)
        {
            return !IsActive || _visibleOptions.Contains(option);
        }

        public bool IsVisible(OptionGroup group)
        {
            if (!IsActive)
            {
                return true;
            }

            foreach (var m in group.Members)
            {
                if (_visibleOptions.Contains(m))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsVisible(OptionEntry entry)
        {
            return entry.IsGroup ? IsVisible(entry.Group) : IsVisible(entry.Option);
        }

        public bool IsVisible(CategoryNode node)
        {
            return !IsActive || _visibleNodes.Contains(node);
        }

        /// <summary>
        ///     Whether the category should be forced open. Returns null when the filter is inactive
        ///     (keep the user's own expand/collapse state).
        /// </summary>
        public bool? GetExpanded(CategoryNode node)
        {
            return IsActive ? _visibleNodes.Contains(node) : null;
        }
    }
}