using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MqttControllerFramework.Serialization;

/// <summary>
///     Thread-safe <see cref="IMqttJsonTypeInfoCache"/> backed by <see cref="JsonSerializerOptions"/>.
/// </summary>
public sealed class ConcurrentMqttJsonTypeInfoCache : IMqttJsonTypeInfoCache
{
    private readonly JsonSerializerOptions _options;

    public ConcurrentMqttJsonTypeInfoCache(JsonSerializerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public JsonTypeInfo? TryGet(Type type)
    {
        _options.TryGetTypeInfo(type, out var typeInfo);
        return typeInfo;
    }
}
