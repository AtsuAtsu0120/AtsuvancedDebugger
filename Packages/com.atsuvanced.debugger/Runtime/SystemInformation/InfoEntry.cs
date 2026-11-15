using System;

namespace Atsuvanced.Debugger
{
    /// <summary>A single row of the System Info tab. The value is evaluated lazily.</summary>
    public sealed class InfoEntry
    {
        private readonly object _constant;
        private readonly Func<object> _valueGetter;

        private InfoEntry(string title, Func<object> valueGetter, bool isPrivate)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            _valueGetter = valueGetter ?? throw new ArgumentNullException(nameof(valueGetter));
            IsPrivate = isPrivate;
        }

        private InfoEntry(string title, object value, bool isPrivate)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            _constant = value;
            IsConstant = true;
            IsPrivate = isPrivate;
        }

        public string Title { get; }

        /// <summary>Private entries are shown in the panel but excluded from bug reports.</summary>
        public bool IsPrivate { get; }

        /// <summary>True for entries created with a fixed value (views can format them once).</summary>
        public bool IsConstant { get; }

        public object Value
        {
            get
            {
                if (IsConstant)
                {
                    return _constant;
                }

                try
                {
                    return _valueGetter();
                }
                catch (Exception e)
                {
                    return $"Error ({e.GetType().Name})";
                }
            }
        }

        public static InfoEntry Create(string title, Func<object> valueGetter, bool isPrivate = false)
        {
            return new InfoEntry(title, valueGetter, isPrivate);
        }

        public static InfoEntry Create(string title, object value, bool isPrivate = false)
        {
            return new InfoEntry(title, value, isPrivate);
        }
    }
}