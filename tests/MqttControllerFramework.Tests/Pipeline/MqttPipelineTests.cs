using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Server;
using MqttControllerFramework.Pipeline;

namespace MqttControllerFramework.Tests.Pipeline;

public sealed class MqttPipelineTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static MqttMessageContext BuildContext(IServiceProvider services, string topic = "test/topic")
    {
        var message = new MqttApplicationMessage { Topic = topic };
        var args = new InterceptingPublishEventArgs(message, "client1", "user1", new Hashtable(), CancellationToken.None);
        return new MqttMessageContext { Args = args, Services = services };
    }

    private static (MqttRequestDelegate pipeline, List<string> log) BuildPipeline(
        IServiceProvider services,
        MqttMessageContext context)
    {
        var log = new List<string>();
        var middlewares = services.GetServices<IMqttMiddleware>();
        MqttRequestDelegate terminal = ctx =>
        {
            log.Add("terminal");
            return Task.CompletedTask;
        };
        var pipeline = middlewares.Reverse()
            .Aggregate(terminal, (next, mw) => ctx => mw.InvokeAsync(ctx, next));
        return (pipeline, log);
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoMiddleware_CallsTerminalDirectly()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var context = BuildContext(services);
        var (pipeline, log) = BuildPipeline(services, context);

        await pipeline(context);

        log.Should().ContainSingle().Which.Should().Be("terminal");
    }

    [Fact]
    public async Task SingleMiddleware_RunsBeforeTerminal()
    {
        var log = new List<string>();
        var services = new ServiceCollection()
            .AddScoped<IMqttMiddleware>(_ => new LoggingMiddleware("mw1", log))
            .BuildServiceProvider();
        var context = BuildContext(services);
        var middlewares = services.GetServices<IMqttMiddleware>();
        MqttRequestDelegate terminal = ctx => { log.Add("terminal"); return Task.CompletedTask; };
        var pipeline = middlewares.Reverse().Aggregate(terminal, (next, mw) => ctx => mw.InvokeAsync(ctx, next));

        await pipeline(context);

        log.Should().Equal("mw1", "terminal");
    }

    [Fact]
    public async Task MultipleMiddleware_RunInRegistrationOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection()
            .AddScoped<IMqttMiddleware>(_ => new LoggingMiddleware("first", log))
            .AddScoped<IMqttMiddleware>(_ => new LoggingMiddleware("second", log))
            .BuildServiceProvider();
        var context = BuildContext(services);
        var middlewares = services.GetServices<IMqttMiddleware>();
        MqttRequestDelegate terminal = ctx => { log.Add("terminal"); return Task.CompletedTask; };
        var pipeline = middlewares.Reverse().Aggregate(terminal, (next, mw) => ctx => mw.InvokeAsync(ctx, next));

        await pipeline(context);

        log.Should().Equal("first", "second", "terminal");
    }

    [Fact]
    public async Task ShortCircuit_TerminalNotCalled()
    {
        var log = new List<string>();
        var services = new ServiceCollection()
            .AddScoped<IMqttMiddleware>(_ => new ShortCircuitMiddleware("blocker", log))
            .BuildServiceProvider();
        var context = BuildContext(services);
        var middlewares = services.GetServices<IMqttMiddleware>();
        MqttRequestDelegate terminal = ctx => { log.Add("terminal"); return Task.CompletedTask; };
        var pipeline = middlewares.Reverse().Aggregate(terminal, (next, mw) => ctx => mw.InvokeAsync(ctx, next));

        await pipeline(context);

        log.Should().ContainSingle().Which.Should().Be("blocker");
        log.Should().NotContain("terminal");
    }

    [Fact]
    public async Task Middleware_CanMutateItems_NextMiddlewareSeesMutation()
    {
        var services = new ServiceCollection()
            .AddScoped<IMqttMiddleware>(_ => new ItemWriterMiddleware("key", "value"))
            .AddScoped<IMqttMiddleware>(_ => new ItemReaderMiddleware("key"))
            .BuildServiceProvider();
        var context = BuildContext(services);
        var middlewares = services.GetServices<IMqttMiddleware>();
        MqttRequestDelegate terminal = ctx => Task.CompletedTask;
        var pipeline = middlewares.Reverse().Aggregate(terminal, (next, mw) => ctx => mw.InvokeAsync(ctx, next));

        await pipeline(context);

        context.Items.Should().ContainKey("key").WhoseValue.Should().Be("value");
        context.Items.Should().ContainKey("key_read").WhoseValue.Should().Be("value");
    }

    // ── Test middleware implementations ────────────────────────────────────

    private sealed class LoggingMiddleware(string name, List<string> log) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
        {
            log.Add(name);
            return next(context);
        }
    }

    private sealed class ShortCircuitMiddleware(string name, List<string> log) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
        {
            log.Add(name);
            return Task.CompletedTask; // intentionally not calling next
        }
    }

    private sealed class ItemWriterMiddleware(string key, string value) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
        {
            context.Items[key] = value;
            return next(context);
        }
    }

    private sealed class ItemReaderMiddleware(string key) : IMqttMiddleware
    {
        public Task InvokeAsync(MqttMessageContext context, MqttRequestDelegate next)
        {
            if (context.Items.TryGetValue(key, out var val))
                context.Items[$"{key}_read"] = val;
            return next(context);
        }
    }
}
