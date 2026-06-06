#if NET10_0_OR_GREATER
using System.Text.Json.Serialization;

namespace MqttControllerFramework.Serialization;

[JsonSerializable(typeof(List<MQTTnet.MqttApplicationMessage>))]
internal partial class FrameworkJsonContext : JsonSerializerContext
{
}
#endif
