using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.UI
{
    internal static class BuiltInOptionViews
    {
        public static OptionView Create(OptionDefinition d)
        {
            var t = d.ValueType;
            if (t == null)
            {
                return new UnsupportedOptionView();
            }

            if (!d.CanWrite)
            {
                return new ReadOnlyOptionView();
            }

            if (t == typeof(bool))
            {
                return new ToggleOptionView();
            }

            if (t.IsEnum)
            {
                return t.IsDefined(typeof(FlagsAttribute), false) ? new FlagsOptionView() : new EnumOptionView();
            }

            if (OptionValueConverter.IsNumeric(t))
            {
                return new NumberOptionView();
            }

            if (t == typeof(string))
            {
                return new TextOptionView();
            }

            if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4))
            {
                return new VectorOptionView();
            }

            if (t == typeof(Color))
            {
                return new ColorOptionView();
            }

            return new UnsupportedOptionView();
        }

        /// <summary>True while the user is editing inside <paramref name="e" /> (don't overwrite their input).</summary>
        public static bool HasFocusWithin(VisualElement e)
        {
            var focused = e?.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == e || e.Contains(focused));
        }

        /// <summary>Reads a numeric option as double; int / long / float / double read without boxing.</summary>
        public static double ReadNumber(OptionContext ctx)
        {
            var t = ctx.ValueType;
            if (t == typeof(int) && ctx.TryGetValue(out int i)) return i;
            if (t == typeof(float) && ctx.TryGetValue(out float f)) return f;
            if (t == typeof(double) && ctx.TryGetValue(out double d)) return d;
            if (t == typeof(long) && ctx.TryGetValue(out long l)) return l;
            return OptionValueConverter.ToDouble(ctx.GetValue());
        }

        /// <summary>The value of an enum option as long (int-backed enums read without boxing).</summary>
        public static long ReadEnumBits(OptionContext ctx)
        {
            return ctx.TryGetEnumValue(out var bits)
                ? bits
                : Convert.ToInt64(ctx.GetValue(), CultureInfo.InvariantCulture);
        }

        /// <summary>Name of each defined value of an enum type, keyed by its integral value.</summary>
        public static Dictionary<long, string> EnumNames(Type enumType)
        {
            var names = new Dictionary<long, string>();
            foreach (var value in Enum.GetValues(enumType))
            {
                var bits = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (!names.ContainsKey(bits))
                {
                    names.Add(bits, Enum.GetName(enumType, value));
                }
            }

            return names;
        }

        public static string Format(object value)
        {
            switch (value)
            {
                case null: return "-";
                case float f: return f.ToString("0.###", CultureInfo.InvariantCulture);
                case double d: return d.ToString("0.###", CultureInfo.InvariantCulture);
                case bool b: return b ? "On" : "Off";
                case Color c: return OptionValueConverter.ToHex(c);
                default: return value.ToString();
            }
        }
    }

    internal sealed class ToggleOptionView : OptionView
    {
        private Toggle _toggle;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            _toggle = new Toggle();
            _toggle.AddToClassList("atsu-option-toggle");
            _toggle.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
            return _toggle;
        }

        public override void Refresh(OptionContext ctx)
        {
            var value = ctx.TryGetValue(out bool b) ? b : (bool)ctx.GetValue();
            if (_toggle.value != value)
            {
                _toggle.SetValueWithoutNotify(value);
            }
        }
    }

    internal sealed class TextOptionView : OptionView
    {
        private TextField _field;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            _field = new TextField { isDelayed = ctx.Group == null };
            // Inside an OnAction group the value is only pending, so write on every keystroke.
            _field.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
            return _field;
        }

        public override void Refresh(OptionContext ctx)
        {
            if (BuiltInOptionViews.HasFocusWithin(_field))
            {
                return;
            }

            var value = (ctx.TryGetValue(out string text) ? text : (string)ctx.GetValue()) ?? string.Empty;
            if (_field.value != value)
            {
                _field.SetValueWithoutNotify(value);
            }
        }
    }

    internal sealed class EnumOptionView : OptionView
    {
        private DropdownField _dropdown;
        private Dictionary<long, string> _names;
        private long _shownBits;
        private bool _hasShown;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            _names = BuiltInOptionViews.EnumNames(ctx.ValueType);
            _dropdown = new DropdownField(new List<string>(Enum.GetNames(ctx.ValueType)), 0);
            _dropdown.RegisterValueChangedCallback(e => ctx.SetValue(Enum.Parse(ctx.ValueType, e.newValue)));
            return _dropdown;
        }

        public override void Refresh(OptionContext ctx)
        {
            var bits = BuiltInOptionViews.ReadEnumBits(ctx);
            if (_hasShown && bits == _shownBits)
            {
                return;
            }

            _hasShown = true;
            _shownBits = bits;
            var name = _names.TryGetValue(bits, out var n) ? n : ctx.GetValue()?.ToString();
            _dropdown.SetValueWithoutNotify(name);
        }
    }

    internal sealed class FlagsOptionView : OptionView
    {
        private readonly List<(long bit, Toggle toggle)> _toggles = new();

        public override VisualElement CreateElement(OptionContext ctx)
        {
            var root = new VisualElement();
            root.AddToClassList("atsu-option-flags");
            foreach (var value in Enum.GetValues(ctx.ValueType))
            {
                var bit = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (bit == 0 || (bit & (bit - 1)) != 0)
                {
                    continue; // single bits only
                }

                var toggle = new Toggle(Enum.GetName(ctx.ValueType, value));
                toggle.RegisterValueChangedCallback(e =>
                {
                    var current = BuiltInOptionViews.ReadEnumBits(ctx);
                    ctx.SetValue(e.newValue ? current | bit : current & ~bit);
                });
                _toggles.Add((bit, toggle));
                root.Add(toggle);
            }

            return root;
        }

        public override void Refresh(OptionContext ctx)
        {
            var current = BuiltInOptionViews.ReadEnumBits(ctx);
            for (var i = 0; i < _toggles.Count; i++)
            {
                var (bit, toggle) = _toggles[i];
                var on = (current & bit) != 0;
                if (toggle.value != on)
                {
                    toggle.SetValueWithoutNotify(on);
                }
            }
        }
    }

    internal sealed class NumberOptionView : OptionView
    {
        private BaseField<double> _doubleField;
        private BaseField<long> _longField;
        private VisualElement _root;
        private Slider _slider;
        private SliderInt _sliderInt;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            var d = ctx.Definition;
            var isInt = OptionValueConverter.IsInteger(ctx.ValueType);
            _root = new VisualElement();
            _root.AddToClassList("atsu-option-number");

            VisualElement input;
            if (d.Range != null && isInt)
            {
                _sliderInt = new SliderInt((int)Math.Ceiling(d.Range.Min), (int)Math.Floor(d.Range.Max))
                    { showInputField = true };
                _sliderInt.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
                input = _sliderInt;
            }
            else if (d.Range != null)
            {
                _slider = new Slider((float)d.Range.Min, (float)d.Range.Max) { showInputField = true };
                _slider.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
                input = _slider;
            }
            else if (isInt)
            {
                _longField = new LongField { isDelayed = true };
                _longField.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
                input = _longField;
            }
            else
            {
                _doubleField = new DoubleField { isDelayed = true };
                _doubleField.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
                input = _doubleField;
            }

            input.AddToClassList("atsu-option-number__input");

            if (d.Increment.HasValue || d.Range == null)
            {
                var step = d.Increment ?? 1d;
                var minus = StepButton("−", () => Step(ctx, -step));
                var plus = StepButton("+", () => Step(ctx, step));
                _root.Add(minus);
                _root.Add(input);
                _root.Add(plus);
            }
            else
            {
                _root.Add(input);
            }

            return _root;
        }

        private static Button StepButton(string text, Action action)
        {
            var b = new Button(action) { text = text };
            b.AddToClassList("atsu-button");
            b.AddToClassList("atsu-option-number__step");
            return b;
        }

        private static void Step(OptionContext ctx, double delta)
        {
            ctx.SetValue(BuiltInOptionViews.ReadNumber(ctx) + delta);
        }

        public override void Refresh(OptionContext ctx)
        {
            if (BuiltInOptionViews.HasFocusWithin(_root))
            {
                return;
            }

            // Compare with what the control shows: setting an equal value still re-formats its text field.
            var v = BuiltInOptionViews.ReadNumber(ctx);
            if (_sliderInt != null && _sliderInt.value != (int)Math.Round(v))
            {
                _sliderInt.SetValueWithoutNotify((int)Math.Round(v));
            }

            if (_slider != null && !_slider.value.Equals((float)v))
            {
                _slider.SetValueWithoutNotify((float)v);
            }

            if (_longField != null && _longField.value != (long)Math.Round(v))
            {
                _longField.SetValueWithoutNotify((long)Math.Round(v));
            }

            if (_doubleField != null && !_doubleField.value.Equals(v))
            {
                _doubleField.SetValueWithoutNotify(v);
            }
        }
    }

    internal sealed class VectorOptionView : OptionView
    {
        private VisualElement _field;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            var t = ctx.ValueType;
            if (t == typeof(Vector2))
            {
                var f = new Vector2Field();
                f.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
                _field = f;
            }
            else if (t == typeof(Vector3))
            {
                var f = new Vector3Field();
                f.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
                _field = f;
            }
            else
            {
                var f = new Vector4Field();
                f.RegisterValueChangedCallback(e => ctx.SetValue(e.newValue));
                _field = f;
            }

            _field.AddToClassList("atsu-option-vector");
            return _field;
        }

        public override void Refresh(OptionContext ctx)
        {
            if (BuiltInOptionViews.HasFocusWithin(_field))
            {
                return;
            }

            switch (_field)
            {
                case Vector2Field f2:
                {
                    var v = ctx.TryGetValue(out Vector2 typed) ? typed : (Vector2)ctx.GetValue();
                    if (f2.value != v) f2.SetValueWithoutNotify(v);
                    break;
                }
                case Vector3Field f3:
                {
                    var v = ctx.TryGetValue(out Vector3 typed) ? typed : (Vector3)ctx.GetValue();
                    if (f3.value != v) f3.SetValueWithoutNotify(v);
                    break;
                }
                case Vector4Field f4:
                {
                    var v = ctx.TryGetValue(out Vector4 typed) ? typed : (Vector4)ctx.GetValue();
                    if (f4.value != v) f4.SetValueWithoutNotify(v);
                    break;
                }
            }
        }
    }

    /// <summary>Hex input + swatch (UI Toolkit has no runtime color picker).</summary>
    internal sealed class ColorOptionView : OptionView
    {
        private TextField _field;
        private bool _hasShown;
        private Color _shown;
        private VisualElement _swatch;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            var root = new VisualElement();
            root.AddToClassList("atsu-option-color");
            _swatch = new VisualElement();
            _swatch.AddToClassList("atsu-option-color__swatch");
            _field = new TextField { isDelayed = true, maxLength = 9 };
            _field.AddToClassList("atsu-option-color__input");
            _field.RegisterValueChangedCallback(e =>
            {
                if (OptionValueConverter.TryParseColor(e.newValue, out var c))
                {
                    ctx.SetValue(c);
                }
                else
                {
                    _hasShown = false;
                    Refresh(ctx);
                }
            });
            root.Add(_swatch);
            root.Add(_field);
            return root;
        }

        public override void Refresh(OptionContext ctx)
        {
            var c = ctx.TryGetValue(out Color typed) ? typed : (Color)ctx.GetValue();
            if (_hasShown && c == _shown)
            {
                return;
            }

            if (BuiltInOptionViews.HasFocusWithin(_field))
            {
                _swatch.style.backgroundColor = c;
                return; // re-check the text once the user leaves the field
            }

            _hasShown = true;
            _shown = c;
            _swatch.style.backgroundColor = c;
            _field.SetValueWithoutNotify(OptionValueConverter.ToHex(c));
        }
    }

    internal sealed class ReadOnlyOptionView : OptionView
    {
        private Label _label;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            _label = new Label();
            _label.AddToClassList("atsu-option-readonly");
            _label.selection.isSelectable = true;
            return _label;
        }

        private Dictionary<long, string> _enumNames;
        private bool _hasShown;
        private double _shownNumber;
        private object _shownObject;

        public override void Refresh(OptionContext ctx)
        {
            var t = ctx.ValueType;
            string text;
            if (t == typeof(string))
            {
                text = (ctx.TryGetValue(out string s) ? s : (string)ctx.GetValue()) ?? "-";
            }
            else if (t == typeof(bool))
            {
                text = (ctx.TryGetValue(out bool b) ? b : (bool)ctx.GetValue()) ? "On" : "Off";
            }
            else if (t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double))
            {
                // Formatted only when the value changes.
                var n = BuiltInOptionViews.ReadNumber(ctx);
                if (_hasShown && n.Equals(_shownNumber))
                {
                    return;
                }

                _hasShown = true;
                _shownNumber = n;
                text = t == typeof(int) || t == typeof(long)
                    ? NumberText.Get((long)n)
                    : n.ToString("0.###", CultureInfo.InvariantCulture);
            }
            else if (t != null && t.IsEnum && !t.IsDefined(typeof(FlagsAttribute), false))
            {
                var bits = BuiltInOptionViews.ReadEnumBits(ctx);
                if (_hasShown && bits.Equals((long)_shownNumber))
                {
                    return;
                }

                _enumNames ??= BuiltInOptionViews.EnumNames(t);
                _hasShown = true;
                _shownNumber = bits;
                text = _enumNames.TryGetValue(bits, out var name) ? name : ctx.GetValue()?.ToString() ?? "-";
            }
            else
            {
                var value = ctx.GetValue();
                if (_hasShown && Equals(value, _shownObject))
                {
                    return;
                }

                _hasShown = true;
                _shownObject = value; // compared with Equals on the next poll; re-formatted only on change
                text = BuiltInOptionViews.Format(value);
            }

            if (_label.text != text)
            {
                _label.text = text;
            }
        }
    }

    internal sealed class UnsupportedOptionView : OptionView
    {
        public override VisualElement CreateElement(OptionContext ctx)
        {
            var label = new Label(DebuggerStrings.Format("options.unsupported", ctx.ValueType?.Name ?? "-"));
            label.AddToClassList("atsu-option-readonly");
            return label;
        }
    }

    internal sealed class ButtonOptionView : OptionView
    {
        public override bool ShowLabel => false;

        public override VisualElement CreateElement(OptionContext ctx)
        {
            var button = new Button(ctx.Invoke) { text = ctx.Definition.Name };
            button.AddToClassList("atsu-button");
            button.AddToClassList("atsu-option-button");
            return button;
        }
    }
}
