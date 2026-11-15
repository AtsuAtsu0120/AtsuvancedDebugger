using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Static entry point (SRDebug equivalent). The debugger initializes itself before the first scene loads with
    ///     <see cref="DebuggerBuilder.CreateDefault" /> plus every <see cref="Configure" /> registered until then.
    /// </summary>
    public static class AtsuDebug
    {
        private static readonly List<Action<DebuggerBuilder>> s_configurators = new();
        private static DebugService s_instance;

        /// <summary>False when <c>ATSU_DEBUGGER_DISABLED</c> is defined.</summary>
        public static bool IsEnabled
        {
            get
            {
#if ATSU_DEBUGGER_DISABLED
                return false;
#else
                return true;
#endif
            }
        }

        public static bool IsInitialized => s_instance != null;

        /// <summary>The debug service. Initializes on first access.</summary>
        public static IDebugService Instance
        {
            get
            {
                if (s_instance == null)
                {
                    Init();
                }

                return s_instance;
            }
        }

        /// <summary>
        ///     Changes how the debugger is built (method chain on <see cref="DebuggerBuilder" />). Call before it initializes,
        ///     e.g.
        ///     from <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]</c>; the debugger
        ///     initializes at <c>BeforeSceneLoad</c>. Several calls are applied in order.
        /// </summary>
        public static void Configure(Action<DebuggerBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            if (s_instance != null)
            {
                Debug.LogWarning(
                    "[AtsuvancedDebugger] AtsuDebug.Configure was called after the debugger initialized; " +
                    "it is ignored. Call it from RuntimeInitializeLoadType.AfterAssembliesLoaded (or earlier).");
                return;
            }

            s_configurators.Add(configure);
        }

        /// <summary>
        ///     Initializes the debugger: <see cref="DebuggerBuilder.CreateDefault" /> with the settings asset, then every
        ///     <see cref="Configure" /> in order, then <paramref name="configure" />. Subsequent calls are ignored.
        ///     When <c>ATSU_DEBUGGER_DISABLED</c> is defined, a passive service is created that captures nothing and shows no UI.
        /// </summary>
        public static void Init(Action<DebuggerBuilder> configure = null)
        {
            if (s_instance != null)
            {
                return;
            }

            var builder = CreateBuilder();
            configure?.Invoke(builder);
            s_instance = builder.Build(IsEnabled, Application.isPlaying);
        }

        /// <summary>
        ///     Deletes everything the debugger saved (pins, selected tab / section, open categories, last search and values saved
        ///     with <see cref="OptionPersistAttribute" />). The running session keeps its current state; the next launch starts
        ///     fresh. Returns the number of deleted keys.
        /// </summary>
        public static int ClearSavedState()
        {
            if (s_instance != null)
            {
                return s_instance.State.Clear();
            }

            return new DebuggerState(CreateBuilder().Storage ?? new PlayerPrefsDebuggerStorage()).Clear();
        }

        /// <summary>Tears down the service. Mainly for tests and Enter Play Mode Options.</summary>
        public static void Shutdown()
        {
            s_instance?.Dispose();
            s_instance = null;
        }

        private static DebuggerBuilder CreateBuilder()
        {
            var builder = DebuggerBuilder.CreateDefault(DebuggerSettingsAsset.Instance);
            foreach (var configure in s_configurators)
            {
                try
                {
                    configure(builder);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return builder;
        }

        // Domain reload may be disabled (Enter Play Mode Options): reset static state each play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Shutdown();
            s_configurators.Clear();
        }

#if !ATSU_DEBUGGER_DISABLED && !ATSU_DEBUGGER_DISABLE_AUTO_INIT
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            Init();
        }
#endif
    }
}