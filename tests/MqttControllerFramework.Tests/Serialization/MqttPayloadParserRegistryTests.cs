using MqttControllerFramework.Abstracts;
using MqttControllerFramework.Attributes;
using MqttControllerFramework.Serialization;

namespace MqttControllerFramework.Tests.Serialization;

public class MqttPayloadParserRegistryTests
{
    [Fact]
    public void RegisterParser_ThenTryGetParser_ReturnsSameInstance()
    {
        var registry = new MqttPayloadParserRegistry();
        registry.RegisterParser("application/msgpack", typeof(SampleParser), () => new SampleParser());

        var result = registry.TryGetParser("application/msgpack", typeof(SampleParser));
        result.Should().BeOfType<SampleParser>();
    }

    [Fact]
    public void TryGetParserByType_UsedAsFallback()
    {
        var registry = new MqttPayloadParserRegistry();
        registry.RegisterParser("application/custom", typeof(SampleParser), () => new SampleParser());

        var result = registry.TryGetParserByType(typeof(SampleParser));
        result.Should().BeOfType<SampleParser>();
    }

    [Fact]
    public void TryGetParser_WithUnknownContentType_ReturnsNull()
    {
        var registry = new MqttPayloadParserRegistry();
        registry.TryGetParser("unknown/type", typeof(SampleParser)).Should().BeNull();
    }

    [Fact]
    public void RegisterParser_NullContentType_Throws()
    {
        var registry = new MqttPayloadParserRegistry();
        var act = () => registry.RegisterParser("", typeof(SampleParser), () => new SampleParser());
        act.Should().Throw<ArgumentException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    [MqttPayloadContentType("application/msgpack")]
    private sealed class SampleParser : IMqttPayloadParser<int>
    {
        public int Parse(ReadOnlySpan<byte> payload) => payload.Length;
    }
}
