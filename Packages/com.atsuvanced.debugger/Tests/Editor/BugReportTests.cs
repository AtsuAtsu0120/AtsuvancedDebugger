using System;
using System.Collections.Generic;
using System.IO;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using UnityEngine;

namespace Atsuvanced.Debugger.Tests
{
    public class BugReportTests
    {
        private static DebugService CreateService()
        {
            var s = TestDebugger.Create(new MemoryDebuggerStorage());
            s.AddSystemInfo(InfoEntry.Create("Version", "1.2.3"), "App");
            s.AddSystemInfo(InfoEntry.Create("Device Id", "secret", true), "App");
            s.AddSystemInfo(InfoEntry.Create("Only Private", "x", true), "Hidden");
            for (var i = 0; i < 5; i++)
            {
                s.Console.Add(LogType.Log, "log " + i, "at Foo()");
            }

            return s;
        }

        [Test]
        public void Builder_ExcludesPrivateInfo_AndTakesNewestLogs()
        {
            var s = CreateService();
            var report = BugReportBuilder.Create(s, "It crashed", " me@example.com ", new byte[] { 1 }, 3);
            Assert.AreEqual("It crashed", report.Description);
            Assert.AreEqual("me@example.com", report.UserEmail);
            Assert.AreEqual("1.2.3", report.SystemInformation["App"]["Version"]);
            Assert.IsFalse(report.SystemInformation["App"].ContainsKey("Device Id"),
                "Private entries are excluded (FR-SYS-03).");
            Assert.IsFalse(report.SystemInformation.ContainsKey("Hidden"));
            Assert.AreEqual(3, report.ConsoleLog.Count);
            Assert.AreEqual("log 2", report.ConsoleLog[0].Message);
            Assert.AreEqual("log 4", report.ConsoleLog[2].Message);

            var none = BugReportBuilder.Create(s, "x", maxLogEntries: 0, includeSystemInfo: false);
            Assert.AreEqual(0, none.ConsoleLog.Count);
            Assert.AreEqual(0, none.SystemInformation.Count);
            Assert.IsNull(none.UserEmail);
            s.Dispose();
        }

        [Test]
        public void Json_EscapesText()
        {
            var report = new BugReport
                { Description = "a \"quote\"\nline\\ \u0001", CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5) };
            report.SystemInformation["Cat"] = new Dictionary<string, string> { ["K"] = "V" };
            report.ConsoleLog.Add(new LogEntry(LogType.Error, "boom", "at X", new DateTime(2026, 1, 2), 1));
            var json = BugReportBuilder.ToJson(report);
            StringAssert.Contains("\"description\":\"a \\\"quote\\\"\\nline\\\\ \\u0001\"", json);
            StringAssert.Contains("\"email\":null", json);
            StringAssert.Contains("\"Cat\":{\"K\":\"V\"}", json);
            StringAssert.Contains("\"type\":\"Error\"", json);
            Assert.DoesNotThrow(() => JsonUtility.FromJson<Probe>(json), "Valid JSON");

            var text = BugReportBuilder.ToText(report);
            StringAssert.Contains("## Cat", text);
            StringAssert.Contains("[Error] boom", text);
        }

        [Test]
        public void LocalReporter_WritesTextJsonAndScreenshot()
        {
            var dir = Path.Combine(Path.GetTempPath(), "AtsuDebuggerTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var reporter = new LocalBugReporter(dir);
                var report = new BugReport { Description = "hello", Screenshot = new byte[] { 137, 80, 78, 71 } };
                bool? ok = null;
                string message = null;
                reporter.Submit(report, null, (s, m) =>
                {
                    ok = s;
                    message = m;
                });
                Assert.IsTrue(ok);
                var folder = reporter.LastReportPath;
                StringAssert.Contains(folder, message);
                Assert.IsTrue(File.Exists(Path.Combine(folder, "report.txt")));
                Assert.IsTrue(File.Exists(Path.Combine(folder, "report.json")));
                Assert.AreEqual(4, File.ReadAllBytes(Path.Combine(folder, "screenshot.png")).Length);

                // Same second -> a second folder instead of overwriting.
                reporter.Submit(report, null, (s, m) => { });
                Assert.AreNotEqual(folder, reporter.LastReportPath);
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [Test]
        public void BugReportTab_SendsThroughTheReporter_AndCompletesTheSheet()
        {
            var s = CreateService();
            Assert.IsInstanceOf<LocalBugReporter>(s.BugReports.Reporter, "Default reporter.");
            var reporter = new FakeReporter();
            s.BugReports.Reporter = reporter;
            Assert.AreSame(reporter, s.BugReports.Reporter);

            bool? completed = null;
            s.ShowBugReportSheet(r => completed = r, false, "prefilled");
            Assert.IsTrue(s.Panel.IsVisible);
            Assert.AreEqual(DebugTabIds.BugReporter, s.Navigator.CurrentTabId);
            var page = (BugReportPage)((SectionedPage)s.Pages.CurrentPage).CurrentSectionPage;
            Assert.AreEqual("prefilled", page.Description);

            page.Description = "";
            page.SendReport();
            Assert.IsNull(reporter.Last, "An empty description is not sent.");

            page.Description = "Broken door";
            // No screenshot in edit mode: the capture returns null and the report is sent without one.
            page.SendReport();
            Assert.IsNotNull(reporter.Last);
            Assert.AreEqual("Broken door", reporter.Last.Description);
            Assert.IsNull(reporter.Last.Screenshot);
            Assert.AreEqual(5, reporter.Last.ConsoleLog.Count);
            Assert.IsTrue(completed);
            Assert.AreEqual("", page.Description, "Cleared after sending.");
            s.Dispose();
        }

        [Test]
        public void Sheet_ClosedWithoutSending_CompletesWithFalse()
        {
            var s = CreateService();
            bool? completed = null;
            s.ShowBugReportSheet(r => completed = r);
            Assert.IsTrue(s.Panel.IsVisible, "Edit mode capture calls back immediately (null) and the tab opens.");
            Assert.IsNull(completed);
            s.HideDebugPanel();
            Assert.IsFalse(completed);
            s.Dispose();
        }

        private sealed class FakeReporter : IBugReporter
        {
            public readonly bool Succeed = true;
            public BugReport Last;

            public void Submit(BugReport report, Action<float> onProgress, Action<bool, string> onComplete)
            {
                Last = report;
                onProgress?.Invoke(0.5f);
                onComplete(Succeed, Succeed ? "ok" : "offline");
            }
        }

        [Serializable]
        private class Probe
        {
            public string description;
        }
    }

    public class ConsoleExportTests
    {
        [Test]
        public void ToText_And_SaveToFile()
        {
            var buffer = new ConsoleLogBuffer(10);
            buffer.Add(LogType.Warning, "careful", "at A()\n");
            buffer.Add(LogType.Log, "plain", "");
            var dir = Path.Combine(Path.GetTempPath(), "AtsuDebuggerTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var path = ConsoleExport.SaveToFile(buffer, dir);
                var text = File.ReadAllText(path);
                StringAssert.Contains("[Warning] careful", text);
                StringAssert.Contains("at A()", text);
                StringAssert.Contains("[Log] plain", text);
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
    }
}