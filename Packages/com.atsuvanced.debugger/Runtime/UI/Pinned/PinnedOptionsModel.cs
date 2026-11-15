using System.Collections.Generic;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Pinned entries in display (tree) order, UI independent. A group is one entry when any member is pinned
    ///     (pinning a member pins the whole group anyway).
    /// </summary>
    internal sealed class PinnedOptionsModel
    {
        private readonly OptionRegistry _registry;

        public PinnedOptionsModel(OptionRegistry registry)
        {
            _registry = registry;
        }

        public List<OptionEntry> Entries { get; } = new();

        public void Refresh()
        {
            Entries.Clear();
            if (_registry.PinnedOptions.Count == 0)
            {
                return;
            }

            Collect(_registry.Root);
        }

        private void Collect(CategoryNode node)
        {
            foreach (var entry in node.Entries)
            {
                if (entry.IsGroup)
                {
                    foreach (var m in entry.Group.Members)
                    {
                        if (!_registry.IsPinned(m))
                        {
                            continue;
                        }

                        Entries.Add(entry);
                        break;
                    }
                }
                else if (_registry.IsPinned(entry.Option))
                {
                    Entries.Add(entry);
                }
            }

            foreach (var child in node.Children)
            {
                Collect(child);
            }
        }
    }
}