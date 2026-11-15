using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Atsuvanced.Debugger.Tests
{
    public class ConsoleLogBufferTests
    {
        [Test]
        public void RingBuffer_KeepsNewestEntries()
        {
            var b = new ConsoleLogBuffer(3);
            for (var i = 0; i < 5; i++)
            {
                b.Add(LogType.Log, i.ToString(), "");
            }

            var list = new List<LogEntry>();
            b.CopyTo(list);

            CollectionAssert.AreEqual(new[] { "2", "3", "4" }, list.Select(e => e.Message).ToArray());
            Assert.AreEqual(3, b.GetCount(LogSeverity.Info));
        }

        [Test]
        public void SeverityCounts_TrackEviction()
        {
            var b = new ConsoleLogBuffer(2);
            b.Add(LogType.Error, "e", "");
            b.Add(LogType.Warning, "w", "");
            b.Add(LogType.Exception, "x", ""); // evicts "e"

            Assert.AreEqual(1, b.GetCount(LogSeverity.Error));
            Assert.AreEqual(1, b.GetCount(LogSeverity.Warning));
            Assert.AreEqual(0, b.GetCount(LogSeverity.Info));
        }

        [Test]
        public void Version_ChangesOnMutation()
        {
            var b = new ConsoleLogBuffer(4);
            var v0 = b.Version;
            b.Add(LogType.Log, "a", "");
            Assert.AreNotEqual(v0, b.Version);

            var v1 = b.Version;
            b.Clear();
            Assert.AreNotEqual(v1, b.Version);
            Assert.AreEqual(0, b.Count);
        }

        [Test]
        public void Resize_KeepsNewest()
        {
            var b = new ConsoleLogBuffer(5);
            for (var i = 0; i < 5; i++)
            {
                b.Add(LogType.Log, i.ToString(), "");
            }

            b.Resize(2);

            var list = new List<LogEntry>();
            b.CopyTo(list);
            CollectionAssert.AreEqual(new[] { "3", "4" }, list.Select(e => e.Message).ToArray());

            b.Add(LogType.Log, "5", "");
            b.CopyTo(list);
            CollectionAssert.AreEqual(new[] { "4", "5" }, list.Select(e => e.Message).ToArray());
        }
    }
}