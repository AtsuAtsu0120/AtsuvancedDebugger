using System;
using System.Globalization;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>Converts UI values (double / long / string ...) to the option's declared type.</summary>
    public static class OptionValueConverter
    {
        public static bool IsInteger(Type t)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return !t.IsEnum;
                default:
                    return false;
            }
        }

        public static bool IsFloatingPoint(Type t)
        {
            var c = Type.GetTypeCode(t);
            return c == TypeCode.Single || c == TypeCode.Double || c == TypeCode.Decimal;
        }

        public static bool IsNumeric(Type t)
        {
            return IsInteger(t) || IsFloatingPoint(t);
        }

        /// <summary>
        ///     Converts <paramref name="value" /> to <paramref name="type" />; numeric values are clamped to the type's
        ///     range.
        /// </summary>
        public static object Convert(object value, Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (value == null)
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            if (type.IsEnum)
            {
                if (value is string s)
                {
                    return Enum.Parse(type, s);
                }

                return Enum.ToObject(type, System.Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }

            if (IsNumeric(type))
            {
                var d = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
                d = Clamp(d, type);
                if (IsInteger(type))
                {
                    d = Math.Round(d);
                }

                return System.Convert.ChangeType(d, type, CultureInfo.InvariantCulture);
            }

            if (type == typeof(string))
            {
                return System.Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        public static double ToDouble(object value)
        {
            return value == null ? 0d : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Clamps to the numeric limits of <paramref name="type" />.</summary>
        public static double Clamp(double d, Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte: return Math.Max(byte.MinValue, Math.Min(byte.MaxValue, d));
                case TypeCode.SByte: return Math.Max(sbyte.MinValue, Math.Min(sbyte.MaxValue, d));
                case TypeCode.Int16: return Math.Max(short.MinValue, Math.Min(short.MaxValue, d));
                case TypeCode.UInt16: return Math.Max(ushort.MinValue, Math.Min(ushort.MaxValue, d));
                case TypeCode.Int32: return Math.Max(int.MinValue, Math.Min(int.MaxValue, d));
                case TypeCode.UInt32: return Math.Max(uint.MinValue, Math.Min(uint.MaxValue, d));
                case TypeCode.Int64: return Math.Max(long.MinValue, Math.Min(long.MaxValue, d));
                case TypeCode.UInt64: return Math.Max(0d, Math.Min(ulong.MaxValue, d));
                case TypeCode.Single: return Math.Max(float.MinValue, Math.Min(float.MaxValue, d));
                default: return d;
            }
        }

        public static string ToHex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        public static bool TryParseColor(string s, out Color c)
        {
            if (string.IsNullOrEmpty(s))
            {
                c = default;
                return false;
            }

            return ColorUtility.TryParseHtmlString(s.StartsWith("#", StringComparison.Ordinal) ? s : "#" + s, out c);
        }
    }
}