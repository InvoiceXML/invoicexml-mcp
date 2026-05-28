using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Core.Tests.TestSupport;

/// <summary>
/// Minimal <see cref="IOptionsMonitor{T}"/> returning a fixed value, for unit
/// tests of services that take <see cref="IOptionsMonitor{T}"/> without needing
/// a full DI container.
/// </summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
