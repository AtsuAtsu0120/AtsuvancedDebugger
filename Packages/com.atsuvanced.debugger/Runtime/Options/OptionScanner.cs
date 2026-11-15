using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Builds <see cref="OptionDefinition" />s from an arbitrary object via reflection.
    ///     Member metadata is cached per type; only the bound delegates are created per instance.
    /// </summary>
    public static class OptionScanner
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        private static readonly Dictionary<Type, MemberMeta[]> Cache = new();

        private static readonly Dictionary<Type, (string, int)[]> SortCache = new();

        /// <summary>Builds the options of <paramref name="container" />.</summary>
        /// <param name="isExtraValueType">
        ///     Value types accepted beyond <see cref="IsSupportedType" /> (e.g. types with a registered view). Null = built-in
        ///     types only.
        /// </param>
        public static List<OptionDefinition> Scan(object container, Func<Type, bool> isExtraValueType = null)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            var metas = GetMeta(container.GetType());
            var result = new List<OptionDefinition>(metas.Length);
            foreach (var m in metas)
            {
                // The type filter is applied here, not in the cache, so registering a type view later needs no cache reset.
                if (m.Property != null && !IsSupportedType(m.Property.PropertyType) &&
                    (isExtraValueType == null || !isExtraValueType(m.Property.PropertyType)))
                {
                    continue;
                }

                var definition = Build(container, m);

                if (m.Persist != null)
                {
                    definition.WithPersistence(m.Persist.Key);
                }

                result.Add(definition);
            }

            return result;
        }

        private static OptionDefinition Build(object container, MemberMeta m)
        {
            if (m.Property != null)
            {
                var p = m.Property;
                var getter = p.GetMethod != null && p.GetMethod.IsPublic
                    ? (Func<object>)(() => p.GetValue(container))
                    : null;
                var setter = p.SetMethod != null && p.SetMethod.IsPublic
                    ? (Action<object>)(v => p.SetValue(container, v))
                    : null;
                var option = OptionDefinition.CreateUntyped(
                    m.Name, m.Category, m.Sort, p.PropertyType, getter, setter, m.Range, m.Increment, p.Name,
                    m.Description, m.Group);
                if (getter != null)
                {
                    option.SetTypedGetter(TypedGetters.ForProperty(p, container));
                }

                return option;
            }

            var method = m.Method;
            return OptionDefinition.CreateMethod(
                m.Name, m.Category, m.Sort, () => method.Invoke(container, null), method.Name,
                m.Description, m.Group);
        }

        /// <summary>Whether a property of this type can be displayed as an option.</summary>
        public static bool IsSupportedType(Type type)
        {
            if (type.IsEnum)
            {
                return true;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.String:
                    return true;
            }

            return type == typeof(Vector2) || type == typeof(Vector3) ||
                   type == typeof(Vector4) || type == typeof(Color);
        }

        /// <summary>
        ///     Category orders declared with <see cref="OptionCategorySortAttribute" /> on the class (paths are relative to the
        ///     container prefix).
        /// </summary>
        internal static IReadOnlyList<(string path, int priority)> GetCategorySorts(Type type)
        {
            lock (SortCache)
            {
                if (SortCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                var list = new List<(string, int)>();
                var classCategory = ResolveClassCategory(type);
                foreach (var a in type.GetCustomAttributes<OptionCategorySortAttribute>(true))
                {
                    var path = a.Path ?? classCategory;
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogWarning($"[AtsuvancedDebugger] [OptionCategorySort] on {type.Name} needs a path " +
                                         "or an [OptionCategory] on the class.");
                        continue;
                    }

                    list.Add((path, a.SortPriority));
                }

                var result = list.ToArray();
                SortCache[type] = result;
                return result;
            }
        }

        private static MemberMeta[] GetMeta(Type type)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                var built = BuildMeta(type);
                Cache[type] = built;
                return built;
            }
        }

        private static MemberMeta[] BuildMeta(Type type)
        {
            var list = new List<MemberMeta>();
            var classCategory = ResolveClassCategory(type);

            foreach (var p in type.GetProperties(Flags))
            {
                if (p.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (p.GetMethod == null || !p.GetMethod.IsPublic)
                {
                    continue;
                }

                if (IsHidden(p))
                {
                    continue;
                }

                list.Add(new MemberMeta
                {
                    Property = p,
                    Name = ResolveName(p),
                    Category = CategoryPath.Combine(classCategory, ResolveCategory(p)),
                    Sort = ResolveSort(p),
                    Description = ResolveDescription(p),
                    Group = p.GetCustomAttribute<OptionGroupAttribute>(true),
                    Range = p.GetCustomAttribute<NumberRangeAttribute>(true),
                    Increment = p.GetCustomAttribute<IncrementAttribute>(true)?.Increment,
                    Persist = p.GetCustomAttribute<OptionPersistAttribute>(true)
                });
            }

            foreach (var m in type.GetMethods(Flags))
            {
                if (m.IsSpecialName || m.IsGenericMethodDefinition)
                {
                    continue;
                }

                if (m.DeclaringType == typeof(object))
                {
                    continue;
                }

                if (m.GetParameters().Length != 0)
                {
                    continue;
                }

                if (m.ReturnType != typeof(void))
                {
                    continue;
                }

                if (m.IsDefined(typeof(CompilerGeneratedAttribute), false))
                {
                    continue;
                }

                if (IsHidden(m))
                {
                    continue;
                }

                list.Add(new MemberMeta
                {
                    Method = m,
                    Name = ResolveName(m),
                    Category = CategoryPath.Combine(classCategory, ResolveCategory(m)),
                    Sort = ResolveSort(m),
                    Description = ResolveDescription(m),
                    Group = m.GetCustomAttribute<OptionGroupAttribute>(true)
                });
            }

            return list.ToArray();
        }

        private static bool IsHidden(MemberInfo member)
        {
            if (member.IsDefined(typeof(HiddenOptionAttribute), true))
            {
                return true;
            }

            var browsable = member.GetCustomAttribute<BrowsableAttribute>(true);
            return browsable != null && !browsable.Browsable;
        }

        private static string ResolveName(MemberInfo member)
        {
            var own = member.GetCustomAttribute<OptionNameAttribute>(true);
            if (own != null)
            {
                return own.Name;
            }

            var cm = member.GetCustomAttribute<DisplayNameAttribute>(true);
            if (cm != null && !string.IsNullOrEmpty(cm.DisplayName))
            {
                return cm.DisplayName;
            }

            return member.Name;
        }

        private static string ResolveCategory(MemberInfo member)
        {
            var own = member.GetCustomAttribute<OptionCategoryAttribute>(true);
            if (own != null)
            {
                return own.Category;
            }

            var cm = member.GetCustomAttribute<CategoryAttribute>(true);
            if (cm != null)
            {
                return cm.Category;
            }

            return null;
        }

        /// <summary>Category declared on the container class; parent of every member category.</summary>
        private static string ResolveClassCategory(Type type)
        {
            var own = type.GetCustomAttribute<OptionCategoryAttribute>(true);
            if (own != null)
            {
                return own.Category;
            }

            var cm = type.GetCustomAttribute<CategoryAttribute>(true);
            return cm?.Category;
        }

        private static string ResolveDescription(MemberInfo member)
        {
            var own = member.GetCustomAttribute<OptionDescriptionAttribute>(true);
            if (own != null)
            {
                return own.Description;
            }

            return member.GetCustomAttribute<DescriptionAttribute>(true)?.Description;
        }

        private static int ResolveSort(MemberInfo member)
        {
            return member.GetCustomAttribute<OptionSortAttribute>(true)?.SortPriority ?? 0;
        }

        private sealed class MemberMeta
        {
            public string Category;
            public string Description;
            public OptionGroupAttribute Group;
            public double? Increment;
            public MethodInfo Method;
            public string Name;
            public OptionPersistAttribute Persist;
            public PropertyInfo Property;
            public NumberRangeAttribute Range;
            public int Sort;
        }
    }
}