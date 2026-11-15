using System;
using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     A unit of the debugger's behaviour, like a Renderer Feature in URP. Every built-in tab, overlay and shortcut is a
    ///     feature built on the public API (<see cref="IDebugService" />), and the set of features is chosen when the debugger
    ///     is
    ///     built (<see cref="DebuggerBuilder" />):
    ///     <code>
    /// AtsuDebug.Configure(b => b
    ///     .Remove&lt;ProfilerFeature&gt;()
    ///     .Configure&lt;OptionsFeature&gt;(f => f.Layout = OptionsLayout.SingleTab)
    ///     .Add(new NetworkFeature()));
    /// </code>
    ///     Read settings from <c>Debugger.Settings</c> in <see cref="OnInstall" />. Exceptions are logged and never break the
    ///     debugger.
    /// </summary>
    public abstract class DebuggerFeature
    {
        /// <summary>The debugger this feature is installed in. Null before installation.</summary>
        public IDebugService Debugger { get; private set; }

        public bool IsInstalled => Debugger != null;

        /// <summary>Adds the feature's tabs, sections, overlays and event handlers.</summary>
        protected abstract void OnInstall();

        /// <summary>Removes what <see cref="OnInstall" /> added. Called when the debugger shuts down.</summary>
        protected virtual void OnUninstall()
        {
        }

        /// <summary>Called every frame in play mode (unscaled delta time in seconds), whether the panel is open or not.</summary>
        protected virtual void OnUpdate(float unscaledDeltaTime)
        {
        }

        internal void InternalInstall(IDebugService service)
        {
            if (Debugger != null)
            {
                throw new InvalidOperationException($"{GetType().Name} is already installed.");
            }

            Debugger = service;
            Guard(OnInstall);
        }

        internal void InternalUninstall()
        {
            if (Debugger == null)
            {
                return;
            }

            Guard(OnUninstall);
            Debugger = null;
        }

        internal void InternalUpdate(float unscaledDeltaTime)
        {
            try
            {
                OnUpdate(unscaledDeltaTime);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public override string ToString()
        {
            return $"Feature:{GetType().Name}";
        }
    }
}