using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>A bug report: description, screenshot, system information and console log (FR-BUG-01).</summary>
    public sealed class BugReport
    {
        /// <summary>Console entries, oldest first.</summary>
        public List<LogEntry> ConsoleLog = new();

        public DateTime CreatedAt = DateTime.Now;
        public string Description;

        /// <summary>PNG-encoded screenshot (taken without the debug panel), or null.</summary>
        public byte[] Screenshot;

        /// <summary>category -> (title -> value). Private entries (<see cref="InfoEntry.IsPrivate" />) are excluded.</summary>
        public Dictionary<string, Dictionary<string, string>> SystemInformation = new();

        public string UserEmail;
    }

    /// <summary>
    ///     Destination of bug reports (local file, HTTP endpoint, Slack, Jira, ...). Set with
    ///     <see cref="BugReportService.Reporter" /> (or <c>DebuggerBuilder.UseBugReporter</c>); the default is
    ///     <see cref="LocalBugReporter" />.
    /// </summary>
    public interface IBugReporter
    {
        /// <param name="report">Report to send.</param>
        /// <param name="onProgress">0..1 progress. May be ignored.</param>
        /// <param name="onComplete">
        ///     (success, message): the error message on failure, or an optional note on success (e.g. where it was saved).
        ///     Must be invoked exactly once, on the main thread.
        /// </param>
        void Submit(BugReport report, Action<float> onProgress, Action<bool, string> onComplete);
    }
}