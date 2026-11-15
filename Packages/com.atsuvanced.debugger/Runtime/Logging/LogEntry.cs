using System;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    public readonly struct LogEntry
    {
        public readonly LogType Type;
        public readonly string Message;
        public readonly string StackTrace;
        public readonly DateTime Timestamp;

        /// <summary>Monotonic sequence number assigned by the buffer.</summary>
        public readonly long Sequence;

        public LogEntry(LogType type, string message, string stackTrace, DateTime timestamp, long sequence)
        {
            Type = type;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Timestamp = timestamp;
            Sequence = sequence;
        }

        /// <summary>Log / Warning / Error (Error, Assert and Exception are folded into Error).</summary>
        public LogSeverity Severity => ToSeverity(Type);

        public static LogSeverity ToSeverity(LogType type)
        {
            switch (type)
            {
                case LogType.Log: return LogSeverity.Info;
                case LogType.Warning: return LogSeverity.Warning;
                default: return LogSeverity.Error;
            }
        }
    }

    public enum LogSeverity
    {
        Info,
        Warning,
        Error
    }
}