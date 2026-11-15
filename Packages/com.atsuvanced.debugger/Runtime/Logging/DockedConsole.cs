using System;

namespace Atsuvanced.Debugger
{
    /// <summary>Edge of the screen the docked console sticks to.</summary>
    public enum ConsoleAlignment
    {
        Top,
        Bottom
    }

    /// <summary>
    ///     The console docked to the top or bottom of the game screen (shown while the panel is closed), implemented by
    ///     <c>DockedConsoleFeature</c>. The state is persisted with the rest of the debugger state.
    /// </summary>
    public interface IDockedConsole
    {
        /// <summary>Whether the docked console is shown (while the panel is closed).</summary>
        bool IsVisible { get; set; }

        /// <summary>Expanded = the latest log lines are listed; collapsed = one bar with counts and the latest message.</summary>
        bool IsExpanded { get; set; }

        ConsoleAlignment Alignment { get; set; }

        /// <summary>Raised when any of the properties changes.</summary>
        event Action Changed;
    }
}