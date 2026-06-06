using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Server;
using MqttControllerFramework.Configuration;

namespace MqttControllerFramework.ClientActions;

/// <summary>Default implementation of <see cref="IMqttClientActionService"/>.</summary>
public sealed class MqttClientActionService(
    MqttServer mqttServer,
    JsonSerializerOptions jsonSerializerOptions,
    IOptions<MqttServerSettings> settings)
    : IMqttClientActionService
{
    private readonly string _originName = settings.Value.ServerOriginPropertyName;
    private readonly string _originValue = settings.Value.ServerOriginPropertyValue;

    public Task SendMessageAsync(string topic, ReadOnlySequence<byte> message, CancellationToken cancellationToken = default)
    {
        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(message)
            .WithUserProperty(_originName, _originValue)
            .Build();

        return mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMessage), cancellationToken);
    }

    public async Task<TResult?> SendMessageAsync<TResult>(string topic, ReadOnlySequence<byte> message, CancellationToken cancellationToken = default)
    {
        var responseTopic = $"response/{Guid.NewGuid()}";
        var tcs = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<InterceptingPublishEventArgs, Task> handler = async context =>
        {
            if (context.ApplicationMessage.ResponseTopic != responseTopic) return;
            try
            {
                var result = await DeserializePayloadAsync<TResult>(context.ApplicationMessage.Payload, cancellationToken);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        mqttServer.InterceptingPublishAsync += handler;
        try
        {
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithResponseTopic(responseTopic)
                .Build();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await using var _ = linkedCts.Token.Register(() => tcs.TrySetCanceled());

            await mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMessage), cancellationToken);
            return await tcs.Task;
        }
        finally
        {
            mqttServer.InterceptingPublishAsync -= handler;
        }
    }

    public async Task<TResult?> GetDataFromTopicAsync<TResult>(string topic, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<InterceptingPublishEventArgs, Task> handler = async context =>
        {
            if (context.ApplicationMessage.Topic != topic) return;
            try
            {
                var result = await DeserializePayloadAsync<TResult>(context.ApplicationMessage.Payload, cancellationToken);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        mqttServer.InterceptingPublishAsync += handler;
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await using var _ = linkedCts.Token.Register(() => tcs.TrySetCanceled());

            var requestMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"request/{Guid.NewGuid()}")
                .WithUserProperty(_originName, _originValue)
                .Build();
            await mqttServer.InjectApplicationMessage(new InjectedMqttApplicationMessage(requestMessage), cancellationToken);
            return await tcs.Task;
        }
        finally
        {
            mqttServer.InterceptingPublishAsync -= handler;
        }
    }

    private async Task<TResult?> DeserializePayloadAsync<TResult>(ReadOnlySequence<byte> payload, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        foreach (var segment in payload)
            await ms.WriteAsync(segment, cancellationToken);
        ms.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<TResult>(ms, jsonSerializerOptions, cancellationToken);
    }
}
