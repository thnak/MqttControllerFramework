namespace MqttControllerFramework.Serialization;

/// <summary>
///     Registry for custom MQTT payload parsers keyed by content-type and target type.
/// </summary>
public interface IMqttPayloadParserRegistry
{
    /// <summary>
    ///     Tries to find a parser for the given <paramref name="contentType"/> and <paramref name="targetType"/>.
    ///     Returns <c>null</c> when no match is found.
    /// </summary>
    object? TryGetParser(string? contentType, Type targetType);

    /// <summary>
    ///     Fallback lookup by target type only, used when the message has no ContentType header.
    /// </summary>
    object? TryGetParserByType(Type targetType);

    /// <summary>Registers a parser factory for the given content-type / target-type pair.</summary>
    void RegisterParser(string contentType, Type targetType, Func<object> parserFactory);
}
