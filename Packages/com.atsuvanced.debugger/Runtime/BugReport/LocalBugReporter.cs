using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Default bug reporter (FR-BUG-03): saves each report into its own folder under
    ///     <c>Application.persistentDataPath</c> (report.txt, report.json and screenshot.png).
    /// </summary>
    public sealed class LocalBugReporter : IBugReporter
    {
        /// <param name="directory">
        ///     Absolute folder, or a folder relative to <c>Application.persistentDataPath</c>.
        ///     Null = <see cref="BugReportSettings.Folder" />'s default.
        /// </param>
        public LocalBugReporter(string directory = null)
        {
            if (string.IsNullOrEmpty(directory))
            {
                directory = "AtsuvancedDebugger/BugReports";
            }

            Directory = Path.IsPathRooted(directory)
                ? directory
                : Path.Combine(Application.persistentDataPath, directory);
        }

        /// <summary>Folder the reports are written to.</summary>
        public string Directory { get; }

        /// <summary>Folder of the last saved report, or null.</summary>
        public string LastReportPath { get; private set; }

        public void Submit(BugReport report, Action<float> onProgress, Action<bool, string> onComplete)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            try
            {
                var folder = Save(report);
                onProgress?.Invoke(1f);
                onComplete?.Invoke(true, DebuggerStrings.Format("bug.savedTo", folder));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onComplete?.Invoke(false, e.Message);
            }
        }

        /// <summary>Writes the report synchronously and returns its folder.</summary>
        public string Save(BugReport report)
        {
            var name = report.CreatedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var folder = Path.Combine(Directory, name);
            for (var i = 2; System.IO.Directory.Exists(folder); i++)
            {
                folder = Path.Combine(Directory, name + "-" + i.ToString(CultureInfo.InvariantCulture));
            }

            System.IO.Directory.CreateDirectory(folder);

            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(folder, "report.txt"), BugReportBuilder.ToText(report), utf8);
            File.WriteAllText(Path.Combine(folder, "report.json"), BugReportBuilder.ToJson(report), utf8);
            if (report.Screenshot != null)
            {
                File.WriteAllBytes(Path.Combine(folder, "screenshot.png"), report.Screenshot);
            }

            LastReportPath = folder;
            return folder;
        }
    }
}