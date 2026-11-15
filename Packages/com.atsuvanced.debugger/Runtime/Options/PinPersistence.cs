using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Persists the pin state by <see cref="OptionDefinition.Id" />. Options registered later (e.g. on the next
    ///     launch, or when an enemy spawns again) are pinned as soon as they appear. Unregistering an option keeps
    ///     its pin; only an explicit unpin or <see cref="OptionRegistry.ClearPinned" /> forgets it.
    /// </summary>
    internal sealed class PinPersistence : IDisposable
    {
        internal const string StateKey = "pins";
        private const char Separator = '\n';
        private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

        private readonly OptionRegistry _registry;
        private readonly DebuggerState _state;
        private bool _restoring;

        public PinPersistence(OptionRegistry registry, DebuggerState state)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _state = state ?? throw new ArgumentNullException(nameof(state));

            var stored = state.Get(StateKey);
            if (!string.IsNullOrEmpty(stored))
            {
                foreach (var id in stored.Split(Separator))
                {
                    if (id.Length > 0)
                    {
                        _ids.Add(id);
                    }
                }
            }

            registry.OptionAdded += OnOptionAdded;
            registry.PinStateChanged += OnPinStateChanged;
            registry.PinsCleared += OnPinsCleared;

            foreach (var o in registry.Options.ToArrayCopy())
            {
                OnOptionAdded(o);
            }

            // Options pinned before persistence was attached are remembered too.
            foreach (var o in registry.PinnedOptions)
            {
                _ids.Add(o.Id);
            }

            Write(false);
        }

        /// <summary>Ids that will be pinned when registered (for tests / diagnostics).</summary>
        public IReadOnlyCollection<string> PinnedIds => _ids;

        public void Dispose()
        {
            _registry.OptionAdded -= OnOptionAdded;
            _registry.PinStateChanged -= OnPinStateChanged;
            _registry.PinsCleared -= OnPinsCleared;
        }

        private void OnOptionAdded(OptionDefinition option)
        {
            if (!_ids.Contains(option.Id) || _registry.IsPinned(option))
            {
                return;
            }

            _restoring = true;
            try
            {
                _registry.Pin(option);
            }
            finally
            {
                _restoring = false;
            }
        }

        private void OnPinStateChanged(OptionDefinition option, bool pinned)
        {
            if (pinned)
            {
                // Group members pulled in by a restored pin are remembered too (saved with the next change).
                if (_ids.Add(option.Id))
                {
                    Write(!_restoring);
                }

                return;
            }

            // Unpinned because it was unregistered: keep it for when it comes back.
            if (!_registry.Contains(option))
            {
                return;
            }

            if (_ids.Remove(option.Id))
            {
                Write(true);
            }
        }

        private void OnPinsCleared()
        {
            if (_ids.Count == 0)
            {
                return;
            }

            _ids.Clear();
            Write(true);
        }

        private void Write(bool save)
        {
            _state.Set(StateKey, _ids.Count == 0 ? null : string.Join(Separator.ToString(), _ids));
            if (save)
            {
                _state.Save();
            }
        }
    }

    internal static class ListCopyExtensions
    {
        public static T[] ToArrayCopy<T>(this IReadOnlyList<T> list)
        {
            var a = new T[list.Count];
            for (var i = 0; i < a.Length; i++)
            {
                a[i] = list[i];
            }

            return a;
        }
    }
}