using System;
using System.Collections.Generic;
using System.Text;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Utilities for '/'-separated category paths such as "Player/Status/Buffs".
    ///     Paths are normalized: segments are trimmed and empty segments are dropped.
    /// </summary>
    public static class CategoryPath
    {
        public const char Separator = '/';

        /// <summary>Returns the normalized path, or an empty string if it has no segments.</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            // Fast path: already normalized.
            if (IsNormalized(path))
            {
                return path;
            }

            return string.Join(Separator.ToString(), Split(path));
        }

        public static string[] Split(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Array.Empty<string>();
            }

            var parts = path.Split(Separator);
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0)
                {
                    list.Add(t);
                }
            }

            return list.ToArray();
        }

        public static string Combine(string parent, string child)
        {
            parent = Normalize(parent);
            child = Normalize(child);
            if (parent.Length == 0)
            {
                return child;
            }

            if (child.Length == 0)
            {
                return parent;
            }

            return parent + Separator + child;
        }

        public static string Combine(params string[] segments)
        {
            var sb = new StringBuilder();
            foreach (var s in segments)
            {
                var n = Normalize(s);
                if (n.Length == 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(Separator);
                }

                sb.Append(n);
            }

            return sb.ToString();
        }

        /// <summary>True if <paramref name="path" /> equals <paramref name="ancestor" /> or is below it.</summary>
        public static bool IsSameOrDescendant(string path, string ancestor)
        {
            path = Normalize(path);
            ancestor = Normalize(ancestor);
            if (ancestor.Length == 0)
            {
                return true;
            }

            if (!path.StartsWith(ancestor, StringComparison.Ordinal))
            {
                return false;
            }

            return path.Length == ancestor.Length || path[ancestor.Length] == Separator;
        }

        private static bool IsNormalized(string path)
        {
            if (path[0] == Separator || path[path.Length - 1] == Separator)
            {
                return false;
            }

            if (char.IsWhiteSpace(path[0]) || char.IsWhiteSpace(path[path.Length - 1]))
            {
                return false;
            }

            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] != Separator)
                {
                    continue;
                }

                if (path[i - 1] == Separator || char.IsWhiteSpace(path[i - 1]) || char.IsWhiteSpace(path[i + 1]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}