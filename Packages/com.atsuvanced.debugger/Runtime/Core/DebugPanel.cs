using System;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     The debug panel: visibility, the PIN that protects it, and screen capture (<see cref="IDebugService.Panel" />
    ///     ).
    /// </summary>
    public interface IDebugPanel
    {
        bool IsVisible { get; }

        // ---- Entry code -----------------------------------------------------------------

        /// <summary>True when a PIN protects the panel.</summary>
        bool IsEntryCodeEnabled { get; }

        /// <summary>True while the PIN pad is shown.</summary>
        bool IsEntryCodeShown { get; }

        /// <summary>Raised after the panel opens (true) or closes (false).</summary>
        event Action<bool> VisibilityChanged;

        /// <summary>
        ///     Opens the panel on a tab (null = the tab shown last time, or <see cref="GeneralSettings.DefaultTab" />). With
        ///     <paramref name="requireEntryCode" /> the PIN pad is shown first when a PIN is set and not yet entered.
        ///     Selects the tab when the panel is already open.
        /// </summary>
        void Show(string tabId = null, bool requireEntryCode = true);

        void Hide();

        /// <summary>Hides and releases all UI resources and pages. Everything is rebuilt on the next show.</summary>
        void Destroy();

        /// <summary>True if the screen position (pixels, origin bottom-left like Input.mousePosition) is over debugger UI.</summary>
        bool IsScreenPointOver(Vector2 screenPosition);

        /// <summary>
        ///     Captures the game screen without any debugger UI (PNG). The callback runs at the end of a later frame;
        ///     it receives null in edit mode or when the capture failed.
        /// </summary>
        void CaptureScreenshot(Action<byte[]> onCaptured);

        /// <summary>Protects the panel with a 4-digit PIN ("0000"-"9999"). Overrides the settings.</summary>
        void SetEntryCode(string code);

        void DisableEntryCode();
    }

    /// <summary>State of the panel (UI independent); the view only reflects it.</summary>
    internal sealed class DebugPanel : IDebugPanel
    {
        internal const string TabStateKey = "tab";

        private readonly PageNavigator _navigator;
        private readonly DebuggerSettings _settings;
        private readonly DebuggerState _state;
        private readonly DebugTabRegistry _tabs;
        private string _entryCodeTabId;

        public DebugPanel(DebuggerSettings settings, DebuggerState state, DebugTabRegistry tabs,
            PageNavigator navigator)
        {
            _settings = settings;
            _state = state;
            _tabs = tabs;
            _navigator = navigator;
            EntryCode = new EntryCodeGate(settings.EntryCode);
            navigator.Changed += OnNavigatorChanged;
        }

        internal EntryCodeGate EntryCode { get; }

        internal IDebugPanelView View { get; private set; } = NullPanelView.Instance;

        public bool IsVisible { get; private set; }
        public event Action<bool> VisibilityChanged;

        public bool IsEntryCodeEnabled => EntryCode.IsEnabled;
        public bool IsEntryCodeShown { get; private set; }

        public void Show(string tabId = null, bool requireEntryCode = true)
        {
            if (!IsVisible && requireEntryCode && EntryCode.IsRequired)
            {
                RequestEntryCode(tabId);
                return;
            }

            tabId ??= _navigator.CurrentTabId == null ? InitialTabId() : null;
            if (!IsVisible)
            {
                Opening?.Invoke();
            }

            _navigator.EnsureTabSelected(tabId);
            if (IsVisible)
            {
                return;
            }

            IsVisible = true;
            View.Show();
            _navigator.SetActive(true);
            Raise(true);
        }

        public void Hide()
        {
            if (!IsVisible)
            {
                return;
            }

            IsVisible = false;
            _navigator.SetActive(false);
            View.Hide();
            _state.Save();
            Raise(false);
        }

        public void Destroy()
        {
            CancelEntryCode();
            Hide();
            _navigator.Reset();
            View.Destroy();
        }

        public bool IsScreenPointOver(Vector2 screenPosition)
        {
            return View.IsScreenPointOverPanel(screenPosition);
        }

        public void CaptureScreenshot(Action<byte[]> onCaptured)
        {
            if (onCaptured == null)
            {
                throw new ArgumentNullException(nameof(onCaptured));
            }

            View.CaptureScreenshot(onCaptured);
        }

        public void SetEntryCode(string code)
        {
            EntryCode.SetCode(code);
        }

        public void DisableEntryCode()
        {
            EntryCode.Disable();
            if (IsEntryCodeShown)
            {
                SubmitEntryCode(null); // opens the panel that was waiting for the PIN
            }
        }

        /// <summary>Raised when the PIN pad is closed without the right code.</summary>
        internal event Action EntryCodeCancelled;

        /// <summary>Raised just before the panel opens (e.g. to drop options of destroyed containers first).</summary>
        internal event Action Opening;

        internal void SetView(IDebugPanelView view)
        {
            View = view ?? NullPanelView.Instance;
            if (IsVisible)
            {
                View.Show();
            }

            if (IsEntryCodeShown)
            {
                View.ShowEntryCode();
            }
        }

        /// <summary>Checks the PIN; on success the panel opens on the tab that was requested.</summary>
        internal bool SubmitEntryCode(string code)
        {
            if (!IsEntryCodeShown)
            {
                return false;
            }

            if (!EntryCode.TrySubmit(code))
            {
                return false;
            }

            var tab = _entryCodeTabId;
            CloseEntryCode();
            Show(tab, false);
            return true;
        }

        internal void CancelEntryCode()
        {
            if (!IsEntryCodeShown)
            {
                return;
            }

            CloseEntryCode();
            EntryCodeCancelled?.Invoke();
        }

        private void RequestEntryCode(string tabId)
        {
            _entryCodeTabId = tabId;
            if (IsEntryCodeShown)
            {
                return;
            }

            IsEntryCodeShown = true;
            EntryCode.BeginRequest();
            View.ShowEntryCode();
        }

        private void CloseEntryCode()
        {
            IsEntryCodeShown = false;
            _entryCodeTabId = null;
            View.HideEntryCode();
        }

        private void OnNavigatorChanged()
        {
            if (_navigator.CurrentTabId != null)
            {
                _state.Set(TabStateKey, _navigator.CurrentTabId);
            }
        }

        /// <summary>The tab selected last time (if it still exists), otherwise the default tab.</summary>
        private string InitialTabId()
        {
            var stored = _state.Get(TabStateKey);
            return stored != null && _tabs.Find(stored) != null ? stored : _settings.General.DefaultTab;
        }

        private void Raise(bool visible)
        {
            if (VisibilityChanged == null)
            {
                return;
            }

            foreach (var d in VisibilityChanged.GetInvocationList())
            {
                try
                {
                    ((Action<bool>)d)(visible);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}