using System.Text.Json.Serialization.Metadata;

namespace MqttControllerFramework.Serialization;

/// <summary>
///     Thread-safe cache for <see cref="JsonTypeInfo"/> instances used by the dispatcher.
/// </summary>
public interface IMqttJsonTypeInfoCache
{
    /// <summary>Returns the <see cref="JsonTypeInfo"/> for <paramref name="type"/>, or <c>null</c> if unavailable.</summary>
    JsonTypeInfo? TryGet(Type type);
}
