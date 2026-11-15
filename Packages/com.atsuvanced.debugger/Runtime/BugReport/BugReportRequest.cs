using System;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     A bug report sheet opened with <see cref="BugReportService.ShowSheet" />, waiting to be filled in and sent.
    ///     A bug report page reads it from <see cref="BugReportService.Pending" /> (prefill the description and screenshot)
    ///     and calls <see cref="BugReportService.Complete" /> after sending. Closing the panel completes it with false.
    /// </summary>
    public sealed class BugReportRequest
    {
        internal BugReportRequest(string description, Action<bool> onComplete)
        {
            Description = description;
            OnComplete = onComplete;
        }

        /// <summary>Initial description passed by the caller, or null.</summary>
        public string Description { get; }

        /// <summary>PNG screenshot taken before the panel opened, or null (not requested or the capture failed).</summary>
        public byte[] Screenshot { get; internal set; }

        internal Action<bool> OnComplete { get; }
    }
}