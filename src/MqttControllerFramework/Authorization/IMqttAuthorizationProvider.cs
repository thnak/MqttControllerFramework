namespace MqttControllerFramework.Authorization;

/// <summary>
///     Implement and register this to enforce publish-time authorization on routes
///     decorated with <see cref="Attributes.MqttAuthorizeAttribute"/>.
/// </summary>
public interface IMqttAuthorizationProvider
{
    /// <summary>
    ///     Called for every publish before dispatching.
    ///     Return <see cref="MqttAuthorizationResult.Allow"/> to permit,
    ///     or <see cref="MqttAuthorizationResult.Deny(string, int?, bool?)"/> to block.
    /// </summary>
    ValueTask<MqttAuthorizationResult> AuthorizePublishAsync(
        string userName,
        string topic,
        int qos,
        bool retain,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Called for every subscription request.
    ///     Return <see cref="MqttAuthorizationResult.Allow"/> to grant the subscription,
    ///     or <see cref="MqttAuthorizationResult.Deny(string, int?, bool?)"/> to reject it.
    ///     <see cref="MqttAuthorizationResult.MaxQoS"/> caps the granted QoS level when set.
    /// </summary>
    ValueTask<MqttAuthorizationResult> AuthorizeSubscribeAsync(
        string userName,
        string topicFilter,
        int requestedQos,
        CancellationToken cancellationToken = default);
}
