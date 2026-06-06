using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MqttControllerFramework.RateLimiting;
using MqttControllerFramework.RateLimiting.Abstractions;
using MqttControllerFramework.RateLimiting.Strategies;

namespace MqttControllerFramework.Tests.RateLimiting;

public class MqttRateLimitServiceTests
{
    private static MqttRateLimitService BuildService(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider();
        var svc = new MqttRateLimitService(NullLogger<MqttRateLimitService>.Instance);
        svc.RegisterStrategy(new TokenBucketRateLimitStrategy(time));
        return svc;
    }

    [Fact]
    public async Task AllowsMessageWhenNoRulesRegistered()
    {
        var svc = BuildService(out _);
        var result = await svc.CheckRateLimitAsync("user", "devices/1/data", 100);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task AllowsMessageWithinBucketCapacity()
    {
        var svc = BuildService(out _);
        svc.RegisterTopicRateLimits("devices/+/data", new[]
        {
            new RouteRateLimitConfig
            {
                StrategyType = "TokenBucket",
                Priority = 0,
                Configuration = new Dictionary<string, object>
                {
                    ["Capacity"] = 5, ["RefillRate"] = 1, ["RefillIntervalMs"] = 1000, ["TokensPerRequest"] = 1
                }
            }
        });

        for (int i = 0; i < 5; i++)
        {
            var r = await svc.CheckRateLimitAsync("user", "devices/abc/data", 50);
            r.IsAllowed.Should().BeTrue($"message {i + 1} should be allowed");
        }
    }

    [Fact]
    public async Task DeniesMessageWhenBucketExhausted()
    {
        var svc = BuildService(out _);
        svc.RegisterTopicRateLimits("test/+/msg", new[]
        {
            new RouteRateLimitConfig
            {
                StrategyType = "TokenBucket",
                Priority = 0,
                Configuration = new Dictionary<string, object>
                {
                    ["Capacity"] = 2, ["RefillRate"] = 1, ["RefillIntervalMs"] = 60000, ["TokensPerRequest"] = 1
                }
            }
        });

        await svc.CheckRateLimitAsync("u", "test/x/msg", 10);
        await svc.CheckRateLimitAsync("u", "test/x/msg", 10);
        var denied = await svc.CheckRateLimitAsync("u", "test/x/msg", 10);
        denied.IsAllowed.Should().BeFalse();
        denied.RetryAfterSeconds.Should().BeGreaterThan(0);
    }
}
