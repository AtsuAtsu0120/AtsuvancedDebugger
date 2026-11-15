using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>
    ///     Bug Report tab (FR-BUG-01): description, optional email, a screenshot taken without the debug panel,
    ///     the console log and the (non-private) system information, sent through <see cref="BugReportService.Reporter" />.
    /// </summary>
    /// <remarks>
    ///     Public and inheritable: register a subclass as the <c>"report"</c> section of the <c>DebugTabIds.BugReporter</c>
    ///     tab.
    ///     Override <see cref="CreateReport" /> to add data to the report. When overriding a <c>protected</c> member, call the
    ///     base implementation.
    /// </remarks>
    public class BugReportPage : DebugPage
    {
        private TextField _description, _email;
        private Toggle _includeScreenshot, _includeLogs, _includeSystemInfo;
        private Label _noScreenshot, _status;
        private BugReportRequest _received;
        private int _shownLogCount = -1;
        private Button _send, _capture;
        private Texture2D _texture;
        private Image _thumbnail;

        public BugReportPage() : base(DebuggerStrings.Get("bug.title"))
        {
        }

        /// <summary>The PNG screenshot that will be attached, or null.</summary>
        protected byte[] Screenshot { get; private set; }

        /// <summary>True while a screenshot is being taken or the report is being sent.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>The description field's text.</summary>
        public string Description
        {
            get => _description?.value ?? string.Empty;
            set
            {
                if (_description != null)
                {
                    _description.value = value ?? string.Empty;
                }
            }
        }

        protected override float UpdateInterval => 1f;

        protected override VisualElement CreateContent()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("atsu-bug");

            _description = new TextField(DebuggerStrings.Get("bug.description")) { multiline = true };
            _description.textEdition.placeholder = DebuggerStrings.Get("bug.descriptionPlaceholder");
            _description.AddToClassList("atsu-bug__description");
            scroll.Add(_description);

            _email = new TextField(DebuggerStrings.Get("bug.email"));
            _email.AddToClassList("atsu-bug__email");
            scroll.Add(_email);

            _includeScreenshot = new Toggle(DebuggerStrings.Get("bug.includeScreenshot")) { value = true };
            _includeScreenshot.AddToClassList("atsu-bug__toggle");
            scroll.Add(_includeScreenshot);

            var shot = new VisualElement();
            shot.AddToClassList("atsu-bug__screenshot");
            _thumbnail = new Image { scaleMode = ScaleMode.ScaleToFit };
            _thumbnail.AddToClassList("atsu-bug__thumbnail");
            _noScreenshot = new Label(DebuggerStrings.Get("bug.noScreenshot"));
            _noScreenshot.AddToClassList("atsu-bug__no-screenshot");
            _capture = new Button(() => Capture(null));
            _capture.AddToClassList("atsu-button");
            _capture.AddToClassList("atsu-button--small");
            shot.Add(_thumbnail);
            shot.Add(_noScreenshot);
            shot.Add(_capture);
            scroll.Add(shot);

            _includeLogs = new Toggle { value = true };
            _includeLogs.AddToClassList("atsu-bug__toggle");
            scroll.Add(_includeLogs);
            _includeSystemInfo = new Toggle(DebuggerStrings.Get("bug.includeSystemInfo")) { value = true };
            _includeSystemInfo.AddToClassList("atsu-bug__toggle");
            scroll.Add(_includeSystemInfo);

            _status = new Label();
            _status.AddToClassList("atsu-bug__status");
            _status.selection.isSelectable = true;
            scroll.Add(_status);

            UpdateScreenshotView();
            return scroll;
        }

        protected override VisualElement CreateToolbar()
        {
            _send = new Button(SendReport) { text = DebuggerStrings.Get("bug.send") };
            _send.AddToClassList("atsu-button");
            _send.AddToClassList("atsu-button--primary");
            return _send;
        }

        protected override void OnShow()
        {
            Refresh();
        }

        protected override void OnUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            // A sheet opened with ShowBugReportSheet (also while this page is already shown).
            var request = Debugger.BugReports.Pending;
            if (request != null && request != _received)
            {
                _received = request;
                if (request.Description != null)
                {
                    _description.value = request.Description;
                }

                if (request.Screenshot != null)
                {
                    SetScreenshot(request.Screenshot);
                }

                SetStatus(string.Empty, false);
            }

            var logs = Math.Min(Debugger.Console.Count, Debugger.BugReports.MaxLogEntries);
            if (logs != _shownLogCount)
            {
                _shownLogCount = logs;
                _includeLogs.label = DebuggerStrings.Format("bug.includeLogs", logs);
            }
        }

        /// <summary>Builds the report to send. Override to add data (call the base implementation first).</summary>
        protected virtual BugReport CreateReport()
        {
            return BugReportBuilder.Create(Debugger, _description.value, _email.value,
                _includeScreenshot.value ? Screenshot : null,
                _includeLogs.value ? Debugger.BugReports.MaxLogEntries : 0,
                _includeSystemInfo.value);
        }

        /// <summary>Takes a new screenshot (the panel is hidden for a frame).</summary>
        protected void Capture(Action then)
        {
            if (IsBusy)
            {
                return;
            }

            SetBusy(true);
            Debugger.Panel.CaptureScreenshot(png =>
            {
                SetBusy(false);
                if (IsDisposed)
                {
                    return;
                }

                SetScreenshot(png);
                then?.Invoke();
            });
        }

        /// <summary>Same as pressing "Send": validates, takes a screenshot if needed, then submits.</summary>
        internal void SendReport()
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_description.value))
            {
                SetStatus(DebuggerStrings.Get("bug.emptyDescription"), true);
                _description.Focus();
                return;
            }

            if (_includeScreenshot.value && Screenshot == null)
            {
                Capture(Submit);
            }
            else
            {
                Submit();
            }
        }

        private void Submit()
        {
            BugReport report;
            try
            {
                report = CreateReport();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus(DebuggerStrings.Format("bug.failed", e.Message), true);
                return;
            }

            SetBusy(true);
            SetStatus(DebuggerStrings.Format("bug.sending", 0), false);
            var completed = false;
            try
            {
                Debugger.BugReports.Reporter.Submit(report,
                    progress =>
                    {
                        if (!completed)
                        {
                            SetStatus(
                                DebuggerStrings.Format("bug.sending", Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)),
                                false);
                        }
                    },
                    (success, message) =>
                    {
                        if (completed)
                        {
                            return;
                        }

                        completed = true;
                        OnSubmitted(success, message);
                    });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (!completed)
                {
                    completed = true;
                    OnSubmitted(false, e.Message);
                }
            }
        }

        private void OnSubmitted(bool success, string message)
        {
            SetBusy(false);
            if (success)
            {
                SetStatus(DebuggerStrings.Format("bug.sent", message ?? string.Empty).Trim(), false);
                _description.value = string.Empty;
                SetScreenshot(null);
            }
            else
            {
                SetStatus(DebuggerStrings.Format("bug.failed", message ?? "?"), true);
            }

            Debugger.BugReports.Complete(success);
        }

        private void SetScreenshot(byte[] png)
        {
            Screenshot = png;
            if (_texture)
            {
                Object.Destroy(_texture);
            }

            _texture = null;
            if (png != null)
            {
                _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    { name = "AtsuDebugger BugReport Screenshot" };
                if (!_texture.LoadImage(png))
                {
                    Object.Destroy(_texture);
                    _texture = null;
                }
            }

            UpdateScreenshotView();
        }

        private void UpdateScreenshotView()
        {
            if (_thumbnail == null)
            {
                return;
            }

            _thumbnail.image = _texture;
            _thumbnail.EnableInClassList(UssClasses.Hidden, _texture == null);
            _noScreenshot.EnableInClassList(UssClasses.Hidden, _texture != null);
            _capture.text = DebuggerStrings.Get(_texture != null ? "bug.retake" : "bug.capture");
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            _send?.SetEnabled(!busy);
        }

        private void SetStatus(string text, bool error)
        {
            if (_status == null)
            {
                return;
            }

            _status.text = text;
            _status.EnableInClassList("atsu-bug__status--error", error);
        }

        protected override void OnDispose()
        {
            if (_texture)
            {
                Object.Destroy(_texture);
            }

            _texture = null;
        }
    }
}