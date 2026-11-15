using Atsuvanced.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtsuvancedDebuggerSamples
{
    /// <summary>Registers a custom tab and an option that opens a page.</summary>
    public static class CustomPageSample
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            var debugger = AtsuDebug.Instance;

            // 1. A custom tab whose root page is a fully custom page.
            debugger.Tabs.Add(new DebugTabDefinition("feedback", "Feedback", () => new FeedbackPage(), order: 450));

            // 2. A page that hides the header and tab bar entirely and handles navigation itself.
            debugger.AddOption(OptionDefinition.FromPage("Open Fullscreen Page", () => new FullscreenPage(), "Samples"));
        }
    }

    public sealed class FullscreenPage : DebugPage
    {
        public FullscreenPage() : base("Fullscreen")
        {
        }

        public override PageChrome Chrome => PageChrome.None;

        protected override VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.Add(new Label("This page owns the whole panel."));
            var back = new Button(() => Navigator.Pop()) { text = "Back" };
            back.AddToClassList("atsu-button");
            root.Add(back);
            return root;
        }
    }
}
