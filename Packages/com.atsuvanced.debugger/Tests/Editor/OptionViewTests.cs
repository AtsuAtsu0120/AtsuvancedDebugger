using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    public class OptionViewTests
    {
        private OptionRegistry _registry;
        private OptionViewRegistry _views;

        [SetUp]
        public void SetUp()
        {
            _registry = new OptionRegistry();
            _views = new OptionViewRegistry(_registry);
        }

        [Test]
        public void Scanner_SkipsUnsupportedTypesAndValueReturningMethods()
        {
            var options = OptionScanner.Scan(new Container(), _registry.IsValueTypeSupported);

            Assert.IsFalse(options.Any(o => o.Name == "Item"), "Unregistered struct is not displayable.");
            Assert.IsFalse(options.Any(o => o.Name == "Unsupported"), "Unsupported types need a type view.");
            Assert.IsFalse(options.Any(o => o.Name == "Map"), "Methods returning a value are not options.");
        }

        [Test]
        public void TypeRegistration_MakesTypeScannableAndResolvesView()
        {
            _views.Register<ItemId>(() => new ItemIdView());
            var item = OptionScanner.Scan(new Container(), _registry.IsValueTypeSupported)
                .Single(o => o.Name == "Item");

            Assert.IsInstanceOf<ItemIdView>(_views.Create(item));
        }

        [Test]
        public void TypeRegistration_AppliesToTypesAlreadyScanned_AndUnregisterRemovesIt()
        {
            Assert.IsFalse(
                OptionScanner.Scan(new Container(), _registry.IsValueTypeSupported).Any(o => o.Name == "Item"),
                "Scanned (and cached) before registration.");

            _views.Register<ItemId>(() => new ItemIdView());
            Assert.IsTrue(
                OptionScanner.Scan(new Container(), _registry.IsValueTypeSupported).Any(o => o.Name == "Item"),
                "No cache reset is needed.");

            _views.Unregister(typeof(ItemId));
            Assert.IsFalse(OptionScanner.Scan(new Container(), _registry.IsValueTypeSupported)
                .Any(o => o.Name == "Item"));
        }

        [Test]
        public void CreateDefault_IgnoresTypeRegistrations()
        {
            _views.Register<int>(() => new ItemIdView());
            try
            {
                var o = OptionScanner.Scan(new Container(), _registry.IsValueTypeSupported)
                    .Single(x => x.Name == "Number");
                Assert.IsInstanceOf<ItemIdView>(_views.Create(o));
                Assert.IsNotInstanceOf<ItemIdView>(OptionViewRegistry.CreateDefault(o));
            }
            finally
            {
                _views.Unregister(typeof(int));
            }
        }

        [Test]
        public void Methods_AreAlwaysButtons()
        {
            var o = OptionScanner.Scan(new Container(), _registry.IsValueTypeSupported).Single(x => x.Name == "Run");
            Assert.AreEqual("ButtonOptionView", _views.Create(o).GetType().Name);
        }

        [Test]
        public void Context_ConvertsAndClampsToRange()
        {
            var value = 0;
            var o = OptionDefinition.Create("v", () => value, v => value = v, range: new NumberRangeAttribute(0, 10));
            var ctx = new OptionContext(o, null, OptionPlacement.Panel);

            ctx.SetValue(4.6);
            Assert.AreEqual(5, value);
            ctx.SetValue(99L);
            Assert.AreEqual(10, value);
        }

        [Test]
        public void Context_RoutesThroughGroupPending()
        {
            var registry = new OptionRegistry();
            var text = "a";
            var input = OptionDefinition.Create("T", () => text, v => text = v, "C",
                group: new OptionGroupAttribute("g"));
            var invoked = "";
            var run = OptionDefinition.FromMethod("Run", () => invoked = text, "C", 1,
                group: new OptionGroupAttribute("g"));
            registry.AddOption(input);
            registry.AddOption(run);
            var group = registry.GetGroup(input);

            var ctx = new OptionContext(input, group, OptionPlacement.Panel);
            ctx.SetValue("b");
            Assert.AreEqual("a", text);
            Assert.IsTrue(ctx.IsPending);
            Assert.AreEqual("b", ctx.GetValue());

            new OptionContext(run, group, OptionPlacement.Panel).Invoke();
            Assert.AreEqual("b", invoked);
        }

        [TestCase(300.0, typeof(sbyte), (sbyte)127)]
        [TestCase(-3.7, typeof(int), -4)]
        [TestCase(2.5f, typeof(double), 2.5)]
        public void Converter_ClampsAndRounds(object input, Type type, object expected)
        {
            Assert.AreEqual(expected, OptionValueConverter.Convert(input, type));
        }

        [Test]
        public void Converter_EnumsAndColors()
        {
            Assert.AreEqual(LogType.Warning, OptionValueConverter.Convert("Warning", typeof(LogType)));
            Assert.AreEqual(LogType.Error, OptionValueConverter.Convert(0L, typeof(LogType)));
            Assert.IsTrue(OptionValueConverter.TryParseColor("FF000080", out var c));
            Assert.AreEqual("#FF000080", OptionValueConverter.ToHex(c));
        }

        public struct ItemId
        {
            public int Value;
        }

        private sealed class ItemIdView : OptionView
        {
            public override VisualElement CreateElement(OptionContext context)
            {
                return new Label("item");
            }
        }

        private class Container
        {
            public int Number { get; set; }

            public ItemId Item { get; set; }

            public GameObject Unsupported { get; set; }

            public sbyte Small { get; set; }
            public Color Tint { get; set; }

            public void Run()
            {
            }

            public VisualElement Map()
            {
                return new VisualElement { name = "map" };
            }
        }
    }
}