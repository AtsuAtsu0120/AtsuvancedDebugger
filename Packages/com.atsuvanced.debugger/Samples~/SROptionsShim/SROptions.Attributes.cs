// SRDebugger-style nested attributes. They derive from the Atsuvanced attributes,
// so existing code such as [NumberRange(0, 10)] inside 'partial class SROptions' keeps compiling.
public partial class SROptions
{
    public sealed class NumberRangeAttribute : Atsuvanced.Debugger.NumberRangeAttribute
    {
        public NumberRangeAttribute(double min, double max) : base(min, max)
        {
        }
    }

    public sealed class IncrementAttribute : Atsuvanced.Debugger.IncrementAttribute
    {
        public IncrementAttribute(double increment) : base(increment)
        {
        }
    }

    public sealed class SortAttribute : Atsuvanced.Debugger.OptionSortAttribute
    {
        public SortAttribute(int priority) : base(priority)
        {
        }
    }

    public sealed class DisplayNameAttribute : Atsuvanced.Debugger.OptionNameAttribute
    {
        public DisplayNameAttribute(string name) : base(name)
        {
        }
    }

    /// <summary>Atsuvanced extension: shows several members as one UI unit (ignored by SRDebugger itself).</summary>
    public sealed class GroupAttribute : Atsuvanced.Debugger.OptionGroupAttribute
    {
        public GroupAttribute(string key) : base(key)
        {
        }
    }
}
