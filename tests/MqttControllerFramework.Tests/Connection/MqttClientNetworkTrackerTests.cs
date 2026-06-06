using System.Net;
using FluentAssertions;
using MqttControllerFramework.Connection;

namespace MqttControllerFramework.Tests.Connection;

public sealed class MqttClientNetworkTrackerTests
{
    private readonly MqttClientNetworkTracker _tracker = new();

    [Fact]
    public void Track_ThenGet_ReturnsAddress()
    {
        _tracker.TrackClientNetworkActivity("client1", new IPEndPoint(IPAddress.Parse("192.168.1.10"), 1234));

        _tracker.GetClientEndPointAddress("client1").Should().Be("192.168.1.10");
    }

    [Fact]
    public void GetUnknownClient_ReturnsNull()
    {
        _tracker.GetClientEndPointAddress("unknown").Should().BeNull();
    }

    [Fact]
    public void TrackMultipleClients_IndependentEntries()
    {
        _tracker.TrackClientNetworkActivity("c1", new IPEndPoint(IPAddress.Parse("10.0.0.1"), 0));
        _tracker.TrackClientNetworkActivity("c2", new IPEndPoint(IPAddress.Parse("10.0.0.2"), 0));

        _tracker.GetClientEndPointAddress("c1").Should().Be("10.0.0.1");
        _tracker.GetClientEndPointAddress("c2").Should().Be("10.0.0.2");
    }

    [Fact]
    public void TrackSameClientTwice_UpdatesAddress()
    {
        _tracker.TrackClientNetworkActivity("client1", new IPEndPoint(IPAddress.Parse("1.1.1.1"), 0));
        _tracker.TrackClientNetworkActivity("client1", new IPEndPoint(IPAddress.Parse("2.2.2.2"), 0));

        _tracker.GetClientEndPointAddress("client1").Should().Be("2.2.2.2");
    }
}
