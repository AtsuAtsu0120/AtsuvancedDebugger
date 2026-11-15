using Atsuvanced.Debugger;
using SRDebugger.Services;
using IDebugService = SRDebugger.Services.IDebugService;

/// <summary>SRDebugger compatible entry point. Forwards to <see cref="AtsuDebug" />.</summary>
public static class SRDebug
{
    public const string Version = "atsuvanced-compat";

    private static SRDebugServiceAdapter _adapter;
    private static Atsuvanced.Debugger.IDebugService _adaptedCore;

    public static bool IsInitialized => AtsuDebug.IsInitialized;

    public static IDebugService Instance
    {
        get
        {
            var core = AtsuDebug.Instance;
            // Re-create the adapter if the core service was replaced (Shutdown / play mode restart).
            if (_adapter == null || !ReferenceEquals(_adaptedCore, core))
            {
                _adapter = new SRDebugServiceAdapter(core);
                _adaptedCore = core;
            }

            return _adapter;
        }
    }

    public static void Init()
    {
        AtsuDebug.Init();
    }
}