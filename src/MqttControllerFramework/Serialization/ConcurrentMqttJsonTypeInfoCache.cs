using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MqttControllerFramework.Serialization;

/// <summary>
///     Thread-safe <see cref="IMqttJsonTypeInfoCache"/> backed by <see cref="JsonSerializerOptions"/>.
/// </summary>
public sealed class ConcurrentMqttJsonTypeInfoCache : IMqttJsonTypeInfoCache
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Initializes the cache using the supplied <paramref name="options"/> as the type-info source.</summary>
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
