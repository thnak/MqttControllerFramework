# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

MqttControllerFramework is an ASP.NET Core–style, **source-generated** MQTT controller framework for .NET built on top of MQTTnet 5 / MQTTnet.AspNetCore. Handlers are plain classes with `[MqttController]` + `[MqttTopic("...")]` attributes; a Roslyn incremental source generator emits the routing/dispatch code at compile time, so there is no runtime reflection in the hot path.

Published to NuGet as `MqttControllerFramework`.

## Solution layout

- `src/MqttControllerFramework` — the runtime library (multi-targets `net8.0` and `net10.0`).
- `src/MqttControllerFramework.SourceGenerators` — the Roslyn incremental generator (`netstandard2.0`, packed into the main package's `analyzers/dotnet/cs` folder). Referenced from the main project as `OutputItemType="Analyzer"` with `ReferenceOutputAssembly="false"` — it never ships as a runtime dependency.
- `tests/MqttControllerFramework.Tests` — xUnit + FluentAssertions, targets `net10.0` only.
- `docs/wiki/` — the GitHub wiki source (also linked from README); update the relevant page there when changing public-facing behavior (routing, middleware, auth, rate limiting, TLS, retained messages, etc.).

Solution file is `MqttControllerFramework.slnx` (the new XML solution format, not `.sln`).

## Build, test, run

```bash
dotnet restore MqttControllerFramework.slnx
dotnet build MqttControllerFramework.slnx -c Release
dotnet test MqttControllerFramework.slnx -c Release
```

Run a single test class/method (the test project runs on xUnit v3 / Microsoft.Testing.Platform, so use its native filter flags, not VSTest's `--filter "FullyQualifiedName~..."`):

```bash
dotnet test MqttControllerFramework.slnx --filter-class "*MqttPipelineTests"
dotnet test MqttControllerFramework.slnx --filter-method "*MqttPipelineTests.SomeTestMethod"
```

CI (`.github/workflows/ci.yml`) runs `dotnet restore` → `build -c Release` → `test -c Release` against both .NET 8 and .NET 10 SDKs on every push/PR to `master`. `.github/workflows/release.yml` builds the source generator, tests, packs, and pushes to NuGet.org + GitHub Packages on `v*.*.*` tags.

## Test project: xUnit v3 / Microsoft.Testing.Platform

`tests/MqttControllerFramework.Tests` runs on `xunit.v3` (not `xunit`/`xunit.abstractions`), builds as an executable (`OutputType=Exe`), and opts into native `dotnet test` support via `UseMicrosoftTestingPlatformRunner` + `TestingPlatformDotnetTestSupport`, with `global.json` setting `"test": {"runner": "Microsoft.Testing.Platform"}` for .NET 10+ SDKs. Assertions use `AwesomeAssertions` (an MIT-licensed community fork/continuation of FluentAssertions 7 — FluentAssertions itself went commercial-licensed at v8, so don't reintroduce it), imported via the same `FluentAssertions`-compatible API surface but under the `AwesomeAssertions` namespace (see `GlobalUsings.cs`). VSTest-style `--filter`/`--logger`/`--collect` arguments don't apply here — use the MTP equivalents (`--filter-class`, `--filter-method`, `--filter-trait`, etc; see xUnit's Microsoft.Testing.Platform docs).

When changing the source generator, remember its output only refreshes in a consuming project after that project rebuilds — if you're validating generator changes against the test project, do a full rebuild, not an incremental one.

## Architecture

### Compile-time: source generation

`MqttControllerGenerator.cs` (an `IIncrementalGenerator`) scans for classes marked `[MqttController]`, walks their `[MqttTopic("...")]`-attributed methods, and emits:
- A `GeneratedMqttControllerRegistration` class (in the consuming assembly's `<Assembly>.Mqtt.Generated` namespace) implementing `IMqttControllerRegistration` — the thing passed to `.WithControllers<TRegistration>()`.
- Per-topic dispatch code, including parameter binding: `[FromMqttTopic(n)]` binds a method parameter to the n-th `+` wildcard segment of the topic template; other parameters are deserialized from the payload.
- Topic matching via a generated DFA (`MqttTopicDfaGenerator.cs`) for byte-level matching without string allocation on the hot path.
- Compile-time diagnostics: **MQTT001** (error — duplicate topic template) and **MQTT002** (warning — ambiguous/overlapping topic patterns), computed via `TopicsConflict`/`SegmentsConflict` segment-by-segment comparison (`+` and `#` wildcard aware).
- `MqttDocumentationGenerator.cs` additionally emits Markdown documentation for discovered controllers.

Analyzer diagnostics (MQTT001/MQTT002) are tracked in `AnalyzerReleaseTracking.Shipped.md` / `.Unshipped.md` — add new rule IDs there when introducing new diagnostics (`RS2008` is the only suppressed analyzer rule, since these files track a generator, not a `DiagnosticAnalyzer`).

### Runtime: registration and request flow

1. `services.AddMqttServer(configuration)` (`MqttServerServiceCollectionExtensions`) binds `MqttServerSettings`, configures the underlying MQTTnet server (TCP/WebSocket adapters, TLS via `HotSwappableServerCertProvider` when `EnableSsl` is set), registers framework singletons (`IMqttBrokerStatsService`, `IMqttClientNetworkTracker`, `IMqttClientActionService`, `IRetainStorage`), registers `MqttBrokerHostedService`, and returns a fluent `MqttServerBuilder`.
2. `MqttServerBuilder` chains `.WithControllers<TRegistration>()` (wires the generated `IMqttRoutingService`/`MqttRouter`), `.WithAuthentication/.WithAuthorization/.WithConnectionValidator/.WithNetworkTracker/.WithRetainStorage`, `.UseMiddleware<T>()`, `.WithRateLimiting()`, and the `On*` lifecycle event hooks — each just registers a service against the underlying `IServiceCollection` and returns `this`.
3. `MqttBrokerHostedService` wires every MQTTnet `MqttServer` event (connection validation, auth, publish interception, subscribe/unsubscribe, retained messages, session/stat counters) to framework abstractions. Each incoming publish opens a **per-message DI scope**, builds a `MqttMessageContext`, and runs it through the `IMqttMiddleware` chain (registered in order, resolved from that scope — supports scoped and singleton lifetimes) via `MqttRequestDelegate`, terminating in `IMqttRoutingService.RouteAsync`, which matches the topic against the generator-built `MqttRouter` route table and invokes the target controller method.
4. Errors inside broker event handlers are caught and logged (`Safe<T>` wrapper) rather than propagating into MQTTnet's event pipeline.

### Key extension points (all listed in README's Full Builder API table)

`IMqttAuthenticationProvider`, `IMqttAuthorizationProvider`, `IMqttConnectionValidator` (pre-auth; can seed `SessionItems` used by later middleware/handlers), `IMqttClientNetworkTracker`, `IRetainStorage`, `IMqttMiddleware`, `IMqttPayloadParser<T>` (via `IMqttPayloadParserRegistry`), and the four `IMqttClient*Event` lifecycle interfaces. Rate limiting is per-route via `[TokenBucketRateLimit(...)]` on a handler method, backed by `IMqttRateLimitService` (token-bucket strategy under `RateLimiting/Strategies`) and only takes effect once `.WithRateLimiting()` is called.

### net8.0 / net10.0 multi-targeting

Runtime code guards net10-only APIs with `#if NET10_0_OR_GREATER` (e.g. `Serialization/FrameworkJsonContext.cs` uses source-generated `System.Text.Json` serialization only on net10+). When touching serialization or anything framework-version-sensitive, verify both TFMs still build (`dotnet build -f net8.0` / `-f net10.0`), since the test project only targets `net10.0`.

## CodeGraph

This repo has a `.codegraph/` index — use `codegraph_explore` (or `codegraph explore "..."` in shell) before grepping/reading files to trace call paths across the generator → generated code → runtime pipeline boundary, since the generated dispatch code itself isn't present in this repo (it only exists in a consuming project's build output).
