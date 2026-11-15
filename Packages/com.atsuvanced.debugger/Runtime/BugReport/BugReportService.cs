using System;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Bug reports of a debugger (<see cref="IDebugService.BugReports" />): where they are sent and the sheet opened from
    ///     code. A bug report page reads <see cref="Pending" /> and calls <see cref="Complete" /> after sending.
    /// </summary>
    public sealed class BugReportService
    {
        private readonly IDebugPanel _panel;
        private readonly BugReportSettings _settings;
        private readonly DebugTabRegistry _tabs;
        private IBugReporter _default;
        private IBugReporter _reporter;

        internal BugReportService(BugReportSettings settings, DebugPanel panel, DebugTabRegistry tabs)
        {
            _settings = settings;
            _panel = panel;
            _tabs = tabs;
            panel.VisibilityChanged += visible =>
            {
                if (!visible)
                {
                    Complete(false);
                }
            };
            panel.EntryCodeCancelled += () => Complete(false);
        }

        /// <summary>Destination of bug reports. Setting null restores the default <see cref="LocalBugReporter" />.</summary>
        public IBugReporter Reporter
        {
            get => _reporter ?? (_default ??= new LocalBugReporter(_settings.Folder));
            set => _reporter = value;
        }

        /// <summary>Maximum number of console entries attached to a report (<see cref="BugReportSettings.MaxLogEntries" />).</summary>
        public int MaxLogEntries => _settings.MaxLogEntries;

        /// <summary>The sheet opened with <see cref="ShowSheet" /> that has not completed yet, or null.</summary>
        public BugReportRequest Pending { get; private set; }

        /// <summary>
        ///     Opens the Bug Report tab. With <paramref name="takeScreenshot" /> the game screen is captured first (before the
        ///     panel opens). <paramref name="onComplete" /> receives true when the report was sent, false when the panel was
        ///     closed without sending.
        /// </summary>
        public void ShowSheet(Action<bool> onComplete = null, bool takeScreenshot = true, string description = null)
        {
            Complete(false); // an older sheet that never completed
            var request = new BugReportRequest(description, onComplete);
            Pending = request;

            if (_tabs.Find(DebugTabIds.BugReporter) == null)
            {
                Debug.LogWarning("[AtsuvancedDebugger] The Bug Report tab is not installed (BugReportFeature).");
                Complete(false);
                return;
            }

            if (!takeScreenshot || _panel.IsVisible)
            {
                _panel.Show(DebugTabIds.BugReporter);
                return;
            }

            _panel.CaptureScreenshot(png =>
            {
                if (Pending != request)
                {
                    return;
                }

                request.Screenshot = png;
                _panel.Show(DebugTabIds.BugReporter);
            });
        }

        /// <summary>
        ///     Completes <see cref="Pending" /> (its callback receives <paramref name="sent" />). Does nothing when no sheet is
        ///     pending.
        /// </summary>
        public void Complete(bool sent)
        {
            var request = Pending;
            if (request == null)
            {
                return;
            }

            Pending = null;
            try
            {
                request.OnComplete?.Invoke(sent);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}