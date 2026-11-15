using System;
using System.Globalization;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Culture-independent string form of option values for persistence: bool, numbers, string, enums (names,
    ///     Flags as "A, B"), Vector2/3/4 and Color ("x,y,z" with invariant numbers). Other types are not persisted.
    /// </summary>
    internal static class OptionValueSerializer
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool IsSupported(Type type)
        {
            return type != null && (type.IsEnum || type == typeof(string) || type == typeof(bool) ||
                                    OptionValueConverter.IsNumeric(type) || type == typeof(Vector2) ||
                                    type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Color));
        }

        public static bool TrySerialize(object value, Type type, out string text)
        {
            text = null;
            if (!IsSupported(type))
            {
                return false;
            }

            switch (value)
            {
                case null:
                    if (type != typeof(string))
                    {
                        return false;
                    }

                    text = string.Empty;
                    return true;
                case string s:
                    text = s;
                    return true;
                case bool b:
                    text = b ? "true" : "false";
                    return true;
                case Enum e:
                    text = e.ToString();
                    return true;
                case float f:
                    text = f.ToString("R", Inv);
                    return true;
                case double d:
                    text = d.ToString("R", Inv);
                    return true;
                case Vector2 v2:
                    text = Join(v2.x, v2.y);
                    return true;
                case Vector3 v3:
                    text = Join(v3.x, v3.y, v3.z);
                    return true;
                case Vector4 v4:
                    text = Join(v4.x, v4.y, v4.z, v4.w);
                    return true;
                case Color c:
                    text = Join(c.r, c.g, c.b, c.a);
                    return true;
                case IFormattable n:
                    text = n.ToString(null, Inv);
                    return true;
            }

            return false;
        }

        public static bool TryDeserialize(string text, Type type, out object value)
        {
            value = null;
            if (text == null || !IsSupported(type))
            {
                return false;
            }

            try
            {
                if (type == typeof(string))
                {
                    value = text;
                    return true;
                }

                if (type == typeof(bool))
                {
                    if (text == "true")
                    {
                        value = true;
                    }
                    else if (text == "false")
                    {
                        value = false;
                    }
                    else
                    {
                        return false;
                    }

                    return true;
                }

                if (type.IsEnum)
                {
                    value = Enum.Parse(type, text);
                    return true;
                }

                if (OptionValueConverter.IsNumeric(type))
                {
                    if (!double.TryParse(text, NumberStyles.Float, Inv, out var d))
                    {
                        return false;
                    }

                    value = OptionValueConverter.Convert(d, type);
                    return true;
                }

                var parts = Split(text);
                if (parts == null)
                {
                    return false;
                }

                if (type == typeof(Vector2) && parts.Length == 2)
                {
                    value = new Vector2(parts[0], parts[1]);
                }
                else if (type == typeof(Vector3) && parts.Length == 3)
                {
                    value = new Vector3(parts[0], parts[1], parts[2]);
                }
                else if (type == typeof(Vector4) && parts.Length == 4)
                {
                    value = new Vector4(parts[0], parts[1], parts[2], parts[3]);
                }
                else if (type == typeof(Color) && parts.Length == 4)
                {
                    value = new Color(parts[0], parts[1], parts[2], parts[3]);
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (ArgumentException)
            {
                return false; // e.g. an enum member that no longer exists
            }
        }

        private static string Join(params float[] values)
        {
            var parts = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                parts[i] = values[i].ToString("R", Inv);
            }

            return string.Join(",", parts);
        }

        private static float[] Split(string text)
        {
            var parts = text.Split(',');
            var result = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, Inv, out result[i]))
                {
                    return null;
                }
            }

            return result;
        }
    }
}