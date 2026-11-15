using System;

namespace Atsuvanced.Debugger
{
    // NOTE: These attributes are intentionally not sealed so that the SROptions shim
    // can expose SRDebugger-style nested attributes (SROptions.NumberRangeAttribute etc.) that derive from them.

    /// <summary>
    ///     Category shown in the Options tab. Nest with '/' (e.g. "Player/Status/Buffs") or pass segments
    ///     (<c>[OptionCategory("Player", "Status", "Buffs")]</c>). Depth is unlimited.
    ///     On a class, the category becomes the parent of every member's category.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
    public class OptionCategoryAttribute : Attribute
    {
        public OptionCategoryAttribute(string category)
        {
            Category = CategoryPath.Normalize(category);
        }

        public OptionCategoryAttribute(params string[] segments)
        {
            Category = CategoryPath.Combine(segments);
        }

        public string Category { get; }
    }

    /// <summary>
    ///     Display order of a category among its siblings (lower first; categories without one use 0 and keep the order in
    ///     which they
    ///     first appeared). On a class without <see cref="Path" />, it applies to the class's own
    ///     <see cref="OptionCategoryAttribute" />
    ///     category; with <see cref="Path" /> it applies to that category (under the container's prefix). Top-level categories
    ///     are tabs, level-2 categories are sections, so this also orders tabs and sections. Can be repeated.
    ///     The same can be done from code with <see cref="OptionRegistry.SetCategorySortPriority" />.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class OptionCategorySortAttribute : Attribute
    {
        public OptionCategorySortAttribute(int sortPriority)
        {
            SortPriority = sortPriority;
        }

        public OptionCategorySortAttribute(string path, int sortPriority)
        {
            Path = CategoryPath.Normalize(path);
            SortPriority = sortPriority;
        }

        /// <summary>Category path ("Player/Status"), or null for the class's own category.</summary>
        public string Path { get; }

        public int SortPriority { get; }
    }

    /// <summary>Description shown under the option and included in search.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class OptionDescriptionAttribute : Attribute
    {
        public OptionDescriptionAttribute(string description)
        {
            Description = description;
        }

        public string Description { get; }
    }

    /// <summary>Overrides the display name of an option.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class OptionNameAttribute : Attribute
    {
        public OptionNameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    /// <summary>Sort priority within a category. Lower values are shown first.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class OptionSortAttribute : Attribute
    {
        public OptionSortAttribute(int priority)
        {
            SortPriority = priority;
        }

        public int SortPriority { get; }
    }

    /// <summary>Clamps a numeric option to [Min, Max].</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NumberRangeAttribute : Attribute
    {
        public NumberRangeAttribute(double min, double max)
        {
            Min = min;
            Max = max;
        }

        public double Min { get; }
        public double Max { get; }
    }

    /// <summary>Step used by increment/decrement buttons of a numeric option.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IncrementAttribute : Attribute
    {
        public IncrementAttribute(double increment)
        {
            Increment = increment;
        }

        public double Increment { get; }
    }

    /// <summary>Excludes the member from the Options tab.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class HiddenOptionAttribute : Attribute
    {
    }
}

namespace Atsuvanced.Debugger
{
    public enum OptionGroupLayout
    {
        /// <summary>Members side by side in one row (e.g. [input][button]).</summary>
        Horizontal,

        /// <summary>Members stacked in one block.</summary>
        Vertical
    }

    public enum OptionGroupCommit
    {
        /// <summary>OnAction if the group has both a writable property and a method, otherwise Immediate.</summary>
        Auto,

        /// <summary>Edits are written to the property immediately (SRDebugger behaviour).</summary>
        Immediate,

        /// <summary>Edits are kept as pending values and written just before a method member is invoked.</summary>
        OnAction
    }

    /// <summary>
    ///     Puts several options (properties and/or methods) into one UI unit, e.g. an input field and the button
    ///     that submits it. The members remain separate definitions, so SRDebugger still sees them as individual options.
    ///     Members with the same key in the same category form one group. Settings (Label / Layout / Commit) may be
    ///     given on any one member; the first non-default value wins.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class OptionGroupAttribute : Attribute
    {
        public OptionGroupAttribute(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Group key is required.", nameof(key));
            }

            Key = key;
        }

        public string Key { get; }
        public string Label { get; set; }
        public OptionGroupLayout Layout { get; set; } = OptionGroupLayout.Horizontal;
        public OptionGroupCommit Commit { get; set; } = OptionGroupCommit.Auto;
    }
}

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Saves the property's value whenever it changes and restores it when the option is registered again
    ///     (e.g. "God mode" stays on after a restart). Uses the debugger's storage (<see cref="DebuggerBuilder.UseStorage" />
    ///     ).
    ///     Supported types: bool, numbers, string, enums, Vector2/3/4, Color.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OptionPersistAttribute : Attribute
    {
        /// <summary>Storage key; defaults to the option's Id ("Category/Name"). Set it to keep a saved value across a rename.</summary>
        public string Key { get; set; }
    }
}