using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>An option container whose options can be added/removed at runtime.</summary>
    public class DynamicOptionContainer : IOptionContainer
    {
        private readonly List<OptionDefinition> _options = new();

        public IReadOnlyList<OptionDefinition> Options => _options;
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
}