using System.Collections.Concurrent;

namespace MqttControllerFramework.Serialization;

/// <summary>
///     Thread-safe <see cref="IMqttPayloadParserRegistry"/> implementation.
/// </summary>
public sealed class MqttPayloadParserRegistry : IMqttPayloadParserRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Type, Func<object>>> _byContentType = new();
    private readonly ConcurrentDictionary<Type, Func<object>> _byType = new();

    /// <inheritdoc />
    public object? TryGetParser(string? contentType, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        if (!_byContentType.TryGetValue(contentType, out var typeMap))
            return null;

        return typeMap.TryGetValue(targetType, out var factory) ? factory() : null;
    }

    /// <inheritdoc />
    public object? TryGetParserByType(Type targetType)
        => _byType.TryGetValue(targetType, out var factory) ? factory() : null;

    /// <inheritdoc />
    public void RegisterParser(string contentType, Type targetType, Func<object> parserFactory)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be null or whitespace.", nameof(contentType));
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(parserFactory);

        var typeMap = _byContentType.GetOrAdd(contentType, _ => new ConcurrentDictionary<Type, Func<object>>());
        typeMap.TryAdd(targetType, parserFactory);
        _byType.TryAdd(targetType, parserFactory);
    }
}
