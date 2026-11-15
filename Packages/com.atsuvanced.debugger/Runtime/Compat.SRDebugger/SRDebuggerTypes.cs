using System;
using System.Collections.Generic;
using Atsu = Atsuvanced.Debugger;

namespace SRDebugger
{
    public enum DefaultTabs
    {
        SystemInformation = 0,
        Options = 1,
        Console = 2,
        Profiler = 3,
        BugReporter = 4
    }

    public enum ConsoleAlignment
    {
        Top,
        Bottom
    }

    public delegate void VisibilityChangedDelegate(bool isVisible);

    public delegate void ActionCompleteCallback(bool success);

    /// <summary>SRDebugger compatible OptionDefinition. Wraps the core definition.</summary>
    public sealed class OptionDefinition
    {
        private OptionDefinition(Atsu.OptionDefinition core)
        {
            Core = core;
        }

        public Atsu.OptionDefinition Core { get; }

        public string Name => Core.Name;
        public string Category => Core.Category;
        public int SortPriority => Core.SortPriority;
        public bool IsMethod => Core.Kind == Atsu.OptionKind.Method;
        public bool IsProperty => Core.Kind == Atsu.OptionKind.Property;

        public static OptionDefinition Create<T>(
            string name, Func<T> getter, Action<T> setter = null,
            string category = "Default", int sortPriority = 0)
        {
            return new OptionDefinition(Atsu.OptionDefinition.Create(name, getter, setter, category, sortPriority));
        }

        public static OptionDefinition FromMethod(
            string name, Action callback, string category = "Default", int sortPriority = 0)
        {
            return new OptionDefinition(Atsu.OptionDefinition.FromMethod(name, callback, category, sortPriority));
        }
    }

    /// <summary>SRDebugger compatible option container interface.</summary>
    public interface IOptionContainer
    {
        bool IsDynamic { get; }
        IEnumerable<OptionDefinition> GetOptions();
        event Action<OptionDefinition> OptionAdded;
        event Action<OptionDefinition> OptionRemoved;
    }

    /// <summary>SRDebugger compatible dynamic container.</summary>
    public sealed class DynamicOptionContainer : IOptionContainer
    {
        private readonly List<OptionDefinition> _options = new();

        public IList<OptionDefinition> Options => _options.AsReadOnly();
        public bool IsDynamic => true;

        public event Action<OptionDefinition> OptionAdded;
        public event Action<OptionDefinition> OptionRemoved;

        public IEnumerable<OptionDefinition> GetOptions()
        {
            return _options;
        }

        public void AddOption(OptionDefinition option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            if (_options.Contains(option))
            {
                return;
            }

            _options.Add(option);
            OptionAdded?.Invoke(option);
        }

        public bool RemoveOption(OptionDefinition option)
        {
            if (option == null || !_options.Remove(option))
            {
                return false;
            }

            OptionRemoved?.Invoke(option);
            return true;
        }
    }

    /// <summary>SRDebugger compatible InfoEntry.</summary>
    public sealed class InfoEntry
    {
        private InfoEntry(Atsu.InfoEntry core)
        {
            Core = core;
        }

        public Atsu.InfoEntry Core { get; }

        public string Title => Core.Title;
        public object Value => Core.Value;
        public bool IsPrivate => Core.IsPrivate;

        public static InfoEntry Create(string name, Func<object> getter, bool isPrivate = false)
        {
            return new InfoEntry(Atsu.InfoEntry.Create(name, getter, isPrivate));
        }

        public static InfoEntry Create(string name, object value, bool isPrivate = false)
        {
            return new InfoEntry(Atsu.InfoEntry.Create(name, value, isPrivate));
        }
    }
}