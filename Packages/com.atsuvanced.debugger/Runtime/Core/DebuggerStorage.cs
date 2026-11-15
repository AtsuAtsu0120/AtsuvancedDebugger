using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Key-value storage used to persist the debugger's own state (pinned options, selected tab / section,
    ///     open categories, last search). Replace it with <see cref="DebuggerBuilder.UseStorage" /> before initialization
    ///     (<c>AtsuDebug.Configure(b => b.UseStorage(...))</c> from
    ///     <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]</c>).
    /// </summary>
    public interface IDebuggerStorage
    {
        /// <summary>Returns the stored value, or null if the key does not exist.</summary>
        string GetString(string key);

        void SetString(string key, string value);

        void DeleteKey(string key);

        /// <summary>Flushes pending writes. Called when the panel closes and when the pin state changes.</summary>
        void Save();
    }

    /// <summary>Default storage backed by <see cref="PlayerPrefs" />.</summary>
    public sealed class PlayerPrefsDebuggerStorage : IDebuggerStorage
    {
        public string GetString(string key)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? string.Empty);
        }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }

    /// <summary>In-memory storage: nothing survives a restart. Used when persistence is disabled and in tests.</summary>
    public sealed class MemoryDebuggerStorage : IDebuggerStorage
    {
        private readonly Dictionary<string, string> _values = new();

        public string GetString(string key)
        {
            return _values.TryGetValue(key, out var v) ? v : null;
        }

        public void SetString(string key, string value)
        {
            _values[key] = value ?? string.Empty;
        }

        public void DeleteKey(string key)
        {
            _values.Remove(key);
        }

        public void Save()
        {
        }
    }

    /// <summary>
    ///     Persisted state of the debugger and its pages on top of an <see cref="IDebuggerStorage" />
    ///     (available as <see cref="IDebugService.State" />). Values are cached, so repeated writes of the same value do not
    ///     touch
    ///     the storage; call <see cref="Save" /> to flush (the panel also saves when it closes).
    ///     <para>
    ///         Keys share one namespace with the built-in pages (<c>"tab"</c>, <c>"section/..."</c>, <c>"open/..."</c>,
    ///         <c>"search"</c>,
    ///         <c>"pins"</c>, <c>"pinned/..."</c>, <c>"dock/..."</c>, <c>"profiler/..."</c>, <c>"value/..."</c>), so prefix
    ///         your own keys
    ///         (e.g. <c>"mygame.cheats/last-stage"</c>). Every key is stored as <see cref="KeyPrefix" /> + key.
    ///     </para>
    /// </summary>
    public sealed class DebuggerState
    {
        /// <summary>Prefix added to every key in the underlying storage.</summary>
        public const string KeyPrefix = "AtsuvancedDebugger/";

        /// <summary>Newline-separated list of every key written through this class (storages cannot enumerate keys).</summary>
        internal const string IndexKey = "__keys";

        private readonly Dictionary<string, string> _cache = new();
        private bool _dirty;

        private HashSet<string> _index;

        internal DebuggerState(IDebuggerStorage storage)
        {
            Storage = storage ?? new MemoryDebuggerStorage();
        }

        /// <summary>Every key written so far (from the index stored with the values).</summary>
        public IReadOnlyCollection<string> Keys => LoadIndex();

        internal IDebuggerStorage Storage { get; }

        private HashSet<string> LoadIndex()
        {
            if (_index != null)
            {
                return _index;
            }

            _index = new HashSet<string>();
            string stored = null;
            try
            {
                stored = Storage.GetString(KeyPrefix + IndexKey);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (!string.IsNullOrEmpty(stored))
            {
                foreach (var k in stored.Split('\n'))
                {
                    if (k.Length > 0)
                    {
                        _index.Add(k);
                    }
                }
            }

            return _index;
        }

        private void AddToIndex(string key)
        {
            if (!LoadIndex().Add(key))
            {
                return;
            }

            try
            {
                Storage.SetString(KeyPrefix + IndexKey, string.Join("\n", _index));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        ///     Deletes every key written by the debugger (pins, tab / section, open categories, search, saved option values).
        ///     Returns the number of keys deleted.
        /// </summary>
        public int Clear()
        {
            var keys = new List<string>(LoadIndex());
            try
            {
                foreach (var key in keys)
                {
                    Storage.DeleteKey(KeyPrefix + key);
                }

                Storage.DeleteKey(KeyPrefix + IndexKey);
                Storage.Save();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            _index.Clear();
            foreach (var key in keys)
            {
                _cache[key] = null;
            }

            _dirty = false;
            return keys.Count;
        }

        /// <summary>Returns the stored value, or null if the key does not exist.</summary>
        public string Get(string key)
        {
            if (_cache.TryGetValue(key, out var v))
            {
                return v;
            }

            try
            {
                v = Storage.GetString(KeyPrefix + key);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                v = null;
            }

            _cache[key] = v;
            return v;
        }

        /// <summary>Stores the value (null or empty deletes the key).</summary>
        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                value = null;
            }

            if (_cache.TryGetValue(key, out var old) && old == value)
            {
                return;
            }

            _cache[key] = value;
            _dirty = true;
            try
            {
                if (value == null)
                {
                    Storage.DeleteKey(KeyPrefix + key);
                }
                else
                {
                    Storage.SetString(KeyPrefix + key, value);
                }

                if (value != null)
                {
                    AddToIndex(key);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Flushes pending writes to the storage.</summary>
        public void Save()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            try
            {
                Storage.Save();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}