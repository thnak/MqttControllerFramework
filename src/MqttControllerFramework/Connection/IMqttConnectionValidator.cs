using MQTTnet.Server;

namespace MqttControllerFramework.Connection;

/// <summary>
///     Optional hook called for every incoming connection <em>before</em> authentication.
///     Register an implementation to enforce custom ClientId rules, ban-lists, IP filters, etc.
///     You can also set <see cref="ValidatingConnectionEventArgs.SessionItems"/> here.
/// </summary>
public interface IMqttConnectionValidator
{
    /// <summary>
    ///     Validates an incoming connection.
    ///     Return <see cref="MqttConnectionValidationResult.Accept"/> to let the connection proceed,
    ///     or <see cref="MqttConnectionValidationResult.Reject"/> to refuse it immediately.
    /// </summary>
    ValueTask<MqttConnectionValidationResult> ValidateAsync(
        ValidatingConnectionEventArgs context,
        CancellationToken cancellationToken = default);
}
