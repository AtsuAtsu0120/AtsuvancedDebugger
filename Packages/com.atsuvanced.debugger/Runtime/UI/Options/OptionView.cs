using System;
using System.Collections.Generic;
using Atsuvanced.Debugger.UI;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger
{
    /// <summary>Where an option is being drawn.</summary>
    public enum OptionPlacement
    {
        /// <summary>The Options tab.</summary>
        Panel,

        /// <summary>The pinned overlay on top of the game.</summary>
        Pinned
    }

    /// <summary>
    ///     Everything a view needs to read / write one option. Reads and writes go through the option's group
    ///     when it has one, so the group's commit mode (OnAction = pending until a button is pressed) is respected.
    /// </summary>
    public sealed class OptionContext
    {
        public OptionContext(OptionDefinition definition, OptionGroup group, OptionPlacement placement)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Group = group;
            Placement = placement;
        }

        public OptionDefinition Definition { get; }

        /// <summary>The group the option belongs to, or null.</summary>
        public OptionGroup Group { get; }

        /// <summary>The object the option was scanned from (use as <c>dataSource</c>), or null.</summary>
        public object Container => Definition.Container;

        public OptionPlacement Placement { get; }

        public Type ValueType => Definition.ValueType;
        public bool CanWrite => Definition.CanWrite;

        /// <summary>True if the group holds an uncommitted value for this option.</summary>
        public bool IsPending => Group != null && Group.HasPending(Definition);

        public object GetValue()
        {
            return Group != null ? Group.GetDisplayValue(Definition) : Definition.GetValue();
        }

        public T GetValue<T>()
        {
            return (T)OptionValueConverter.Convert(GetValue(), typeof(T));
        }

        /// <summary>
        ///     Reads the displayed value (pending value of the group, otherwise the current value) without boxing when
        ///     possible (see <see cref="OptionDefinition.TryGetValue{T}" />). Use it in <see cref="OptionView.Refresh" />,
        ///     which runs several times a second while the row is on screen.
        /// </summary>
        public bool TryGetValue<T>(out T value)
        {
            if (Group != null && Group.HasPending(Definition))
            {
                if (Group.GetDisplayValue(Definition) is T pending)
                {
                    value = pending;
                    return true;
                }

                value = default;
                return false;
            }

            return Definition.TryGetValue(out value);
        }

        /// <summary>The displayed value of an enum option as its integral value (see <see cref="OptionDefinition.TryGetEnumValue" />).</summary>
        public bool TryGetEnumValue(out long value)
        {
            if (Group != null && Group.HasPending(Definition))
            {
                if (Group.GetDisplayValue(Definition) is IConvertible c)
                {
                    value = c.ToInt64(System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }

                value = 0;
                return false;
            }

            return Definition.TryGetEnumValue(out value);
        }

        /// <summary>Converts to the option's type and writes (or stores as pending in an OnAction group).</summary>
        public void SetValue(object value)
        {
            var converted = OptionValueConverter.Convert(value, Definition.ValueType);
            if (Definition.Range != null && OptionValueConverter.IsNumeric(Definition.ValueType))
            {
                var d = Math.Max(Definition.Range.Min,
                    Math.Min(Definition.Range.Max, OptionValueConverter.ToDouble(converted)));
                converted = OptionValueConverter.Convert(d, Definition.ValueType);
            }

            if (Group != null)
            {
                Group.SetValue(Definition, converted);
            }
            else
            {
                Definition.SetValue(converted);
            }
        }

        /// <summary>Invokes a method option (applying the group's pending values first).</summary>
        public void Invoke()
        {
            if (Group != null)
            {
                Group.Invoke(Definition);
            }
            else
            {
                Definition.Invoke();
            }
        }
    }

    /// <summary>
    ///     Draws one option. Subclass this for custom drawing and register it for a value type with
    ///     <c>DebuggerBuilder.AddOptionView&lt;T&gt;</c> or <see cref="OptionViewRegistry.Register{T}" />: every option of that
    ///     type is then drawn with it. Custom drawing is only chosen per type.
    /// </summary>
    public abstract class OptionView
    {
        /// <summary>
        ///     If true the row shows the option name in a label column on the left;
        ///     if false the element takes the whole row width.
        /// </summary>
        public virtual bool ShowLabel => true;

        /// <summary>Builds the element. Called once per row.</summary>
        public abstract VisualElement CreateElement(OptionContext context);

        /// <summary>Called when the value may have changed (change notification, polling while visible, pending changes).</summary>
        public virtual void Refresh(OptionContext context)
        {
        }

        /// <summary>Called when the row is removed.</summary>
        public virtual void Dispose()
        {
        }
    }

    /// <summary>
    ///     Custom views of one debugger (<see cref="IDebugService.OptionViews" />), registered per value type.
    ///     Resolution order for an option: methods → button, type registration → built-in.
    ///     Register at start-up with <c>DebuggerBuilder.AddOptionView</c> or at any time here (rows created afterwards use
    ///     it).
    /// </summary>
    public sealed class OptionViewRegistry
    {
        private readonly OptionRegistry _options;
        private readonly Dictionary<Type, Func<OptionView>> _typeViews = new();

        /// <param name="options">Registry whose scanning accepts the value types registered here (may be null).</param>
        public OptionViewRegistry(OptionRegistry options = null)
        {
            _options = options;
        }

        /// <summary>Draws every option of <typeparamref name="T" /> with the view created by <paramref name="factory" />.</summary>
        public void Register<T>(Func<OptionView> factory)
        {
            Register(typeof(T), factory);
        }

        /// <summary>
        ///     Draws every option of <paramref name="valueType" /> with the view created by <paramref name="factory" />.
        ///     Properties of the
        ///     type become options from now on (containers added earlier are not rescanned).
        /// </summary>
        public void Register(Type valueType, Func<OptionView> factory)
        {
            if (valueType == null)
            {
                throw new ArgumentNullException(nameof(valueType));
            }

            _typeViews[valueType] = factory ?? throw new ArgumentNullException(nameof(factory));
            _options?.AddValueType(valueType);
        }

        public bool Unregister(Type valueType)
        {
            if (valueType == null || !_typeViews.Remove(valueType))
            {
                return false;
            }

            _options?.RemoveValueType(valueType);
            return true;
        }

        public bool HasTypeView(Type valueType)
        {
            return valueType != null && _typeViews.ContainsKey(valueType);
        }

        /// <summary>
        ///     Creates the view the panel would use for <paramref name="definition" /> (following the resolution order
        ///     above).
        /// </summary>
        public OptionView Create(OptionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Kind == OptionKind.Method)
            {
                return new ButtonOptionView();
            }

            if (definition.ValueType != null && _typeViews.TryGetValue(definition.ValueType, out var factory))
            {
                return factory();
            }

            return BuiltInOptionViews.Create(definition);
        }

        /// <summary>
        ///     Creates the built-in view for <paramref name="definition" />, ignoring type registrations. Use it to decorate the default drawing from a custom view, e.g. a type view for <c>float</c>
        ///     that adds a reset button next to the built-in slider.
        /// </summary>
        public static OptionView CreateDefault(OptionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Kind == OptionKind.Method)
            {
                return new ButtonOptionView();
            }

            return BuiltInOptionViews.Create(definition);
        }
    }
}