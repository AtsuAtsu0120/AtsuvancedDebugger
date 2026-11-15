using Atsuvanced.Debugger;
using UnityEngine;

namespace AtsuvancedDebuggerSamples.Showcase
{
    /// <summary>Registers every showcase container when the game starts.</summary>
    public static class OptionsShowcase
    {
        // Compose the debugger before it initializes (BeforeSceneLoad), as a method chain on DebuggerBuilder.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Configure()
        {
            AtsuDebug.Configure(b => b
                // Custom drawing is chosen per value type (registered before any container is scanned).
                .AddOptionView<ItemId>(() => new ItemIdView())
                .AddOptionView<StageName>(() => new StageNameView())
                .AddOptionView<FrameStats>(() => new FrameStatsView())
                .AddOptionView<Percent>(() => new PercentView()));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            var debugger = AtsuDebug.Instance;

            var player = new PlayerOptions();
            var inventory = new InventoryOptions();
            debugger.AddOptionContainer(player);
            debugger.AddOptionContainer(new WorldOptions());
            debugger.AddOptionContainer(inventory);
            debugger.AddOptionContainer(new EnemySpawner());
            var diagnostics = new DiagnosticsOptions();
            debugger.AddOptionContainer(diagnostics);

            // A custom counter in the Profiler tab (FR-PRF-04).
            debugger.Profiler.AddCounter("Simulated Load", () => diagnostics.Load.Value / 100.0, ProfilerUnit.Percent, "Showcase");

            // A button that opens a page.
            debugger.AddOption(OptionDefinition.FromPage("所持品一覧を開く", () => new InventoryPage(inventory), "Inventory", -1));

            // Pin a few options (shown on the game screen while the panel is closed).
            debugger.PinOption("HP");
            debugger.PinAllOptions("World/Time");

            var ticker = new GameObject("[Showcase Ticker]").AddComponent<ShowcaseTicker>();
            ticker.Player = player;
            Object.DontDestroyOnLoad(ticker.gameObject);

            Debug.Log("[Showcase] Options showcase registered. Open the panel with F1 or triple-tap the top-left corner.");
        }
    }
}
