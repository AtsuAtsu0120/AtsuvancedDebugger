using System.ComponentModel;
using System.Linq;
using NUnit.Framework;

namespace Atsuvanced.Debugger.Tests
{
    public class OptionRegistryTests
    {
        [Test]
        public void AddRemoveContainer()
        {
            var r = new OptionRegistry();
            var c = new Notifying();

            r.AddContainer(c);
            r.AddContainer(c); // duplicate ignored
            Assert.AreEqual(2, r.Options.Count);

            Assert.IsTrue(r.RemoveContainer(c));
            Assert.AreEqual(0, r.Options.Count);
            Assert.IsFalse(r.RemoveContainer(c));
        }

        [Test]
        public void INotifyPropertyChanged_RaisesValueChanged()
        {
            var r = new OptionRegistry();
            var c = new Notifying();
            r.AddContainer(c);
            var option = r.Options.Single(o => o.Name == "Value");
            var count = 0;
            option.ValueChanged += _ => count++;

            c.Value = 3;
            Assert.AreEqual(1, count);

            r.RemoveContainer(c);
            c.Value = 4;
            Assert.AreEqual(1, count, "Handler must be detached after removal.");
        }

        [Test]
        public void DynamicContainer_PropagatesAddRemove()
        {
            var r = new OptionRegistry();
            var d = new DynamicOptionContainer();
            r.AddContainer(d);

            var opt = OptionDefinition.FromMethod("Dyn", () => { });
            d.AddOption(opt);
            Assert.Contains(opt, r.Options.ToList());

            d.RemoveOption(opt);
            Assert.IsFalse(r.Options.Contains(opt));
        }

        [Test]
        public void Pinning()
        {
            var r = new OptionRegistry();
            r.AddContainer(new Notifying());
            var events = 0;
            r.PinStateChanged += (_, __) => events++;

            r.PinCategory("A");
            Assert.AreEqual(1, r.PinnedOptions.Count);

            r.PinByName("Act");
            Assert.AreEqual(2, r.PinnedOptions.Count);

            r.UnpinByName("Value");
            Assert.AreEqual(1, r.PinnedOptions.Count);

            r.ClearPinned();
            Assert.AreEqual(0, r.PinnedOptions.Count);
            Assert.AreEqual(4, events);
        }

        [Test]
        public void RemovingOption_UnpinsIt()
        {
            var r = new OptionRegistry();
            var opt = OptionDefinition.Create("X", () => 1);
            r.AddOption(opt);
            r.Pin(opt);

            r.RemoveOption(opt);

            Assert.AreEqual(0, r.PinnedOptions.Count);
        }

        private class Notifying : INotifyPropertyChanged
        {
            private int _value;

            [OptionCategory("A")]
            public int Value
            {
                get => _value;
                set
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            [OptionCategory("B")]
            public void Act()
            {
            }
        }
    }
}