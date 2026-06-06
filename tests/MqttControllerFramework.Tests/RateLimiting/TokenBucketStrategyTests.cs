using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using MqttControllerFramework.RateLimiting.Abstractions;
using MqttControllerFramework.RateLimiting.Strategies;

namespace MqttControllerFramework.Tests.RateLimiting;

public sealed class TokenBucketStrategyTests
{
    private static RateLimitContext BuildContext(
        string user = "u1",
        string topic = "data/t",
        int capacity = 3,
        int refillRate = 1,
        int refillIntervalMs = 1000)
    {
        var config = new TokenBucketConfiguration
        {
            Capacity = capacity,
            RefillRate = refillRate,
            RefillIntervalMs = refillIntervalMs,
            TokensPerRequest = 1
        };
        return new RateLimitContext
        {
            UserName = user,
            Topic = topic,
            Properties = new Dictionary<string, object> { ["Configuration"] = config }
        };
    }

    [Fact]
    public async Task TokensRefillAfterElapsedTime()
    {
        var time = new FakeTimeProvider();
        var strategy = new TokenBucketRateLimitStrategy(time);
        var ctx = BuildContext(capacity: 2, refillRate: 2, refillIntervalMs: 1000);

        // Exhaust the bucket
        (await strategy.CheckRateLimitAsync(ctx)).IsAllowed.Should().BeTrue();
        (await strategy.CheckRateLimitAsync(ctx)).IsAllowed.Should().BeTrue();
        (await strategy.CheckRateLimitAsync(ctx)).IsAllowed.Should().BeFalse();

        // Advance past one refill interval
        time.Advance(TimeSpan.FromMilliseconds(1001));

        (await strategy.CheckRateLimitAsync(ctx)).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task DifferentUsers_HaveIndependentBuckets()
    {
        var strategy = new TokenBucketRateLimitStrategy(TimeProvider.System);
        var ctxA = BuildContext(user: "alice", capacity: 1);
        var ctxB = BuildContext(user: "bob", capacity: 1);

        (await strategy.CheckRateLimitAsync(ctxA)).IsAllowed.Should().BeTrue();
        // alice exhausted; bob should still be allowed
        (await strategy.CheckRateLimitAsync(ctxA)).IsAllowed.Should().BeFalse();
        (await strategy.CheckRateLimitAsync(ctxB)).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task DeniedResult_IncludesMetadata()
    {
        var strategy = new TokenBucketRateLimitStrategy(TimeProvider.System);
        var ctx = BuildContext(capacity: 1);

        await strategy.CheckRateLimitAsync(ctx); // consume the only token

        var denied = await strategy.CheckRateLimitAsync(ctx);

        denied.IsAllowed.Should().BeFalse();
        denied.Metadata.Should().ContainKey("AvailableTokens");
        denied.Metadata.Should().ContainKey("Capacity");
    }

    [Fact]
    public async Task Reset_RefilsBucketToCapacity()
    {
        var strategy = new TokenBucketRateLimitStrategy(TimeProvider.System);
        var ctx = BuildContext(capacity: 2);

        await strategy.CheckRateLimitAsync(ctx);
        await strategy.CheckRateLimitAsync(ctx);
        (await strategy.CheckRateLimitAsync(ctx)).IsAllowed.Should().BeFalse();

        await strategy.ResetAsync(ctx.UserName, ctx.Topic);

        (await strategy.CheckRateLimitAsync(ctx)).IsAllowed.Should().BeTrue();
    }
}
