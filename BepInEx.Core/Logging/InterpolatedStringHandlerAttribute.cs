// ReSharper disable once CheckNamespace

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class InterpolatedStringHandlerAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
{
    public InterpolatedStringHandlerArgumentAttribute(string argument)
    {
        Arguments = new[] { argument };
    }

    public InterpolatedStringHandlerArgumentAttribute(params string[] arguments)
    {
        Arguments = arguments;
    }

    public string[] Arguments { get; }
}
