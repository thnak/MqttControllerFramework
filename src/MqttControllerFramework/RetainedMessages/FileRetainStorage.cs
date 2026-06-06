using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MqttControllerFramework.Configuration;
#if NET10_0_OR_GREATER
using MqttControllerFramework.Serialization;
#endif

namespace MqttControllerFramework.RetainedMessages;

/// <summary>
///     Default <see cref="IRetainStorage"/> that persists retained messages as a JSON file
///     at <see cref="MqttServerSettings.RetainedMessagesFilePath"/>.
/// </summary>
internal sealed class FileRetainStorage(IOptions<MqttServerSettings> settings) : IRetainStorage
{
    private readonly string _path = settings.Value.RetainedMessagesFilePath;

    public async Task<IReadOnlyList<MqttApplicationMessage>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return [];
        await using var fs = File.OpenRead(_path);
#if NET10_0_OR_GREATER
        var messages = await JsonSerializer.DeserializeAsync(
            fs, FrameworkJsonContext.Default.ListMqttApplicationMessage, cancellationToken);
#else
        var messages = await JsonSerializer.DeserializeAsync<List<MqttApplicationMessage>>(
            fs, cancellationToken: cancellationToken);
#endif
        return messages ?? [];
    }

    public async Task SaveAsync(IReadOnlyList<MqttApplicationMessage> messages, CancellationToken cancellationToken = default)
    {
        await using var fs = File.Create(_path);
#if NET10_0_OR_GREATER
        await JsonSerializer.SerializeAsync(
            fs, messages, FrameworkJsonContext.Default.ListMqttApplicationMessage, cancellationToken);
#else
        await JsonSerializer.SerializeAsync(fs, messages, cancellationToken: cancellationToken);
#endif
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
