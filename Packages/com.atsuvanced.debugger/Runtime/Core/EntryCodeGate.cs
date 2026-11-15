using System;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     The PIN that protects the panel (FR-TRG-05). UI independent: the panel view collects digits and calls
    ///     <see cref="TrySubmit" />. Once unlocked, the PIN is not asked again in the session unless <see cref="EveryTime" />
    ///     is on.
    /// </summary>
    internal sealed class EntryCodeGate
    {
        public const int Length = 4;

        private string _code;

        public EntryCodeGate(EntryCodeSettings settings)
        {
            EveryTime = settings.EveryTime;
            if (settings.Required && IsValid(settings.Code))
            {
                _code = settings.Code;
            }
        }

        public bool IsEnabled => _code != null;
        public bool EveryTime { get; set; }
        public bool IsUnlocked { get; private set; }

        /// <summary>Whether the PIN must be entered before the panel opens now.</summary>
        public bool IsRequired => IsEnabled && (EveryTime || !IsUnlocked);

        /// <summary>Number of wrong attempts since the PIN was last requested.</summary>
        public int FailedAttempts { get; private set; }

        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length)
            {
                return false;
            }

            foreach (var c in code)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }

        public void SetCode(string code)
        {
            if (!IsValid(code))
            {
                throw new ArgumentException($"The entry code must be {Length} digits (0-9).", nameof(code));
            }

            _code = code;
            IsUnlocked = false;
            FailedAttempts = 0;
        }

        public void Disable()
        {
            _code = null;
            IsUnlocked = false;
            FailedAttempts = 0;
        }

        public void BeginRequest()
        {
            FailedAttempts = 0;
        }

        public bool TrySubmit(string input)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (input == _code)
            {
                IsUnlocked = true;
                FailedAttempts = 0;
                return true;
            }

            FailedAttempts++;
            return false;
        }
    }
}