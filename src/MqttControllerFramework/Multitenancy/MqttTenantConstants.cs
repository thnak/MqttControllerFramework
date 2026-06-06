namespace MqttControllerFramework.Multitenancy;

/// <summary>Well-known constants for the multi-tenancy support.</summary>
public static class MqttTenantConstants
{
    /// <summary>
    ///     Key used in <c>MqttMessageContext.SessionItems</c> to store the resolved tenant ID string
    ///     (string-based resolver path). Also accepted when set manually inside
    ///     <c>IMqttConnectionValidator</c>.
    /// </summary>
    public const string SessionItemKey = "tenantId";

    /// <summary>
    ///     Key used in <c>MqttMessageContext.SessionItems</c> to store the resolved typed tenant
    ///     object (typed resolver path via <c>IMqttTenantResolver&lt;T&gt;</c>).
    /// </summary>
    public const string TenantInfoKey = "tenantInfo";
}
