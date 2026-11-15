using System;
using System.Collections.Generic;
using System.ComponentModel;
using Atsuvanced.Debugger.Internal;
using Object = UnityEngine.Object;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Holds every registered option (from containers or added directly), the category tree and the pin state.
    ///     Options, containers and categories can be added/removed at any time; the tree is kept up to date
    ///     incrementally and change events are raised so the UI can patch itself instead of rebuilding.
    ///     Main-thread only.
    /// </summary>
    public sealed class OptionRegistry
    {
        private readonly Dictionary<string, int> _categorySort = new();

        private readonly Dictionary<object, ContainerEntry> _containers = new(ReferenceComparer.Instance);

        // ---- Value types ------------------------------------------------------------------

        private readonly HashSet<Type> _extraValueTypes = new();

        private readonly Dictionary<OptionDefinition, CategoryNode> _optionNodes = new();

        private readonly List<OptionDefinition> _options = new();

        private readonly Dictionary<OptionDefinition, ContainerEntry> _owners = new();

        private readonly HashSet<OptionDefinition> _pinned = new();
        private int _unityObjectContainers;

        /// <summary>Root of the category tree. Its direct children are the top-level categories.</summary>
        public CategoryNode Root { get; } = new(string.Empty, string.Empty, null, 0);

        /// <summary>All options in registration order. Use <see cref="Root" /> for display order.</summary>
        public IReadOnlyList<OptionDefinition> Options => _options;

        public IReadOnlyCollection<OptionDefinition> PinnedOptions => _pinned;

        /// <summary>Incremented on every structural change (option / category added or removed).</summary>
        public int Version { get; private set; }

        public event Action<OptionDefinition> OptionAdded;
        public event Action<OptionDefinition> OptionRemoved;
        public event Action<CategoryNode> CategoryAdded;

        /// <summary>Raised after the node is detached; <see cref="CategoryNode.Parent" /> is already null.</summary>
        public event Action<CategoryNode> CategoryRemoved;

        /// <summary>
        ///     Raised when an option is pinned / unpinned. When an option is unregistered it is unpinned too;
        ///     in that case <see cref="Contains" /> is already false for it.
        /// </summary>
        public event Action<OptionDefinition, bool> PinStateChanged;

        /// <summary>Raised by <see cref="ClearPinned" /> (after the per-option <see cref="PinStateChanged" /> events).</summary>
        public event Action PinsCleared;

        /// <summary>
        ///     Raised when the children of a category were re-ordered (<see cref="SetCategorySortPriority" />). The argument
        ///     is the parent.
        /// </summary>
        public event Action<CategoryNode> CategoriesReordered;

        /// <summary>Raised when the first member of a group is registered.</summary>
        public event Action<OptionGroup> GroupAdded;

        /// <summary>Raised when the last member of a group is removed.</summary>
        public event Action<OptionGroup> GroupRemoved;

        /// <summary>
        ///     Accepts properties of <paramref name="type" /> as options when containers are scanned (beyond the built-in types of
        ///     <see cref="OptionScanner.IsSupportedType" />). <see cref="OptionViewRegistry.Register(Type, Func{OptionView})" />
        ///     calls this for you. Containers added earlier are not rescanned.
        /// </summary>
        public void AddValueType(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            _extraValueTypes.Add(type);
        }

        public bool RemoveValueType(Type type)
        {
            return type != null && _extraValueTypes.Remove(type);
        }

        /// <summary>Whether properties of <paramref name="type" /> become options when a container is scanned.</summary>
        public bool IsValueTypeSupported(Type type)
        {
            return type != null && (OptionScanner.IsSupportedType(type) || _extraValueTypes.Contains(type));
        }

        // ---- Containers -----------------------------------------------------------------

        /// <summary>
        ///     Registers a container. Its options are placed under <paramref name="categoryPrefix" /> if given
        ///     (e.g. "Enemies/Goblin#3"), which makes it easy to register many instances of the same class.
        /// </summary>
        public void AddContainer(object container, string categoryPrefix = null)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (_containers.ContainsKey(container))
            {
                return;
            }

            var entry = new ContainerEntry
            {
                Container = container,
                CategoryPrefix = CategoryPath.Normalize(categoryPrefix),
                Options = new List<OptionDefinition>()
            };

            if (container is IOptionContainer explicitContainer)
            {
                entry.Options.AddRange(explicitContainer.GetOptions());
                if (explicitContainer.IsDynamic)
                {
                    entry.AddedHandler = o => AddOwned(entry, o);
                    entry.RemovedHandler = o => RemoveInternal(o);
                    explicitContainer.OptionAdded += entry.AddedHandler;
                    explicitContainer.OptionRemoved += entry.RemovedHandler;
                }
            }
            else
            {
                entry.Options.AddRange(OptionScanner.Scan(container, IsValueTypeSupported));
                foreach (var (path, priority) in OptionScanner.GetCategorySorts(container.GetType()))
                {
                    SetCategorySortPriority(CategoryPath.Combine(entry.CategoryPrefix, path), priority);
                }
            }

            if (container is INotifyPropertyChanged npc)
            {
                entry.PropertyChangedHandler = (_, e) => OnContainerPropertyChanged(entry, e.PropertyName);
                npc.PropertyChanged += entry.PropertyChangedHandler;
            }

            _containers.Add(container, entry);
            if (container is Object)
            {
                _unityObjectContainers++;
            }

            var initial = entry.Options.ToArray();
            entry.Options.Clear();
            foreach (var o in initial)
            {
                AddOwned(entry, o);
            }
        }

        public bool RemoveContainer(object container)
        {
            if (container == null || !_containers.TryGetValue(container, out var entry))
            {
                return false;
            }

            _containers.Remove(container);
            if (container is Object)
            {
                _unityObjectContainers--;
            }

            if (entry.PropertyChangedHandler != null && container is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged -= entry.PropertyChangedHandler;
            }

            if (container is IOptionContainer explicitContainer && entry.AddedHandler != null)
            {
                explicitContainer.OptionAdded -= entry.AddedHandler;
                explicitContainer.OptionRemoved -= entry.RemovedHandler;
            }

            foreach (var o in entry.Options.ToArray())
            {
                RemoveInternal(o);
            }

            return true;
        }

        public bool ContainsContainer(object container)
        {
            return container != null && _containers.ContainsKey(container);
        }

        /// <summary>
        ///     Unregisters containers that are destroyed <see cref="UnityEngine.Object" />s (FR-OPT-14).
        ///     Called periodically by the debugger; returns the number of removed containers.
        /// </summary>
        public int RemoveDestroyedContainers()
        {
            if (_unityObjectContainers == 0)
            {
                return 0;
            }

            List<object> destroyed = null;
            foreach (var container in _containers.Keys)
            {
                if (container is Object uo && !uo)
                {
                    (destroyed ??= new List<object>()).Add(container);
                }
            }

            if (destroyed == null)
            {
                return 0;
            }

            foreach (var c in destroyed)
            {
                RemoveContainer(c);
            }

            return destroyed.Count;
        }

        // ---- Single options -------------------------------------------------------------

        public void AddOption(OptionDefinition option, string categoryPrefix = null)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            if (_optionNodes.ContainsKey(option))
            {
                return;
            }

            option.SetCategoryPrefix(categoryPrefix);
            AddInternal(option);
        }

        public bool RemoveOption(OptionDefinition option)
        {
            return option != null && RemoveInternal(option);
        }

        /// <summary>True if the option is currently registered.</summary>
        public bool Contains(OptionDefinition option)
        {
            return option != null && _optionNodes.ContainsKey(option);
        }

        // ---- Categories -----------------------------------------------------------------

        /// <summary>Finds a category node by path ("Player/Status"). Returns null if it does not exist.</summary>
        public CategoryNode FindCategory(string path)
        {
            var node = Root;
            foreach (var segment in CategoryPath.Split(path))
            {
                node = node.FindChild(segment);
                if (node == null)
                {
                    return null;
                }
            }

            return node;
        }

        /// <summary>
        ///     Removes every option in the category and its descendants (the nodes disappear once empty).
        ///     Returns the number of removed options.
        /// </summary>
        public int RemoveCategory(string path)
        {
            var node = FindCategory(path);
            if (node == null)
            {
                return 0;
            }

            var options = new List<OptionDefinition>(node.TotalOptionCount);
            node.CollectOptions(options);
            foreach (var o in options)
            {
                RemoveInternal(o);
            }

            return options.Count;
        }

        /// <summary>The category node the option is displayed in, or null if not registered.</summary>
        public CategoryNode GetCategory(OptionDefinition option)
        {
            return option != null && _optionNodes.TryGetValue(option, out var node) ? node : null;
        }

        // ---- Groups ---------------------------------------------------------------------

        /// <summary>The group the option belongs to, or null.</summary>
        public OptionGroup GetGroup(OptionDefinition option)
        {
            return option?.GroupKey == null ? null : GetCategory(option)?.FindGroup(option.GroupKey);
        }

        /// <summary>Finds a group by "Category/Path/Key".</summary>
        public OptionGroup FindGroup(string groupId)
        {
            var normalized = CategoryPath.Normalize(groupId);
            var split = normalized.LastIndexOf(CategoryPath.Separator);
            var node = split < 0 ? Root : FindCategory(normalized.Substring(0, split));
            return node?.FindGroup(split < 0 ? normalized : normalized.Substring(split + 1));
        }

        // ---- Search ---------------------------------------------------------------------

        /// <summary>See <see cref="OptionSearch" />. Results are ordered best first.</summary>
        public void Search(string query, List<OptionSearchResult> results)
        {
            OptionSearch.Search(_options, query, results);
        }

        /// <summary>Builds a filter that keeps the tree shape and expands only the matching branches.</summary>
        public OptionTreeFilter CreateFilter(string query)
        {
            return OptionTreeFilter.Create(this, query);
        }

        // ---- Pinning --------------------------------------------------------------------

        public bool IsPinned(OptionDefinition option)
        {
            return _pinned.Contains(option);
        }

        /// <summary>Pins the option. A grouped option pins its whole group.</summary>
        public void Pin(OptionDefinition option)
        {
            if (option == null || !_optionNodes.ContainsKey(option))
            {
                return;
            }

            var group = GetGroup(option);
            if (group == null)
            {
                PinOne(option);
                return;
            }

            foreach (var m in group.Members)
            {
                PinOne(m);
            }
        }

        /// <summary>Unpins the option. A grouped option unpins its whole group.</summary>
        public void Unpin(OptionDefinition option)
        {
            if (option == null)
            {
                return;
            }

            var group = GetGroup(option);
            if (group == null)
            {
                UnpinOne(option);
                return;
            }

            foreach (var m in group.Members)
            {
                UnpinOne(m);
            }
        }

        private void PinOne(OptionDefinition option)
        {
            if (_pinned.Add(option))
            {
                PinStateChanged?.Invoke(option, true);
            }
        }

        private void UnpinOne(OptionDefinition option)
        {
            if (_pinned.Remove(option))
            {
                PinStateChanged?.Invoke(option, false);
            }
        }

        /// <summary>Pins every option whose display name, member name or Id matches (SRDebugger PinOption semantics).</summary>
        public void PinByName(string name)
        {
            foreach (var o in _options.ToArray())
            {
                if (MatchesName(o, name))
                {
                    Pin(o);
                }
            }
        }

        public void UnpinByName(string name)
        {
            foreach (var o in _options.ToArray())
            {
                if (MatchesName(o, name))
                {
                    Unpin(o);
                }
            }
        }

        /// <summary>Pins all options in the category, including sub-categories.</summary>
        public void PinCategory(string category)
        {
            foreach (var o in _options.ToArray())
            {
                if (CategoryPath.IsSameOrDescendant(o.Category, category))
                {
                    Pin(o);
                }
            }
        }

        public void UnpinCategory(string category)
        {
            foreach (var o in _options.ToArray())
            {
                if (CategoryPath.IsSameOrDescendant(o.Category, category))
                {
                    Unpin(o);
                }
            }
        }

        public void ClearPinned()
        {
            var copy = new List<OptionDefinition>(_pinned);
            _pinned.Clear();
            foreach (var o in copy)
            {
                PinStateChanged?.Invoke(o, false);
            }

            PinsCleared?.Invoke();
        }

        // ---- Category order -------------------------------------------------------------

        /// <summary>
        ///     Sets the display order of a category among its siblings (lower first, default 0; FR-CAT-08). Works before the
        ///     category
        ///     exists (it is applied when it is created). Top-level categories are tabs and level-2 categories are sections,
        ///     so this orders tabs and sections too.
        /// </summary>
        public void SetCategorySortPriority(string categoryPath, int sortPriority)
        {
            var path = CategoryPath.Normalize(categoryPath);
            if (path.Length == 0)
            {
                throw new ArgumentException("Category path is required.", nameof(categoryPath));
            }

            if (_categorySort.TryGetValue(path, out var old) && old == sortPriority)
            {
                return;
            }

            _categorySort[path] = sortPriority;

            var node = FindCategory(path);
            if (node == null || node.SortPriority == sortPriority)
            {
                return;
            }

            node.SortPriority = sortPriority;
            var parent = node.Parent;
            if (parent == null || !parent.SortChildren())
            {
                return;
            }

            Version++;
            CategoriesReordered?.Invoke(parent);
        }

        /// <summary>The sort priority set for the path (0 if none).</summary>
        public int GetCategorySortPriority(string categoryPath)
        {
            return _categorySort.TryGetValue(CategoryPath.Normalize(categoryPath), out var p) ? p : 0;
        }

        // ---- Internals ------------------------------------------------------------------

        private static bool MatchesName(OptionDefinition o, string name)
        {
            return o.Name == name || o.MemberName == name || o.Id == name;
        }

        private void AddOwned(ContainerEntry entry, OptionDefinition option)
        {
            if (option == null || _optionNodes.ContainsKey(option))
            {
                return;
            }

            option.SetCategoryPrefix(entry.CategoryPrefix);
            option.Container ??= entry.Container is IOptionContainer ? null : entry.Container;
            entry.Options.Add(option);
            _owners[option] = entry;
            AddInternal(option);
        }

        private void AddInternal(OptionDefinition option)
        {
            if (_optionNodes.ContainsKey(option))
            {
                return;
            }

            var node = GetOrCreateNode(option.CategorySegments);
            var group = node.InsertOption(option, out var groupCreated);
            _optionNodes.Add(option, node);
            _options.Add(option);
            Version++;
            if (groupCreated)
            {
                GroupAdded?.Invoke(group);
            }

            OptionAdded?.Invoke(option);

            // Joining an already pinned group keeps the group pinned as a whole.
            if (group != null && !groupCreated && IsAnyOtherMemberPinned(group, option))
            {
                PinOne(option);
            }
        }

        private bool IsAnyOtherMemberPinned(OptionGroup group, OptionDefinition except)
        {
            foreach (var m in group.Members)
            {
                if (m != except && _pinned.Contains(m))
                {
                    return true;
                }
            }

            return false;
        }

        private bool RemoveInternal(OptionDefinition option)
        {
            if (!_optionNodes.TryGetValue(option, out var node))
            {
                return false;
            }

            _optionNodes.Remove(option);
            UnpinOne(option); // after removal so listeners can tell it apart from a user unpin
            _options.Remove(option);
            node.RemoveOption(option, out var removedGroup);
            if (_owners.TryGetValue(option, out var owner))
            {
                owner.Options.Remove(option);
                _owners.Remove(option);
            }

            Version++;
            OptionRemoved?.Invoke(option);
            if (removedGroup != null)
            {
                GroupRemoved?.Invoke(removedGroup);
            }

            PruneEmpty(node);
            return true;
        }

        private CategoryNode GetOrCreateNode(IReadOnlyList<string> segments)
        {
            var node = Root;
            for (var i = 0; i < segments.Count; i++)
            {
                var child = node.FindChild(segments[i]);
                if (child == null)
                {
                    var path = node.IsRoot ? segments[i] : node.Path + CategoryPath.Separator + segments[i];
                    child = new CategoryNode(segments[i], path, node, node.Depth + 1);
                    if (_categorySort.TryGetValue(path, out var priority))
                    {
                        child.SortPriority = priority;
                    }

                    node.AddChild(child);
                    Version++;
                    CategoryAdded?.Invoke(child);
                }

                node = child;
            }

            return node;
        }

        private void PruneEmpty(CategoryNode node)
        {
            while (node != null && !node.IsRoot && node.IsEmpty)
            {
                var parent = node.Parent;
                parent.RemoveChild(node);
                Version++;
                CategoryRemoved?.Invoke(node);
                node = parent;
            }
        }

        private static void OnContainerPropertyChanged(ContainerEntry entry, string propertyName)
        {
            foreach (var o in entry.Options)
            {
                if (o.Kind != OptionKind.Property)
                {
                    continue;
                }

                // null / empty means "all properties changed" per INotifyPropertyChanged convention.
                if (string.IsNullOrEmpty(propertyName) || o.MemberName == propertyName)
                {
                    o.NotifyValueChanged();
                }
            }
        }

        private sealed class ContainerEntry
        {
            public Action<OptionDefinition> AddedHandler;
            public string CategoryPrefix;
            public object Container;
            public List<OptionDefinition> Options;
            public PropertyChangedEventHandler PropertyChangedHandler;
            public Action<OptionDefinition> RemovedHandler;
        }
    }
}