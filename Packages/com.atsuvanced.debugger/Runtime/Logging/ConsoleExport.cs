using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>Writes console logs as text (FR-CON-06).</summary>
    public static class ConsoleExport
    {
        /// <summary>One block per entry: "[time] [type] message" followed by the stack trace.</summary>
        public static string ToText(IReadOnlyList<LogEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                sb.Append('[').Append(e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                    .Append("] [")
                    .Append(e.Type.ToString()).Append("] ").AppendLine(e.Message);
                if (!string.IsNullOrEmpty(e.StackTrace))
                {
                    sb.AppendLine(e.StackTrace.TrimEnd());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Saves every entry of <paramref name="buffer" /> to <c>console-yyyyMMdd-HHmmss.txt</c> and returns the path.
        /// </summary>
        /// <param name="directory">
        ///     Absolute folder, or relative to <c>Application.persistentDataPath</c>. Null =
        ///     "AtsuvancedDebugger/Logs".
        /// </param>
        public static string SaveToFile(ConsoleLogBuffer buffer, string directory = null)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (string.IsNullOrEmpty(directory))
            {
                directory = "AtsuvancedDebugger/Logs";
            }

            if (!Path.IsPathRooted(directory))
            {
                directory = Path.Combine(Application.persistentDataPath, directory);
            }

            Directory.CreateDirectory(directory);

            var entries = new List<LogEntry>();
            buffer.CopyTo(entries);
            var path = Path.Combine(directory,
                "console-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
            File.WriteAllText(path, ToText(entries), new UTF8Encoding(false));
            return path;
        }
    }
}