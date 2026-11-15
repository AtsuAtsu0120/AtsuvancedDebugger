using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Atsuvanced.Debugger.Tests
{
    public class ConsoleFilterTests
    {
        private static List<LogEntry> Logs(params (LogType type, string msg)[] items)
        {
            var list = new List<LogEntry>();
            long seq = 0;
            foreach (var (type, msg) in items)
            {
                list.Add(new LogEntry(type, msg, "stack", DateTime.Now, ++seq));
            }

            return list;
        }

        private static string[] Messages(List<ConsoleFilter.Item> items)
        {
            return items.Select(i => i.Entry.Message).ToArray();
        }

        [Test]
        public void SeverityToggles()
        {
            var src = Logs((LogType.Log, "a"), (LogType.Warning, "b"), (LogType.Error, "c"), (LogType.Exception, "d"));
            var f = new ConsoleFilter { ShowWarning = false };
            var dst = new List<ConsoleFilter.Item>();

            f.Apply(src, dst);
            CollectionAssert.AreEqual(new[] { "a", "c", "d" }, Messages(dst));

            f.ShowError = false;
            f.Apply(src, dst);
            CollectionAssert.AreEqual(new[] { "a" }, Messages(dst));
        }

        [Test]
        public void TextIsCaseInsensitiveSubstring()
        {
            var src = Logs((LogType.Log, "Player spawned"), (LogType.Log, "Enemy spawned"), (LogType.Log, "Loaded"));
            var f = new ConsoleFilter { Text = "SPAWN" };
            var dst = new List<ConsoleFilter.Item>();
            f.Apply(src, dst);
            CollectionAssert.AreEqual(new[] { "Player spawned", "Enemy spawned" }, Messages(dst));
        }

        [Test]
        public void Regex_AndInvalidPatternIsIgnored()
        {
            var src = Logs((LogType.Log, "hp=10"), (LogType.Log, "hp=abc"), (LogType.Log, "mp=3"));
            var f = new ConsoleFilter { UseRegex = true, Text = @"^hp=\d+$" };
            var dst = new List<ConsoleFilter.Item>();
            f.Apply(src, dst);
            CollectionAssert.AreEqual(new[] { "hp=10" }, Messages(dst));
            Assert.IsTrue(f.IsRegexValid);

            f.Text = "([";
            Assert.IsFalse(f.IsRegexValid);
            f.Apply(src, dst);
            Assert.AreEqual(3, dst.Count, "Invalid pattern must not hide everything.");
        }

        [Test]
        public void Collapse_KeepsFirstPositionAndCountsLatest()
        {
            var src = Logs((LogType.Log, "x"), (LogType.Log, "y"), (LogType.Log, "x"), (LogType.Warning, "x"));
            var f = new ConsoleFilter { Collapse = true };
            var dst = new List<ConsoleFilter.Item>();
            f.Apply(src, dst);

            Assert.AreEqual(3, dst.Count);
            Assert.AreEqual("x", dst[0].Entry.Message);
            Assert.AreEqual(2, dst[0].Count);
            Assert.AreEqual(3, dst[0].Entry.Sequence, "Shows the latest occurrence.");
            Assert.AreEqual(LogType.Warning, dst[2].Entry.Type, "Different type is not merged.");
        }
    }
}