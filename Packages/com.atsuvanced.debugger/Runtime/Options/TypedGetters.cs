using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Typed (non-boxing) getters for option values, so views that poll values several times a second allocate
    ///     nothing. Built once per option; any failure just leaves the option on the boxing path.
    /// </summary>
    internal static class TypedGetters
    {
        private static readonly MethodInfo BindMethod =
            typeof(TypedGetters).GetMethod(nameof(Bind), BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo EnumMethod =
            typeof(TypedGetters).GetMethod(nameof(EnumToLong), BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>A <c>Func&lt;PropertyType&gt;</c> bound to the property getter, or null when not supported.</summary>
        public static Delegate ForProperty(PropertyInfo property, object container)
        {
            var get = property.GetMethod;
            if (get == null || !get.IsPublic)
            {
                return null;
            }

            var target = get.IsStatic ? null : container;
            if (target == null && !get.IsStatic)
            {
                return null;
            }

            var t = property.PropertyType;
            try
            {
                if (t == typeof(bool)) return Bind<bool>(get, target);
                if (t == typeof(int)) return Bind<int>(get, target);
                if (t == typeof(long)) return Bind<long>(get, target);
                if (t == typeof(float)) return Bind<float>(get, target);
                if (t == typeof(double)) return Bind<double>(get, target);
                if (t == typeof(string)) return Bind<string>(get, target);
                if (t == typeof(Vector2)) return Bind<Vector2>(get, target);
                if (t == typeof(Vector3)) return Bind<Vector3>(get, target);
                if (t == typeof(Vector4)) return Bind<Vector4>(get, target);
                if (t == typeof(Color)) return Bind<Color>(get, target);
                if (t.IsEnum)
                {
                    return (Delegate)BindMethod.MakeGenericMethod(t).Invoke(null, new[] { get, target });
                }
            }
            catch (Exception)
            {
                // Unusual targets (e.g. struct containers) keep the reflection getter.
            }

            return null;
        }

        /// <summary>The integral value of an int-backed enum as a non-boxing getter, or null.</summary>
        public static Func<long> EnumAsLong(Type type, Delegate typedGetter)
        {
            if (typedGetter == null || type == null || !type.IsEnum || Enum.GetUnderlyingType(type) != typeof(int))
            {
                return null;
            }

            try
            {
                return (Func<long>)EnumMethod.MakeGenericMethod(type).Invoke(null, new object[] { typedGetter });
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Func<T> Bind<T>(MethodInfo get, object target)
        {
            return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), target, get, false);
        }

        private static Func<long> EnumToLong<TEnum>(Delegate typedGetter) where TEnum : struct, IConvertible
        {
            var getter = (Func<TEnum>)typedGetter;
            return () => UnsafeUtility.EnumToInt(getter());
        }
    }
}
