using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>Cached decimal strings of small non-negative integers (counts shown every frame allocate nothing).</summary>
    internal static class NumberText
    {
        private const int CacheSize = 4096;
        private static readonly string[] Cache = new string[CacheSize];

        public static string Get(int value)
        {
            if ((uint)value >= CacheSize)
            {
                return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return Cache[value] ??= value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Get(long value)
        {
            return value >= 0 && value < CacheSize
                ? Get((int)value)
                : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///     Display strings of log entries, built once per entry and reused while the entry stays in the cache
    ///     (a ring indexed by <see cref="LogEntry.Sequence" />). Rebinding rows / shifting docked lines then allocates
    ///     nothing for entries already seen.
    /// </summary>
    internal sealed class LogTextCache
    {
        private const int MaxFirstLine = 300;
        private static readonly string[] TypeNames = BuildTypeNames();

        private readonly Slot[] _slots;
        private readonly char[] _buffer = new char[32];
        private long _lastSecond = -1;
        private string _lastTime;

        public LogTextCache(int capacity)
        {
            _slots = new Slot[Mathf.Max(8, capacity)];
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i].Sequence = -1;
            }
        }

        /// <summary>First line of the message (at most 300 chars). The message itself when it is a single short line.</summary>
        public string FirstLine(in LogEntry e)
        {
            ref var s = ref GetSlot(e);
            return s.FirstLine ??= MakeFirstLine(e.Message);
        }

        /// <summary>"HH:mm:ss" of the entry (entries logged in the same second share the string).</summary>
        public string Time(in LogEntry e)
        {
            ref var s = ref GetSlot(e);
            if (s.Time != null)
            {
                return s.Time;
            }

            var second = e.Timestamp.Ticks / TimeSpan.TicksPerSecond;
            if (second != _lastSecond || _lastTime == null)
            {
                _lastSecond = second;
                _lastTime = Format(e.Timestamp, false, null);
            }

            return s.Time = _lastTime;
        }

        /// <summary>"HH:mm:ss.fff  Type" of the entry.</summary>
        public string Meta(in LogEntry e)
        {
            ref var s = ref GetSlot(e);
            return s.Meta ??= Format(e.Timestamp, true, TypeName(e.Type));
        }

        public static string TypeName(LogType type)
        {
            var i = (int)type;
            return (uint)i < (uint)TypeNames.Length ? TypeNames[i] : type.ToString();
        }

        public void Clear()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new Slot { Sequence = -1 };
            }
        }

        private ref Slot GetSlot(in LogEntry e)
        {
            ref var s = ref _slots[(int)(e.Sequence % _slots.Length)];
            if (s.Sequence != e.Sequence)
            {
                s = new Slot { Sequence = e.Sequence };
            }

            return ref s;
        }

        private static string MakeFirstLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            var nl = message.IndexOf('\n');
            var length = nl >= 0 ? nl : message.Length;
            if (length > 0 && message[length - 1] == '\r')
            {
                length--;
            }

            if (length > MaxFirstLine)
            {
                return message.Substring(0, MaxFirstLine) + "…";
            }

            return length == message.Length ? message : message.Substring(0, length);
        }

        private string Format(DateTime t, bool millis, string suffix)
        {
            var b = _buffer;
            var n = 0;
            Two(b, ref n, t.Hour);
            b[n++] = ':';
            Two(b, ref n, t.Minute);
            b[n++] = ':';
            Two(b, ref n, t.Second);
            if (millis)
            {
                var ms = t.Millisecond;
                b[n++] = '.';
                b[n++] = (char)('0' + ms / 100);
                b[n++] = (char)('0' + ms / 10 % 10);
                b[n++] = (char)('0' + ms % 10);
            }

            if (suffix == null)
            {
                return new string(b, 0, n);
            }

            if (n + 2 + suffix.Length > b.Length)
            {
                return string.Concat(new string(b, 0, n), "  ", suffix);
            }

            b[n++] = ' ';
            b[n++] = ' ';
            suffix.CopyTo(0, b, n, suffix.Length);
            return new string(b, 0, n + suffix.Length);
        }

        private static void Two(char[] b, ref int n, int v)
        {
            b[n++] = (char)('0' + v / 10);
            b[n++] = (char)('0' + v % 10);
        }

        private static string[] BuildTypeNames()
        {
            var names = new string[5];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = ((LogType)i).ToString();
            }

            return names;
        }

        private struct Slot
        {
            public long Sequence;
            public string FirstLine;
            public string Time;
            public string Meta;
        }
    }
}

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Formatted numbers keyed by their value rounded to the displayed precision: a value that shows the same text
    ///     reuses the string, so labels refreshed several times a second (FPS, frame times, counters) allocate only when
    ///     a value is shown for the first time. Bounded (cleared when full).
    /// </summary>
    internal sealed class ValueTextCache
    {
        private const int Limit = 256;
        private readonly Dictionary<long, string> _cache = new();
        private readonly string _format;
        private readonly double _scale;
        private readonly string _suffix;

        /// <summary>For <see cref="GetCounter" /> only.</summary>
        public ValueTextCache() : this(0, null)
        {
        }

        public ValueTextCache(int decimals, string suffix)
        {
            _format = "F" + decimals.ToString(CultureInfo.InvariantCulture);
            _scale = Math.Pow(10, decimals);
            _suffix = suffix ?? string.Empty;
        }

        /// <summary><paramref name="value" /> with the decimals and suffix given to the constructor.</summary>
        public string Get(double value)
        {
            if (!TryKey(value * _scale, out var key))
            {
                return value.ToString(_format, CultureInfo.InvariantCulture) + _suffix;
            }

            if (_cache.TryGetValue(key, out var text))
            {
                return text;
            }

            text = value.ToString(_format, CultureInfo.InvariantCulture) + _suffix;
            Add(key, text);
            return text;
        }

        /// <summary>Same text as <see cref="ProfilerCounter.Format" />.</summary>
        public string GetCounter(double value, ProfilerUnit unit)
        {
            if (!TryKey(CounterScaled(value, unit, out var tier), out var q))
            {
                return ProfilerCounter.Format(value, unit);
            }

            var key = q * 8 + tier;
            if (_cache.TryGetValue(key, out var text))
            {
                return text;
            }

            text = ProfilerCounter.Format(value, unit);
            Add(key, text);
            return text;
        }

        private void Add(long key, string text)
        {
            if (_cache.Count >= Limit)
            {
                _cache.Clear();
            }

            _cache[key] = text;
        }

        private static bool TryKey(double scaled, out long key)
        {
            if (double.IsNaN(scaled) || double.IsInfinity(scaled) || Math.Abs(scaled) > 1e15)
            {
                key = 0;
                return false;
            }

            key = (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
            return true;
        }

        // Mirrors ProfilerCounter.Format / FormatBytes: the value scaled to its shown decimals, plus a unit tier.
        private static double CounterScaled(double value, ProfilerUnit unit, out int tier)
        {
            tier = 0;
            switch (unit)
            {
                case ProfilerUnit.Bytes:
                {
                    var abs = Math.Abs(value);
                    if (abs >= 1024.0 * 1024 * 1024)
                    {
                        tier = 3;
                        return value / (1024.0 * 1024 * 1024) * 100;
                    }

                    if (abs >= 1024.0 * 1024)
                    {
                        tier = 2;
                        return value / (1024.0 * 1024) * 10;
                    }

                    if (abs >= 1024.0)
                    {
                        tier = 1;
                        return value / 1024.0 * 10;
                    }

                    return value;
                }
                case ProfilerUnit.Milliseconds: tier = 4; return value * 100;
                case ProfilerUnit.Nanoseconds: tier = 4; return value / 1_000_000.0 * 100;
                case ProfilerUnit.Percent: tier = 5; return value * 1000;
                case ProfilerUnit.Raw: tier = 6; return value * 100;
                default: tier = 7; return value;
            }
        }
    }
}
