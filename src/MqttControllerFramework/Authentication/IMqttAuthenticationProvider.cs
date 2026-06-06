namespace MqttControllerFramework.Authentication;

/// <summary>
///     Implement and register this to authenticate MQTT clients by username and password.
///     This is required — the broker will reject all connections if no provider is registered.
/// </summary>
public interface IMqttAuthenticationProvider
{
    /// <summary>Authenticates an MQTT client.</summary>
    /// <param name="username">Client-supplied username.</param>
    /// <param name="password">Client-supplied password (plain text).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     <see cref="MqttAuthenticationResult.Authenticated"/> on success,
    ///     or one of the failure factories otherwise.
    /// </returns>
    ValueTask<MqttAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
