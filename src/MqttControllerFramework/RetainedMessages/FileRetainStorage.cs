using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MqttControllerFramework.Configuration;
using MqttControllerFramework.Serialization;

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
        var messages = await JsonSerializer.DeserializeAsync(
            fs, FrameworkJsonContext.Default.ListMqttApplicationMessage, cancellationToken);
        return messages ?? [];
    }

    public async Task SaveAsync(IReadOnlyList<MqttApplicationMessage> messages, CancellationToken cancellationToken = default)
    {
        await using var fs = File.Create(_path);
        await JsonSerializer.SerializeAsync(
            fs, messages, FrameworkJsonContext.Default.ListMqttApplicationMessage, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
