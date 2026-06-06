using System.Collections;
using FluentAssertions;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttControllerFramework.Stats;
using MqttProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion;

namespace MqttControllerFramework.Tests.Stats;

public sealed class MqttBrokerStatsServiceTests
{
    private readonly MqttBrokerStatsService _svc = new();

    // ── Simple counter tests ───────────────────────────────────────────────

    [Fact]
    public void InitialState_AllCountersAreZero()
    {
        _svc.GetTotalMessageCount().Should().Be(0);
        _svc.GetTotalConnectedClientCount().Should().Be(0);
        _svc.GetDroppedMessageCount().Should().Be(0);
        _svc.GetUnconsumedMessageCount().Should().Be(0);
        _svc.GetAcknowledgedMessageCount().Should().Be(0);
        _svc.GetQueueOverwriteCount().Should().Be(0);
        _svc.GetRetainedMessageCount().Should().Be(0);
        _svc.GetSessionCount().Should().Be(0);
        _svc.GetSubscriptionCount().Should().Be(0);
    }

    [Fact]
    public void AcceptNewConnections_DefaultIsTrue()
    {
        _svc.GetAcceptNewConnections().Should().BeTrue();
    }

    [Fact]
    public void SetAcceptNewConnections_False_ReturnsFalse()
    {
        _svc.SetAcceptNewConnections(false);

        _svc.GetAcceptNewConnections().Should().BeFalse();
    }

    [Fact]
    public void MarkBrokerStarted_UptimeBecomesPositive()
    {
        _svc.GetUptime().Should().Be(TimeSpan.Zero);

        _svc.MarkBrokerStarted();

        _svc.GetUptime().Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void IncrementDroppedMessageCount_IncrementsOnce()
    {
        _svc.IncrementDroppedMessageCount();

        _svc.GetDroppedMessageCount().Should().Be(1);
    }

    [Fact]
    public void IncrementUnconsumedMessageCount_IncrementsOnce()
    {
        _svc.IncrementUnconsumedMessageCount();

        _svc.GetUnconsumedMessageCount().Should().Be(1);
    }

    [Fact]
    public void IncrementAcknowledgedMessageCount_IncrementsOnce()
    {
        _svc.IncrementAcknowledgedMessageCount();

        _svc.GetAcknowledgedMessageCount().Should().Be(1);
    }

    [Fact]
    public void IncrementQueueOverwriteCount_IncrementsOnce()
    {
        _svc.IncrementQueueOverwriteCount();

        _svc.GetQueueOverwriteCount().Should().Be(1);
    }

    // ── Publish stats ──────────────────────────────────────────────────────

    [Fact]
    public async Task OnInterceptingPublish_ClientMessage_UpdatesCountersAndBrokerBytesReceived()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var args = BuildPublishArgs("sensors/temp", payload);

        await _svc.MqttServerOnInterceptingPublishAsync(args, isServer: false);

        _svc.GetTotalMessageCount().Should().Be(1);
        _svc.GetTotalMessageSize().Should().Be(4);
        _svc.GetTotalBytesReceivedByBroker().Should().Be(4);
        _svc.GetTotalBytesSentByClients().Should().Be(4);
        _svc.GetTotalBytesSentByBroker().Should().Be(0);
    }

    [Fact]
    public async Task OnInterceptingPublish_ServerMessage_UpdatesBrokerSentBytes()
    {
        var payload = new byte[] { 0xFF };
        var args = BuildPublishArgs("cmd/device", payload);

        await _svc.MqttServerOnInterceptingPublishAsync(args, isServer: true);

        _svc.GetTotalBytesSentByBroker().Should().Be(1);
        _svc.GetTotalBytesReceivedByClients().Should().Be(1);
        _svc.GetTotalBytesReceivedByBroker().Should().Be(0);
    }

    [Fact]
    public async Task OnInterceptingPublish_RetainedMessage_IncrementsRetainedCount()
    {
        var message = new MqttApplicationMessage { Topic = "t/v", Retain = true };
        var args = new InterceptingPublishEventArgs(message, "c1", "u1", new Hashtable(), CancellationToken.None);

        await _svc.MqttServerOnInterceptingPublishAsync(args, isServer: false);

        _svc.GetRetainedMessageCount().Should().Be(1);
    }

    [Fact]
    public async Task OnInterceptingPublish_PopulatesTopicStats()
    {
        var args = BuildPublishArgs("data/topic1", new byte[10]);

        await _svc.MqttServerOnInterceptingPublishAsync(args, isServer: false);
        await _svc.MqttServerOnInterceptingPublishAsync(args, isServer: false);

        var summary = _svc.GetTopicSummary();
        summary.Should().ContainKey("data/topic1");
        summary["data/topic1"].MessageCount.Should().Be(2);
        summary["data/topic1"].TotalBytes.Should().Be(20);
    }

    // ── Client connected / disconnected ────────────────────────────────────

    [Fact]
    public async Task OnClientConnected_IncrementsConnectedCount()
    {
        await _svc.MqttServerOnClientConnectedAsync(BuildConnectedArgs("c1"));

        _svc.GetTotalConnectedClientCount().Should().Be(1);
        _svc.GetSessionCount().Should().Be(1);
    }

    [Fact]
    public async Task OnClientDisconnected_DecrementsConnectedCount()
    {
        await _svc.MqttServerOnClientConnectedAsync(BuildConnectedArgs("c1"));
        await _svc.MqttServerOnClientDisconnectedAsync(BuildDisconnectedArgs("c1"));

        _svc.GetTotalConnectedClientCount().Should().Be(0);
        _svc.GetSessionCount().Should().Be(0);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static InterceptingPublishEventArgs BuildPublishArgs(string topic, byte[] payload)
    {
        var message = new MqttApplicationMessage { Topic = topic };
        message.Payload = new System.Buffers.ReadOnlySequence<byte>(payload);
        return new InterceptingPublishEventArgs(message, "client1", "user1", new Hashtable(), CancellationToken.None);
    }

    private static ClientConnectedEventArgs BuildConnectedArgs(string clientId)
    {
        var packet = new MqttConnectPacket { ClientId = clientId };
        return new ClientConnectedEventArgs(packet, MqttProtocolVersion.V500, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0), new Hashtable());
    }

    private static ClientDisconnectedEventArgs BuildDisconnectedArgs(string clientId)
    {
        var packet = new MqttConnectPacket { ClientId = clientId };
        return new ClientDisconnectedEventArgs(packet, null, MqttClientDisconnectType.Clean, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0), new Hashtable());
    }
}
