using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    public enum OptionKind
    {
        Property,
        Method
    }

    /// <summary>
    ///     A single entry of the Options tab. Either a value (getter/setter) or an action (method).
    /// </summary>
    public sealed class OptionDefinition
    {
        public const string DefaultCategory = "Default";

        private readonly Func<object> _getter;
        private readonly Action _method;
        private readonly Action<object> _setter;

        // Allocation-free reads for views that poll values (see TryGetValue / TryGetEnumValue).
        private Func<long> _enumGetter;
        private Delegate _typedGetter;

        private string[] _categorySegments;
        private SearchKeySet _searchKeys;

        private OptionDefinition(
            string name, string category, int sortPriority, OptionKind kind, Type valueType,
            Func<object> getter, Action<object> setter, Action method,
            NumberRangeAttribute range, double? increment, string memberName, string description,
            OptionGroupAttribute group)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Option name is required.", nameof(name));
            }

            Name = name;
            var normalized = CategoryPath.Normalize(category);
            DeclaredCategory = normalized.Length == 0 ? DefaultCategory : normalized;
            SortPriority = sortPriority;
            Kind = kind;
            ValueType = valueType;
            _getter = getter;
            _setter = setter;
            _method = method;
            Range = range;
            Increment = increment;
            MemberName = memberName ?? name;
            Description = description ?? string.Empty;
            GroupSettings = group;
            RebuildCategory();
        }

        public string Name { get; }
        public int SortPriority { get; }
        public OptionKind Kind { get; }
        public string Description { get; }

        /// <summary>Group key, or null if the option is not grouped. See <see cref="OptionGroupAttribute" />.</summary>
        public string GroupKey => GroupSettings?.Key;

        /// <summary>Group settings declared on this option, or null.</summary>
        public OptionGroupAttribute GroupSettings { get; }

        /// <summary>Effective category path ("Player/Status/Buffs"), including any registration prefix.</summary>
        public string Category { get; private set; }

        /// <summary>Segments of <see cref="Category" />.</summary>
        public IReadOnlyList<string> CategorySegments => _categorySegments;

        /// <summary>Category as declared (attribute / factory argument), without the registration prefix.</summary>
        public string DeclaredCategory { get; }

        /// <summary>Value type for properties; null for methods.</summary>
        public Type ValueType { get; }

        /// <summary>The object the option was scanned from / registered with, or null for standalone options.</summary>
        public object Container { get; internal set; }

        public bool CanRead => _getter != null;
        public bool CanWrite => _setter != null;

        public NumberRangeAttribute Range { get; }
        public double? Increment { get; }

        /// <summary>Member name on the owning container (used to match INotifyPropertyChanged notifications).</summary>
        public string MemberName { get; }

        /// <summary>Key used for pinning / persistence: "Category/Path/Name".</summary>
        public string Id => Category + CategoryPath.Separator + Name;

        /// <summary>True if the value is saved and restored across launches (see <see cref="OptionPersistAttribute" />).</summary>
        public bool IsPersistent { get; private set; }

        /// <summary>Storage key of the saved value, or null to use <see cref="Id" />.</summary>
        public string PersistKey { get; private set; }

        // ---- Category prefix (set by OptionRegistry on registration) ----------------------

        internal string CategoryPrefix { get; private set; } = string.Empty;

        internal SearchKeySet SearchKeys
        {
            get
            {
                if (_searchKeys != null)
                {
                    return _searchKeys;
                }

                var segments = new string[_categorySegments.Length];
                for (var i = 0; i < segments.Length; i++)
                {
                    segments[i] = SearchText.Normalize(_categorySegments[i]);
                }

                _searchKeys = new SearchKeySet
                {
                    Name = SearchText.Normalize(Name),
                    Member = SearchText.Normalize(MemberName),
                    Description = SearchText.Normalize(Description),
                    FullPath = SearchText.Normalize(Id),
                    Group = SearchText.Normalize(
                        GroupSettings == null ? null : GroupSettings.Label ?? GroupSettings.Key),
                    CategorySegments = segments
                };
                return _searchKeys;
            }
        }

        /// <summary>Raised when the value is changed via <see cref="SetValue" /> or a container notification.</summary>
        public event Action<OptionDefinition> ValueChanged;

        public object GetValue()
        {
            if (_getter == null)
            {
                throw new InvalidOperationException($"Option '{Name}' is not readable.");
            }

            return _getter();
        }

        /// <summary>
        ///     Reads the value as <typeparamref name="T" /> without boxing when the option's getter is typed as
        ///     <typeparamref name="T" /> (options created with <see cref="Create{T}" />, or scanned properties of a common
        ///     type: bool, int, long, float, double, string, Vector2/3/4, Color). Otherwise falls back to
        ///     <see cref="GetValue" /> and returns false if the value is not a <typeparamref name="T" />.
        ///     Use it in views that poll values, so polling does not allocate.
        /// </summary>
        public bool TryGetValue<T>(out T value)
        {
            if (_typedGetter is Func<T> typed)
            {
                value = typed();
                return true;
            }

            if (_getter != null && _getter() is T v)
            {
                value = v;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        ///     Reads an enum option's value as its integral value. Allocation free for enums backed by int (the default);
        ///     other enums fall back to <see cref="GetValue" />. False if the option is not a readable enum.
        /// </summary>
        public bool TryGetEnumValue(out long value)
        {
            if (_enumGetter != null)
            {
                value = _enumGetter();
                return true;
            }

            if (_getter != null && ValueType != null && ValueType.IsEnum && _getter() is IConvertible c)
            {
                value = c.ToInt64(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Sets the typed getter used by <see cref="TryGetValue{T}" /> (a <c>Func&lt;ValueType&gt;</c>).</summary>
        internal void SetTypedGetter(Delegate typedGetter)
        {
            if (typedGetter == null || ValueType == null ||
                typedGetter.GetType() != typeof(Func<>).MakeGenericType(ValueType))
            {
                return;
            }

            _typedGetter = typedGetter;
            _enumGetter = TypedGetters.EnumAsLong(ValueType, typedGetter);
        }

        public void SetValue(object value)
        {
            if (_setter == null)
            {
                throw new InvalidOperationException($"Option '{Name}' is read-only.");
            }

            _setter(value);
            NotifyValueChanged();
        }

        public void Invoke()
        {
            if (_method == null)
            {
                throw new InvalidOperationException($"Option '{Name}' is not a method.");
            }

            _method();
        }

        public void NotifyValueChanged()
        {
            ValueChanged?.Invoke(this);
        }

        /// <summary>
        ///     Saves this option's value whenever it changes and restores it when registered. Call before registering.
        ///     Returns this for chaining.
        /// </summary>
        public OptionDefinition WithPersistence(string key = null)
        {
            IsPersistent = true;
            PersistKey = string.IsNullOrEmpty(key) ? null : key;
            return this;
        }

        public override string ToString()
        {
            return $"{Kind}:{Id}";
        }

        /// <summary>Replaces (does not stack) the registration prefix. Only call while unregistered.</summary>
        internal void SetCategoryPrefix(string prefix)
        {
            prefix = CategoryPath.Normalize(prefix);
            if (prefix == CategoryPrefix)
            {
                return;
            }

            CategoryPrefix = prefix;
            RebuildCategory();
        }

        private void RebuildCategory()
        {
            // A prefixed option without its own category goes directly into the prefix category.
            Category = CategoryPrefix.Length == 0
                ? DeclaredCategory
                : DeclaredCategory == DefaultCategory
                    ? CategoryPrefix
                    : CategoryPath.Combine(CategoryPrefix, DeclaredCategory);
            _categorySegments = CategoryPath.Split(Category);
            _searchKeys = null;
        }

        // ---- Factories ------------------------------------------------------------------

        public static OptionDefinition Create<T>(
            string name, Func<T> getter, Action<T> setter = null,
            string category = DefaultCategory, int sortPriority = 0,
            NumberRangeAttribute range = null, double? increment = null, string description = null,
            OptionGroupAttribute group = null)
        {
            if (getter == null)
            {
                throw new ArgumentNullException(nameof(getter));
            }

            var option = new OptionDefinition(
                name, category, sortPriority, OptionKind.Property, typeof(T),
                () => getter(),
                setter == null ? null : v => setter((T)v),
                null, range, increment, name, description, group);
            option.SetTypedGetter(getter);
            return option;
        }

        public static OptionDefinition FromMethod(
            string name, Action callback, string category = DefaultCategory, int sortPriority = 0,
            string description = null, OptionGroupAttribute group = null)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return new OptionDefinition(
                name, category, sortPriority, OptionKind.Method, null,
                null, null, callback, null, null, name, description, group);
        }

        /// <summary>A button option that opens <paramref name="pageFactory" />'s page in the debug panel.</summary>
        public static OptionDefinition FromPage(
            string name, Func<DebugPage> pageFactory, string category = DefaultCategory, int sortPriority = 0,
            string description = null, OptionGroupAttribute group = null)
        {
            if (pageFactory == null)
            {
                throw new ArgumentNullException(nameof(pageFactory));
            }

            return FromMethod(name, () => AtsuDebug.Instance.PushPage(pageFactory()), category, sortPriority,
                description, group);
        }

        internal static OptionDefinition CreateUntyped(
            string name, string category, int sortPriority, Type valueType,
            Func<object> getter, Action<object> setter,
            NumberRangeAttribute range, double? increment, string memberName, string description,
            OptionGroupAttribute group)
        {
            return new OptionDefinition(
                name, category, sortPriority, OptionKind.Property, valueType,
                getter, setter, null, range, increment, memberName, description, group);
        }

        internal static OptionDefinition CreateMethod(
            string name, string category, int sortPriority, Action callback, string memberName, string description,
            OptionGroupAttribute group)
        {
            return new OptionDefinition(
                name, category, sortPriority, OptionKind.Method, null,
                null, null, callback, null, null, memberName, description, group);
        }

        // ---- Search keys (normalized, cached) ---------------------------------------------

        internal sealed class SearchKeySet
        {
            public string[] CategorySegments;
            public string Description;
            public string FullPath;
            public string Group;
            public string Member;
            public string Name;
        }
    }
}