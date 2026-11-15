using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Thread-safe ring buffer of log messages. Writers may be on any thread;
    ///     readers poll <see cref="Version" /> and call <see cref="CopyTo" /> to take a snapshot.
    /// </summary>
    public sealed class ConsoleLogBuffer
    {
        private readonly object _lock = new();
        private readonly long[] _receivedCounts = new long[3];
        private readonly int[] _severityCounts = new int[3];
        private LogEntry[] _buffer;
        private int _count;
        private int _head; // index of the oldest entry
        private long _sequence;
        private int _version;

        public ConsoleLogBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _buffer = new LogEntry[capacity];
        }

        public int Capacity
        {
            get
            {
                lock (_lock)
                {
                    return _buffer.Length;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        /// <summary>Incremented on every mutation. Cheap change detection for the UI.</summary>
        public int Version => Volatile.Read(ref _version);

        /// <summary>Number of entries received for the severity since creation / last Clear (not reduced by eviction).</summary>
        public long GetTotalReceived(LogSeverity severity)
        {
            lock (_lock)
            {
                return _receivedCounts[(int)severity];
            }
        }

        /// <summary>Number of entries currently in the buffer for the given severity.</summary>
        public int GetCount(LogSeverity severity)
        {
            lock (_lock)
            {
                return _severityCounts[(int)severity];
            }
        }

        public void Add(LogType type, string message, string stackTrace)
        {
            lock (_lock)
            {
                var entry = new LogEntry(type, message, stackTrace, DateTime.Now, ++_sequence);
                if (_count == _buffer.Length)
                {
                    _severityCounts[(int)_buffer[_head].Severity]--;
                    _buffer[_head] = entry;
                    _head = (_head + 1) % _buffer.Length;
                }
                else
                {
                    _buffer[(_head + _count) % _buffer.Length] = entry;
                    _count++;
                }

                _severityCounts[(int)entry.Severity]++;
                _receivedCounts[(int)entry.Severity]++;
                _version++;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _count = 0;
                Array.Clear(_severityCounts, 0, _severityCounts.Length);
                Array.Clear(_receivedCounts, 0, _receivedCounts.Length);
                _version++;
            }
        }

        /// <summary>Copies entries oldest-first into <paramref name="destination" /> (cleared first).</summary>
        public void CopyTo(List<LogEntry> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            lock (_lock)
            {
                destination.Clear();
                if (destination.Capacity < _count)
                {
                    // Grow straight to the buffer's capacity: sizing to the current count would re-allocate the list on
                    // every new entry until the buffer is full.
                    destination.Capacity = _buffer.Length;
                }

                for (var i = 0; i < _count; i++)
                {
                    destination.Add(_buffer[(_head + i) % _buffer.Length]);
                }
            }
        }

        /// <summary>
        ///     Copies the newest <paramref name="count" /> entries oldest-first into <paramref name="destination" /> (cleared
        ///     first).
        /// </summary>
        public void CopyLast(int count, List<LogEntry> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            lock (_lock)
            {
                destination.Clear();
                var n = Math.Max(0, Math.Min(count, _count));
                for (var i = _count - n; i < _count; i++)
                {
                    destination.Add(_buffer[(_head + i) % _buffer.Length]);
                }
            }
        }

        /// <summary>Changes capacity, keeping the newest entries.</summary>
        public void Resize(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            lock (_lock)
            {
                if (capacity == _buffer.Length)
                {
                    return;
                }

                var keep = Math.Min(_count, capacity);
                var next = new LogEntry[capacity];
                var skip = _count - keep;
                Array.Clear(_severityCounts, 0, _severityCounts.Length);
                for (var i = 0; i < keep; i++)
                {
                    var e = _buffer[(_head + skip + i) % _buffer.Length];
                    next[i] = e;
                    _severityCounts[(int)e.Severity]++;
                }

                _buffer = next;
                _head = 0;
                _count = keep;
                _version++;
            }
        }
    }
}