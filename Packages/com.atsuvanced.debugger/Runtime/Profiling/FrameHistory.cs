using System;

namespace Atsuvanced.Debugger
{
    /// <summary>Fixed-size ring buffer of per-frame samples (e.g. frame times in ms). Allocation free after construction.</summary>
    public sealed class FrameHistory
    {
        private int _head; // index of the oldest sample
        private float[] _values;

        public FrameHistory(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _values = new float[capacity];
        }

        public int Capacity => _values.Length;
        public int Count { get; private set; }

        /// <summary>Incremented on every change (cheap change detection for drawing).</summary>
        public int Version { get; private set; }

        /// <summary>Sample by age: 0 = oldest, Count - 1 = newest.</summary>
        public float this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _values[(_head + index) % _values.Length];
            }
        }

        /// <summary>The newest sample, or 0 when empty.</summary>
        public float Latest => Count == 0 ? 0f : this[Count - 1];

        public void Add(float value)
        {
            if (Count == _values.Length)
            {
                _values[_head] = value;
                _head = (_head + 1) % _values.Length;
            }
            else
            {
                _values[(_head + Count) % _values.Length] = value;
                Count++;
            }

            Version++;
        }

        public void Clear()
        {
            _head = 0;
            Count = 0;
            Version++;
        }

        /// <summary>Changes the capacity, keeping the newest samples.</summary>
        public void Resize(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (capacity == _values.Length)
            {
                return;
            }

            var keep = Math.Min(Count, capacity);
            var next = new float[capacity];
            for (var i = 0; i < keep; i++)
            {
                next[i] = this[Count - keep + i];
            }

            _values = next;
            _head = 0;
            Count = keep;
            Version++;
        }

        /// <summary>Min / average / max of the newest <paramref name="lastCount" /> samples (all when &lt;= 0).</summary>
        public void GetStats(out float min, out float average, out float max, int lastCount = 0)
        {
            var n = lastCount <= 0 ? Count : Math.Min(lastCount, Count);
            if (n == 0)
            {
                min = average = max = 0f;
                return;
            }

            min = float.MaxValue;
            max = float.MinValue;
            var sum = 0.0;
            for (var i = Count - n; i < Count; i++)
            {
                var v = this[i];
                if (v < min)
                {
                    min = v;
                }

                if (v > max)
                {
                    max = v;
                }

                sum += v;
            }

            average = (float)(sum / n);
        }

        /// <summary>Sum of the newest samples until <paramref name="window" /> is reached; returns how many were summed.</summary>
        public int SumNewest(float window, out float sum)
        {
            sum = 0f;
            var n = 0;
            for (var i = Count - 1; i >= 0 && sum < window; i--, n++)
            {
                sum += this[i];
            }

            return n;
        }
    }
}