using UnityEngine;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Keyboard / gamepad shortcuts (<see cref="KeyboardSettings" />): toggle the panel, focus the search, previous / next
    ///     tab (also the gamepad shoulders). Escape / gamepad B (back, then close) is part of the panel itself.
    /// </summary>
    public sealed class KeyboardShortcutsFeature : DebuggerFeature
    {
        protected override void OnInstall()
        {
        }

        protected override void OnUpdate(float unscaledDeltaTime)
        {
            var keys = Debugger.Settings.Keyboard;
            var panel = Debugger.Panel;
            if (keys.Enabled && ShortcutInput.WasPressed(keys.Toggle))
            {
                if (panel.IsVisible)
                {
                    panel.Hide();
                }
                else
                {
                    panel.Show();
                }
            }
            else if (keys.Enabled && ShortcutInput.WasPressed(keys.Search))
            {
                FocusSearch();
            }
            else if (panel.IsVisible && ((keys.Enabled && ShortcutInput.WasPressed(keys.PreviousTab)) ||
                                         ShortcutInput.WasShoulderPressed(-1)))
            {
                Debugger.Pages.SelectAdjacentTab(-1);
            }
            else if (panel.IsVisible && ((keys.Enabled && ShortcutInput.WasPressed(keys.NextTab)) ||
                                         ShortcutInput.WasShoulderPressed(1)))
            {
                Debugger.Pages.SelectAdjacentTab(1);
            }
        }

        /// <summary>Opens the Search tab (and the panel if needed) and focuses its search field.</summary>
        public void FocusSearch()
        {
            if (Debugger.Tabs.Find(DebugTabIds.Search) == null)
            {
                return;
            }

            Debugger.Panel.Show(DebugTabIds.Search);
            if (Debugger.Pages.ActivePage is OptionSearchPage search)
            {
                search.FocusSearch();
            }
        }
    }

    /// <summary>
    ///     Sets <c>Time.timeScale</c> to 0 while the panel is open when <see cref="GeneralSettings.PauseTimeWhenOpen" />
    ///     is on.
    /// </summary>
    public sealed class PauseTimeFeature : DebuggerFeature
    {
        private bool _paused;
        private float _savedTimeScale = 1f;

        protected override void OnInstall()
        {
            Debugger.Panel.VisibilityChanged += OnVisibilityChanged;
        }

        protected override void OnUninstall()
        {
            Debugger.Panel.VisibilityChanged -= OnVisibilityChanged;
            Resume();
        }

        private void OnVisibilityChanged(bool visible)
        {
            if (!visible)
            {
                Resume();
                return;
            }

            if (_paused || !Debugger.Settings.General.PauseTimeWhenOpen)
            {
                return;
            }

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _paused = true;
        }

        private void Resume()
        {
            if (!_paused)
            {
                return;
            }

            _paused = false;
            Time.timeScale = _savedTimeScale;
        }
    }
}