namespace MqttControllerFramework.Authorization;

/// <summary>
///     Implement and register this to enforce publish-time authorization on routes
///     decorated with <see cref="Attributes.MqttAuthorizeAttribute"/>.
/// </summary>
public interface IMqttAuthorizationProvider
{
    /// <summary>
    ///     Called before dispatching a message on an authorized route.
    ///     Return <see cref="MqttAuthorizationResult.Allow"/> to permit delivery,
    ///     or <see cref="MqttAuthorizationResult.Deny(string, int?, bool?)"/> to block it.
    /// </summary>
    ValueTask<MqttAuthorizationResult> AuthorizePublishAsync(
        string userName,
        string topic,
        int qos,
        bool retain,
        CancellationToken cancellationToken = default);
}
