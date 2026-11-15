using System;
using System.Collections.Generic;
using SRDebugger.Services;
using UnityEngine;
using Atsu = Atsuvanced.Debugger;

namespace SRDebugger
{
    /// <summary>SRDebugger compatible 4-digit entry code.</summary>
    public readonly struct EntryCode
    {
        public readonly int Digit1, Digit2, Digit3, Digit4;

        public EntryCode(int digit1, int digit2, int digit3, int digit4)
        {
            Digit1 = Check(digit1);
            Digit2 = Check(digit2);
            Digit3 = Check(digit3);
            Digit4 = Check(digit4);
        }

        private static int Check(int d)
        {
            return d >= 0 && d <= 9 ? d : throw new ArgumentOutOfRangeException(nameof(d), "Each digit must be 0-9.");
        }

        public override string ToString()
        {
            return $"{Digit1}{Digit2}{Digit3}{Digit4}";
        }
    }

    /// <summary>SRDebugger compatible bug report (converted from the core <see cref="Atsu.BugReport" />).</summary>
    public class BugReport
    {
        public List<ConsoleEntry> ConsoleLog;
        public string Email;
        public byte[] ScreenshotData;
        public Dictionary<string, Dictionary<string, object>> SystemInformation;
        public string UserDescription;

        internal static BugReport From(Atsu.BugReport core)
        {
            var report = new BugReport
            {
                ConsoleLog = new List<ConsoleEntry>(core.ConsoleLog.Count),
                Email = core.UserEmail,
                ScreenshotData = core.Screenshot,
                UserDescription = core.Description,
                SystemInformation = new Dictionary<string, Dictionary<string, object>>()
            };
            foreach (var e in core.ConsoleLog)
            {
                report.ConsoleLog.Add(new ConsoleEntry(e));
            }

            foreach (var category in core.SystemInformation)
            {
                var values = new Dictionary<string, object>();
                foreach (var pair in category.Value)
                {
                    values[pair.Key] = pair.Value;
                }

                report.SystemInformation[category.Key] = values;
            }

            return report;
        }
    }

    /// <summary>SRDebugger compatible submit result.</summary>
    public sealed class BugReportSubmitResult
    {
        private BugReportSubmitResult(bool success, string error)
        {
            IsSuccessful = success;
            ErrorMessage = error;
        }

        public static BugReportSubmitResult Success { get; } = new(true, null);

        public bool IsSuccessful { get; }
        public string ErrorMessage { get; }

        public static BugReportSubmitResult Error(string error)
        {
            return new BugReportSubmitResult(false, error);
        }
    }

    /// <summary>SRDebugger compatible bug report destination. Set with <c>SRDebug.Instance.SetBugReporterHandler</c>.</summary>
    public interface IBugReporterHandler
    {
        /// <summary>False disables sending (the report fails with a message).</summary>
        bool IsUsable { get; }

        void Submit(BugReport report, Action<BugReportSubmitResult> onComplete, IProgress<float> onProgress);
    }

    /// <summary>Runs an <see cref="IBugReporterHandler" /> as the core <see cref="Atsu.IBugReporter" />.</summary>
    internal sealed class BugReporterHandlerAdapter : Atsu.IBugReporter
    {
        private readonly IBugReporterHandler _handler;

        public BugReporterHandlerAdapter(IBugReporterHandler handler)
        {
            _handler = handler;
        }

        public void Submit(Atsu.BugReport report, Action<float> onProgress, Action<bool, string> onComplete)
        {
            if (!_handler.IsUsable)
            {
                onComplete?.Invoke(false, "The bug reporter handler is not usable.");
                return;
            }

            _handler.Submit(BugReport.From(report),
                result => onComplete?.Invoke(result != null && result.IsSuccessful, result?.ErrorMessage),
                new Progress(onProgress));
        }

        private sealed class Progress : IProgress<float>
        {
            private readonly Action<float> _report;

            public Progress(Action<float> report)
            {
                _report = report;
            }

            public void Report(float value)
            {
                _report?.Invoke(value);
            }
        }
    }
}

namespace SRDebugger.Services
{
    /// <summary>SRDebugger compatible console entry (used by <see cref="SRDebugger.BugReport" />).</summary>
    public class ConsoleEntry
    {
        public int Count = 1;
        public LogType LogType;
        public string Message;
        public string StackTrace;

        public ConsoleEntry()
        {
        }

        internal ConsoleEntry(Atsu.LogEntry e)
        {
            Message = e.Message;
            StackTrace = e.StackTrace;
            LogType = e.Type;
        }
    }
}