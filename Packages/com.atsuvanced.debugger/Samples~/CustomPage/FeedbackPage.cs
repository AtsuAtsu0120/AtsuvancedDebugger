using System.Collections.Generic;
using Atsuvanced.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtsuvancedDebuggerSamples
{
    /// <summary>
    /// A fully custom page: any UI Toolkit elements, a "Send" button in the header,
    /// a confirmation before leaving with unsent input, and a pushed result page.
    /// </summary>
    public sealed class FeedbackPage : DebugPage
    {
        private TextField _summary;
        private TextField _detail;
        private DropdownField _severity;
        private Toggle _attachLog;

        public FeedbackPage() : base("Feedback")
        {
        }

        protected override VisualElement CreateContent()
        {
            var root = new ScrollView();
            root.style.paddingLeft = root.style.paddingRight = 16;
            root.style.paddingTop = 12;

            _summary = new TextField("Summary");
            _severity = new DropdownField("Severity", new List<string> { "Low", "Medium", "High", "Crash" }, 1);
            _detail = new TextField("Detail") { multiline = true };
            _detail.style.minHeight = 160;
            _detail.style.whiteSpace = WhiteSpace.Normal;
            _attachLog = new Toggle("Attach console log") { value = true };

            root.Add(_summary);
            root.Add(_severity);
            root.Add(_detail);
            root.Add(_attachLog);
            return root;
        }

        protected override VisualElement CreateToolbar()
        {
            var send = new Button(Send) { text = "Send" };
            send.AddToClassList("atsu-button");
            send.AddToClassList("atsu-button--primary");
            return send;
        }

        protected override bool OnBackRequested()
        {
            // Keep the page if something was typed. A real page would show a confirmation instead.
            if (string.IsNullOrEmpty(_summary?.value)) return true;
            Debug.Log("[Sample] Unsent feedback - clear the summary to leave.");
            return false;
        }

        private void Send()
        {
            var logs = _attachLog.value ? Debugger.Console.Count : 0;
            Debug.Log($"[Sample] Feedback sent: [{_severity.value}] {_summary.value} (logs: {logs})");
            _summary.value = string.Empty;
            _detail.value = string.Empty;
            Navigator.Push(DebugPage.FromElement("Sent", () => new Label("Thank you! Press back to return.")));
        }
    }
}
