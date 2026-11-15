using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Atsuvanced.Debugger.Tests
{
    public class OptionScannerTests
    {
        [Test]
        public void Scan_PicksSupportedPropertiesAndParameterlessVoidMethods()
        {
            var options = OptionScanner.Scan(new Sample());
            var names = options.Select(o => o.Name).OrderBy(n => n).ToArray();

            CollectionAssert.AreEqual(new[] { "God Mode", "Hp", "Kill", "ReadOnlyText" }, names);
        }

        [Test]
        public void Scan_ReadsAttributes()
        {
            var hp = OptionScanner.Scan(new Sample()).Single(o => o.Name == "Hp");

            Assert.AreEqual("Player", hp.Category);
            Assert.AreEqual(0, hp.Range.Min);
            Assert.AreEqual(100, hp.Range.Max);
            Assert.AreEqual(5, hp.Increment);
            Assert.AreEqual(typeof(int), hp.ValueType);
        }

        [Test]
        public void Scan_SupportsComponentModelAttributes()
        {
            var o = OptionScanner.Scan(new Sample()).Single(x => x.MemberName == "Invincible");

            Assert.AreEqual("God Mode", o.Name);
            Assert.AreEqual("Player", o.Category);
        }

        [Test]
        public void Property_GetSetRoundTrips()
        {
            var sample = new Sample();
            var hp = OptionScanner.Scan(sample).Single(o => o.Name == "Hp");
            var changed = 0;
            hp.ValueChanged += _ => changed++;

            hp.SetValue(42);

            Assert.AreEqual(42, sample.Hp);
            Assert.AreEqual(42, hp.GetValue());
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void ReadOnlyProperty_CannotWrite()
        {
            var ro = OptionScanner.Scan(new Sample()).Single(o => o.Name == "ReadOnlyText");

            Assert.IsTrue(ro.CanRead);
            Assert.IsFalse(ro.CanWrite);
            Assert.AreEqual(-1, ro.SortPriority);
        }

        [Test]
        public void Method_Invokes()
        {
            var sample = new Sample();
            OptionScanner.Scan(sample).Single(o => o.Name == "Kill").Invoke();

            Assert.AreEqual(1, sample.CallCount);
        }

        private class Sample
        {
            public int CallCount;

            [OptionCategory("Player")]
            [NumberRange(0, 100)]
            [Increment(5)]
            public int Hp { get; set; } = 10;

            [System.ComponentModel.Category("Player")]
            [DisplayName("God Mode")]
            public bool Invincible { get; set; }

            [OptionSort(-1)] public string ReadOnlyText => "ro";

            public GameObject Unsupported { get; set; }

            [HiddenOption] public int Hidden { get; set; }

            [Browsable(false)] public int NotBrowsable { get; set; }

            public void Kill()
            {
                CallCount++;
            }

            public int NonVoid()
            {
                return 0;
            }

            public void WithArg(int x)
            {
            }
        }
    }
}