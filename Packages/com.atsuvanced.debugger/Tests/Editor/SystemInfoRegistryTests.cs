using System;
using NUnit.Framework;

namespace Atsuvanced.Debugger.Tests
{
    public class SystemInfoRegistryTests
    {
        [Test]
        public void Add_PreservesCategoryOrderAndReplacesSameTitle()
        {
            var r = new SystemInfoRegistry();
            r.Add(InfoEntry.Create("a", 1), "X");
            r.Add(InfoEntry.Create("b", 2), "Y");
            r.Add(InfoEntry.Create("a", 3), "X");

            CollectionAssert.AreEqual(new[] { "X", "Y" }, r.Categories);
            Assert.AreEqual(1, r.GetEntries("X").Count);
            Assert.AreEqual(3, r.GetEntries("X")[0].Value);
        }

        [Test]
        public void Value_IsLazyAndSwallowsExceptions()
        {
            var n = 0;
            var lazy = InfoEntry.Create("n", () => ++n);
            Assert.AreEqual(0, n);
            Assert.AreEqual(1, lazy.Value);

            var broken = InfoEntry.Create("x", () => throw new InvalidOperationException());
            StringAssert.StartsWith("Error", (string)broken.Value);
        }
    }
}