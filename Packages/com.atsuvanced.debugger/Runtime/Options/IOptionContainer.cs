using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Explicit option source. Containers that do not implement this interface are scanned via reflection.
    /// </summary>
    public interface IOptionContainer
    {
        /// <summary>True if options may be added/removed after registration.</summary>
        bool IsDynamic { get; }

        IEnumerable<OptionDefinition> GetOptions();

        event Action<OptionDefinition> OptionAdded;
        event Action<OptionDefinition> OptionRemoved;
    }
}