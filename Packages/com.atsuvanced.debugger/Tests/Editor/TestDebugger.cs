using System;

namespace Atsuvanced.Debugger.Tests
{
    /// <summary>
    ///     Builds a debugger the way <see cref="AtsuDebug" /> does (default features), without play mode, logs or the
    ///     asset.
    /// </summary>
    internal static class TestDebugger
    {
        public static DebugService Create(IDebuggerStorage storage = null, Action<DebuggerSettings> settings = null,
            Action<DebuggerBuilder> configure = null)
        {
            var builder = DebuggerBuilder.CreateDefault();
            settings?.Invoke(builder.Settings);
            builder.UseStorage(storage ?? new MemoryDebuggerStorage());
            configure?.Invoke(builder);
            return builder.Build(true, false, false, true);
        }
    }
}