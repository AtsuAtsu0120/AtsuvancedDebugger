using System;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Rendering side of the debugger. State (visibility, tabs, page stacks, overlays) lives in the core; the view only
    ///     reflects it. The null implementation is used in edit mode and when the debugger is disabled.
    /// </summary>
    internal interface IDebugPanelView
    {
        void Show();
        void Hide();
        void Destroy();
        bool IsScreenPointOverPanel(Vector2 screenPosition);

        /// <summary>Shows the PIN pad (the panel itself stays closed until the code is accepted).</summary>
        void ShowEntryCode();

        void HideEntryCode();

        /// <summary>Captures the screen with the debugger UI hidden. Must call back exactly once (null on failure).</summary>
        void CaptureScreenshot(Action<byte[]> onCaptured);

        /// <summary>Per-frame update (layout, built-in navigation keys), after the overlays were updated.</summary>
        void Tick(float unscaledDeltaTime);
    }

    internal sealed class NullPanelView : IDebugPanelView
    {
        public static readonly NullPanelView Instance = new();

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Destroy()
        {
        }

        public bool IsScreenPointOverPanel(Vector2 screenPosition)
        {
            return false;
        }

        public void ShowEntryCode()
        {
        }

        public void HideEntryCode()
        {
        }

        public void CaptureScreenshot(Action<byte[]> onCaptured)
        {
            onCaptured?.Invoke(null);
        }

        public void Tick(float unscaledDeltaTime)
        {
        }
    }
}