using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Atsuvanced.Debugger.Tests
{
    public class ValuePersistenceTests
    {
        [Flags]
        public enum Layers
        {
            None = 0,
            A = 1,
            B = 2,
            C = 4
        }

        public enum Mode
        {
            Easy,
            Hard
        }

        private static void RoundTrip(object value)
        {
            var type = value.GetType();
            Assert.IsTrue(OptionValueSerializer.TrySerialize(value, type, out var text), type.Name);
            Assert.IsTrue(OptionValueSerializer.TryDeserialize(text, type, out var back), $"{type.Name}: {text}");
            Assert.AreEqual(value, back, $"{type.Name}: {text}");
        }

        [Test]
        public void Serializer_RoundTripsSupportedTypes()
        {
            RoundTrip(true);
            RoundTrip(false);
            RoundTrip(42);
            RoundTrip(-7L);
            RoundTrip((byte)200);
            RoundTrip(0.1f);
            RoundTrip(1234.5678);
            RoundTrip("日本語, with comma");
            RoundTrip(string.Empty);
            RoundTrip(Mode.Hard);
            RoundTrip(Layers.A | Layers.C);
            RoundTrip(new Vector2(1.5f, -2f));
            RoundTrip(new Vector3(0.1f, 0.2f, 0.3f));
            RoundTrip(new Vector4(1, 2, 3, 4));
            RoundTrip(new Color(0.25f, 0.5f, 0.75f, 1f));
        }

        [Test]
        public void Serializer_IsCultureIndependent_AndRejectsGarbage()
        {
            Assert.IsTrue(OptionValueSerializer.TrySerialize(1.5f, typeof(float), out var text));
            Assert.AreEqual("1.5", text);
            Assert.IsFalse(OptionValueSerializer.TryDeserialize("abc", typeof(int), out _));
            Assert.IsFalse(OptionValueSerializer.TryDeserialize("Medium", typeof(Mode), out _), "Removed enum member.");
            Assert.IsFalse(OptionValueSerializer.TryDeserialize("1,2", typeof(Vector3), out _));
            Assert.IsFalse(OptionValueSerializer.TryDeserialize("yes", typeof(bool), out _));
            Assert.IsFalse(OptionValueSerializer.IsSupported(typeof(Rect)));
        }

        [Test]
        public void PersistentValues_AreRestoredOnNextRegistration()
        {
            var storage = new MemoryDebuggerStorage();

            var r1 = new OptionRegistry();
            var p1 = new ValuePersistence(r1, new DebuggerState(storage));
            r1.AddContainer(new Cheats());
            Option(r1, "GodMode").SetValue(true);
            Option(r1, "Speed").SetValue(2.5f);
            Option(r1, "Name").SetValue(string.Empty);
            Option(r1, "NotSaved").SetValue(9);
            p1.Dispose();

            Assert.AreEqual("=2.5", storage.GetString(DebuggerState.KeyPrefix + "value/speed-v1"), "Custom key.");

            var r2 = new OptionRegistry();
            new ValuePersistence(r2, new DebuggerState(storage));
            var fresh = new Cheats();
            r2.AddContainer(fresh);
            Assert.IsTrue(fresh.GodMode);
            Assert.AreEqual(2.5f, fresh.Speed);
            Assert.AreEqual(string.Empty, fresh.Name, "An empty string is a saved value too.");
            Assert.AreEqual(0, fresh.NotSaved);
        }

        [Test]
        public void InvalidSavedValue_IsIgnored()
        {
            var storage = new MemoryDebuggerStorage();
            storage.SetString(DebuggerState.KeyPrefix + "value/Cheats/GodMode", "=maybe");
            var r = new OptionRegistry();
            new ValuePersistence(r, new DebuggerState(storage));
            var c = new Cheats();
            r.AddContainer(c);
            Assert.IsFalse(c.GodMode);
        }

        [Test]
        public void CodeOptions_CanOptIn_WithPersistence()
        {
            var storage = new MemoryDebuggerStorage();
            var volume = 1f;
            var r1 = new OptionRegistry();
            new ValuePersistence(r1, new DebuggerState(storage));
            r1.AddOption(OptionDefinition.Create("Volume", () => volume, v => volume = v, "Audio").WithPersistence());
            r1.Options[0].SetValue(0.3f);

            volume = 1f;
            var r2 = new OptionRegistry();
            new ValuePersistence(r2, new DebuggerState(storage));
            r2.AddOption(OptionDefinition.Create("Volume", () => volume, v => volume = v, "Audio").WithPersistence());
            Assert.AreEqual(0.3f, volume);
        }

        private static OptionDefinition Option(OptionRegistry r, string name)
        {
            return r.Options.First(o => o.Name == name);
        }

        private class Cheats
        {
            [OptionCategory("Cheats")]
            [OptionPersist]
            public bool GodMode { get; set; }

            [OptionCategory("Cheats")]
            [OptionPersist(Key = "speed-v1")]
            [NumberRange(0, 10)]
            public float Speed { get; set; } = 1f;

            [OptionCategory("Cheats")]
            [OptionPersist]
            public string Name { get; set; } = "Hero";

            [OptionCategory("Cheats")] public int NotSaved { get; set; }
        }
    }
}