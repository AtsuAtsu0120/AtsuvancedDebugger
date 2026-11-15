using UnityEngine;

namespace Atsuvanced.Debugger
{
    /// <summary>Drives <see cref="DebugService.Tick" /> every frame. Lives on the debugger's own GameObject (play mode only).</summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-30000)]
    internal sealed class DebuggerRunner : MonoBehaviour
    {
        internal DebugService Service;

        private void Update()
        {
            Service?.Tick(Time.unscaledDeltaTime);
        }
    }
}