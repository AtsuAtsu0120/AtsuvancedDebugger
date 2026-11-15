using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Atsuvanced.Debugger
{
    /// <summary>Builds and serializes bug reports (UI independent).</summary>
    public static class BugReportBuilder
    {
        /// <summary>
        ///     Collects a report from the debugger: non-private system information and the newest
        ///     <paramref name="maxLogEntries" /> console entries (0 = none).
        /// </summary>
        public static BugReport Create(IDebugService service, string description, string email = null,
            byte[] screenshot = null, int maxLogEntries = 500, bool includeSystemInfo = true)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var report = new BugReport
            {
                Description = description ?? string.Empty,
                UserEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Screenshot = screenshot,
                CreatedAt = DateTime.Now
            };

            if (includeSystemInfo)
            {
                var info = service.SystemInfo;
                foreach (var category in info.Categories)
                {
                    var values = new Dictionary<string, string>();
                    foreach (var entry in info.GetEntries(category))
                    {
                        if (entry.IsPrivate)
                        {
                            continue;
                        }

                        values[entry.Title] = entry.Value?.ToString() ?? string.Empty;
                    }

                    if (values.Count > 0)
                    {
                        report.SystemInformation[category] = values;
                    }
                }
            }

            if (maxLogEntries > 0)
            {
                service.Console.CopyLast(maxLogEntries, report.ConsoleLog);
            }

            return report;
        }

        /// <summary>Human readable text (description, system information, log).</summary>
        public static string ToText(BugReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Bug Report");
            sb.Append("Created: ")
                .AppendLine(report.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(report.UserEmail))
            {
                sb.Append("Email: ").AppendLine(report.UserEmail);
            }

            sb.AppendLine();
            sb.AppendLine("## Description");
            sb.AppendLine(string.IsNullOrEmpty(report.Description) ? "(none)" : report.Description);
            sb.AppendLine();

            foreach (var category in report.SystemInformation)
            {
                sb.Append("## ").AppendLine(category.Key);
                foreach (var pair in category.Value)
                {
                    sb.Append(pair.Key).Append(": ").AppendLine(pair.Value);
                }

                sb.AppendLine();
            }

            sb.Append("## Console (").Append(report.ConsoleLog.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(")");
            sb.Append(ConsoleExport.ToText(report.ConsoleLog));
            return sb.ToString();
        }

        /// <summary>JSON (UTF-8 text) without the screenshot, for HTTP endpoints and tools.</summary>
        public static string ToJson(BugReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var w = new StringBuilder();
            w.Append('{');
            Prop(w, "createdAt", report.CreatedAt.ToString("o", CultureInfo.InvariantCulture)).Append(',');
            Prop(w, "description", report.Description).Append(',');
            Prop(w, "email", report.UserEmail).Append(',');
            w.Append("\"hasScreenshot\":").Append(report.Screenshot != null ? "true" : "false").Append(',');

            w.Append("\"systemInformation\":{");
            var firstCategory = true;
            foreach (var category in report.SystemInformation)
            {
                if (!firstCategory)
                {
                    w.Append(',');
                }

                firstCategory = false;
                Str(w, category.Key).Append(":{");
                var first = true;
                foreach (var pair in category.Value)
                {
                    if (!first)
                    {
                        w.Append(',');
                    }

                    first = false;
                    Prop(w, pair.Key, pair.Value);
                }

                w.Append('}');
            }

            w.Append("},\"console\":[");
            for (var i = 0; i < report.ConsoleLog.Count; i++)
            {
                var e = report.ConsoleLog[i];
                if (i > 0)
                {
                    w.Append(',');
                }

                w.Append('{');
                Prop(w, "time", e.Timestamp.ToString("o", CultureInfo.InvariantCulture)).Append(',');
                Prop(w, "type", e.Type.ToString()).Append(',');
                Prop(w, "message", e.Message).Append(',');
                Prop(w, "stackTrace", e.StackTrace);
                w.Append('}');
            }

            w.Append("]}");
            return w.ToString();
        }

        private static StringBuilder Prop(StringBuilder w, string name, string value)
        {
            Str(w, name).Append(':');
            if (value == null)
            {
                return w.Append("null");
            }

            return Str(w, value);
        }

        /// <summary>Appends a JSON string literal.</summary>
        internal static StringBuilder Str(StringBuilder w, string s)
        {
            w.Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': w.Append("\\\""); break;
                    case '\\': w.Append("\\\\"); break;
                    case '\n': w.Append("\\n"); break;
                    case '\r': w.Append("\\r"); break;
                    case '\t': w.Append("\\t"); break;
                    case '\b': w.Append("\\b"); break;
                    case '\f': w.Append("\\f"); break;
                    default:
                        if (ch < 0x20)
                        {
                            w.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            w.Append(ch);
                        }

                        break;
                }
            }

            return w.Append('"');
        }
    }
}