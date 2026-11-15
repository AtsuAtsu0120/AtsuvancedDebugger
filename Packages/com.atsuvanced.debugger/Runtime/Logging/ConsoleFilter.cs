using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Filtering / collapsing of console entries (UI independent).
    ///     Text search is a case-insensitive substring match on the message, or a regular expression.
    /// </summary>
    public sealed class ConsoleFilter
    {
        // Reused between Apply calls (the console re-applies the filter several times a second while logs flow).
        private readonly Dictionary<CollapseKey, int> _collapseIndex = new(new CollapseKeyComparer());
        private Regex _regex;

        private string _text = string.Empty;
        private bool _useRegex;

        public bool ShowInfo { get; set; } = true;
        public bool ShowWarning { get; set; } = true;
        public bool ShowError { get; set; } = true;

        /// <summary>Merge identical entries (same type, message and stack trace) into one row with a count.</summary>
        public bool Collapse { get; set; }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;
                RebuildRegex();
            }
        }

        public bool UseRegex
        {
            get => _useRegex;
            set
            {
                _useRegex = value;
                RebuildRegex();
            }
        }

        /// <summary>
        ///     False when <see cref="UseRegex" /> is on and <see cref="Text" /> is not a valid pattern (text filter is then
        ///     ignored).
        /// </summary>
        public bool IsRegexValid { get; private set; } = true;

        public bool IsSeverityVisible(LogSeverity s)
        {
            return s == LogSeverity.Info ? ShowInfo : s == LogSeverity.Warning ? ShowWarning : ShowError;
        }

        public bool Matches(in LogEntry e)
        {
            if (!IsSeverityVisible(e.Severity))
            {
                return false;
            }

            if (_text.Length == 0)
            {
                return true;
            }

            if (_useRegex)
            {
                return _regex == null || _regex.IsMatch(e.Message);
            }

            return e.Message.IndexOf(_text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Fills <paramref name="destination" /> (cleared first) in source order.</summary>
        public void Apply(IReadOnlyList<LogEntry> source, List<Item> destination)
        {
            destination.Clear();
            Dictionary<CollapseKey, int> index = null;
            if (Collapse)
            {
                index = _collapseIndex;
                index.Clear();
            }

            for (var i = 0; i < source.Count; i++)
            {
                var e = source[i];
                if (!Matches(e))
                {
                    continue;
                }

                if (index == null)
                {
                    destination.Add(new Item(e, 1));
                    continue;
                }

                var key = new CollapseKey(e.Type, e.Message, e.StackTrace);
                if (index.TryGetValue(key, out var at))
                {
                    // Keep the first position, show the latest occurrence.
                    destination[at] = new Item(e, destination[at].Count + 1);
                }
                else
                {
                    index.Add(key, destination.Count);
                    destination.Add(new Item(e, 1));
                }
            }

            // Do not keep the strings of the last snapshot alive.
            index?.Clear();
        }

        private void RebuildRegex()
        {
            _regex = null;
            IsRegexValid = true;
            if (!_useRegex || _text.Length == 0)
            {
                return;
            }

            try
            {
                _regex = new Regex(_text, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                IsRegexValid = false;
            }
        }

        private readonly struct CollapseKey
        {
            public readonly LogType Type;
            public readonly string Message;
            public readonly string StackTrace;

            public CollapseKey(LogType type, string message, string stackTrace)
            {
                Type = type;
                Message = message;
                StackTrace = stackTrace;
            }
        }

        /// <summary>Ordinal comparer without boxing (the default comparer of a tuple with an enum may box on Mono).</summary>
        private sealed class CollapseKeyComparer : IEqualityComparer<CollapseKey>
        {
            public bool Equals(CollapseKey a, CollapseKey b)
            {
                return a.Type == b.Type && string.Equals(a.Message, b.Message) &&
                       string.Equals(a.StackTrace, b.StackTrace);
            }

            public int GetHashCode(CollapseKey k)
            {
                unchecked
                {
                    var h = (int)k.Type;
                    h = h * 397 ^ (k.Message?.GetHashCode() ?? 0);
                    return h * 397 ^ (k.StackTrace?.GetHashCode() ?? 0);
                }
            }
        }

        public readonly struct Item
        {
            public readonly LogEntry Entry;

            /// <summary>Number of collapsed occurrences (1 when not collapsed).</summary>
            public readonly int Count;

            public Item(LogEntry entry, int count)
            {
                Entry = entry;
                Count = count;
            }
        }
    }
}