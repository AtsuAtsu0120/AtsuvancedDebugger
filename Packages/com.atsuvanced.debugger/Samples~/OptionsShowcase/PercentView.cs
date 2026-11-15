using Atsuvanced.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtsuvancedDebuggerSamples.Showcase
{
    /// <summary>A value from 0 to 100. Drawn by <see cref="PercentView" /> (registered per type in OptionsShowcase).</summary>
    public readonly struct Percent
    {
        public readonly int Value;

        public Percent(int value) => Value = Mathf.Clamp(value, 0, 100);

        public override string ToString() => Value + " %";
    }

    /// <summary>
    /// Type view: every Percent option becomes a gauge with −10 / +10 buttons. Writes go through the context,
    /// so a group's commit mode is respected.
    /// </summary>
    public sealed class PercentView : OptionView
    {
        private ProgressBar _bar;

        public override VisualElement CreateElement(OptionContext context)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.flexGrow = 1;

            _bar = new ProgressBar { lowValue = 0, highValue = 100 };
            _bar.style.flexGrow = 1;
            root.Add(_bar);

            if (context.CanWrite)
            {
                root.Add(Button("-10", () => context.SetValue(new Percent(context.GetValue<Percent>().Value - 10))));
                root.Add(Button("+10", () => context.SetValue(new Percent(context.GetValue<Percent>().Value + 10))));
            }

            return root;
        }

        public override void Refresh(OptionContext context)
        {
            var value = context.GetValue<Percent>();
            _bar.value = value.Value;
            _bar.title = value.ToString();
        }

        private static Button Button(string text, System.Action clicked)
        {
            var b = new Button(clicked) { text = text };
            b.AddToClassList("atsu-button");
            b.AddToClassList("atsu-button--small");
            return b;
        }
    }
}
