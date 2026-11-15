using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Saves the values of persistent options (<see cref="OptionPersistAttribute" /> /
    ///     <see cref="OptionDefinition.WithPersistence" />)
    ///     whenever they change, and writes the saved value back when the option is registered (e.g. on the next launch).
    /// </summary>
    internal sealed class ValuePersistence : IDisposable
    {
        internal const string KeyPrefix = "value/";

        // Stored as "=" + value so an empty string is still a value (DebuggerState deletes empty entries).
        private const char ValueMarker = '=';

        private readonly OptionRegistry _registry;
        private readonly DebuggerState _state;
        private readonly HashSet<OptionDefinition> _tracked = new();

        public ValuePersistence(OptionRegistry registry, DebuggerState state)
        {
            _registry = registry;
            _state = state;
            registry.OptionAdded += OnOptionAdded;
            registry.OptionRemoved += OnOptionRemoved;
            foreach (var o in registry.Options.ToArrayCopy())
            {
                OnOptionAdded(o);
            }
        }

        public void Dispose()
        {
            _registry.OptionAdded -= OnOptionAdded;
            _registry.OptionRemoved -= OnOptionRemoved;
            foreach (var o in _tracked)
            {
                o.ValueChanged -= OnValueChanged;
            }

            _tracked.Clear();
        }

        internal static string Key(OptionDefinition o)
        {
            return KeyPrefix + (o.PersistKey ?? o.Id);
        }

        private void OnOptionAdded(OptionDefinition option)
        {
            if (!option.IsPersistent || option.Kind != OptionKind.Property || !option.CanWrite ||
                !option.CanRead)
            {
                return;
            }

            if (!OptionValueSerializer.IsSupported(option.ValueType))
            {
                Debug.LogWarning(
                    $"[AtsuvancedDebugger] Option '{option.Id}' is marked persistent but {option.ValueType?.Name} values cannot be saved.");
                return;
            }

            if (!_tracked.Add(option))
            {
                return;
            }

            Restore(option);
            option.ValueChanged += OnValueChanged;
        }

        private void OnOptionRemoved(OptionDefinition option)
        {
            if (_tracked.Remove(option))
            {
                option.ValueChanged -= OnValueChanged;
            }
        }

        private void Restore(OptionDefinition option)
        {
            var stored = _state.Get(Key(option));
            if (stored == null)
            {
                return;
            }

            if (stored.Length == 0 || stored[0] != ValueMarker ||
                !OptionValueSerializer.TryDeserialize(stored.Substring(1), option.ValueType, out var value))
            {
                Debug.LogWarning(
                    $"[AtsuvancedDebugger] Ignoring the saved value of '{option.Id}' (\"{stored}\" is not a valid {option.ValueType.Name}).");
                return;
            }

            try
            {
                option.SetValue(value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnValueChanged(OptionDefinition option)
        {
            object value;
            try
            {
                value = option.GetValue();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            if (OptionValueSerializer.TrySerialize(value, option.ValueType, out var text))
            {
                _state.Set(Key(option), ValueMarker + text);
            }
        }
    }
}