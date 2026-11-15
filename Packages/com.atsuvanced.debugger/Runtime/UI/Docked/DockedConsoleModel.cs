using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger.UI
{
    /// <summary>Latest log lines and counts for the docked console (UI independent).</summary>
    internal sealed class DockedConsoleModel
    {
        private readonly ConsoleLogBuffer _buffer;
        private int _seenLines = -1;
        private int _seenVersion = -1;

        public DockedConsoleModel(ConsoleLogBuffer buffer)
        {
            _buffer = buffer;
        }

        /// <summary>Newest entries, oldest first.</summary>
        public List<LogEntry> Lines { get; } = new();

        public int InfoCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }

        /// <summary>The newest entry, or null if the buffer is empty.</summary>
        public LogEntry? Latest => Lines.Count > 0 ? Lines[Lines.Count - 1] : null;

        /// <summary>Takes a new snapshot if the buffer changed. Returns true when something changed.</summary>
        public bool Update(int lineCount)
        {
            var version = _buffer.Version;
            if (version == _seenVersion && lineCount == _seenLines)
            {
                return false;
            }

            _seenVersion = version;
            _seenLines = lineCount;

            _buffer.CopyLast(Math.Max(1, lineCount), Lines);
            InfoCount = _buffer.GetCount(LogSeverity.Info);
            WarningCount = _buffer.GetCount(LogSeverity.Warning);
            ErrorCount = _buffer.GetCount(LogSeverity.Error);
            return true;
        }
    }
}