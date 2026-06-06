using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MqttControllerFramework.SourceGenerators;

/// <summary>
/// Discovers MQTT controllers and generates compile-time route registration,
/// per-controller dispatchers, and an optimised routing service.
/// </summary>
[Generator]
public class MqttControllerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var controllers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetControllerInfo(ctx))
            .Where(static m => m is not null);

        var combined = context.CompilationProvider.Combine(controllers.Collect());
        context.RegisterSourceOutput(combined, static (spc, src) => Execute(src.Left, src.Right!, spc));
    }

    // ── Model extraction ──────────────────────────────────────────────────────

    private static MqttControllerInfo? GetControllerInfo(GeneratorSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol sym) return null;

        var attr = sym.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "MqttControllerAttribute" or "MqttController");
        if (attr == null) return null;

        string prefix = attr.ConstructorArguments.Length > 0
            ? attr.ConstructorArguments[0].Value?.ToString() ?? ""
            : "";

        var methods = sym.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .Select(GetMethodInfo)
            .Where(m => m != null)
            .ToList();

        if (methods.Count == 0) return null;

        return new MqttControllerInfo
        {
            ClassName = sym.Name,
            Namespace = GetNamespace(sym),
            FullyQualifiedName = sym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Prefix = prefix,
            Methods = methods!
        };
    }

    private static MqttMethodInfo? GetMethodInfo(IMethodSymbol method)
    {
        var topicAttr = method.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "MqttTopicAttribute" or "MqttTopic");
        if (topicAttr == null) return null;

        string template = topicAttr.ConstructorArguments.Length > 0
            ? topicAttr.ConstructorArguments[0].Value?.ToString() ?? ""
            : "";

        var topicParams = method.Parameters
            .Select((p, i) => (p, i, wi: GetWildcardIndex(p)))
            .Where(x => x.wi != null)
            .Select(x => new MqttTopicParamInfo
            {
                Name = x.p.Name,
                Type = x.p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsEnum = x.p.Type.TypeKind == TypeKind.Enum,
                WildcardIndex = x.wi!.Value,
                ParameterIndex = x.i
            }).ToList();

        var allParams = method.Parameters.Select((p, i) =>
        {
            var ts = p.Type as INamedTypeSymbol;
            var ctAttr = ts?.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "MqttPayloadContentTypeAttribute");
            var ct = ctAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();
            var hasParser = ts?.AllInterfaces
                .Any(iface => iface.Name == "IMqttPayloadParser" && iface.TypeArguments.Length == 1) ?? false;
            return new MqttParamInfo
            {
                Name = p.Name,
                Type = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Index = i,
                IsFromTopic = topicParams.Any(tp => tp.ParameterIndex == i),
                IsCancellationToken = p.Type.ToDisplayString() == "System.Threading.CancellationToken",
                IsByteArray = p.Type.ToDisplayString() == "byte[]",
                IsInterceptingPublishEventArgs = p.Type.ToDisplayString().Contains("InterceptingPublishEventArgs"),
                ContentType = ct,
                ImplementsCustomParser = hasParser,
                IsJsonDeserialized = !topicParams.Any(tp => tp.ParameterIndex == i)
                                     && p.Type.ToDisplayString() != "System.Threading.CancellationToken"
                                     && p.Type.ToDisplayString() != "byte[]"
                                     && !p.Type.ToDisplayString().Contains("InterceptingPublishEventArgs")
                                     && !hasParser
            };
        }).ToList();

        var rateLimits = method.GetAttributes()
            .Where(a => IsRateLimitAttr(a.AttributeClass))
            .Select(GetRateLimitInfo)
            .Where(r => r != null)
            .ToList();

        string? responseType = null;
        bool returnsTaskWithValue = false, returnsValueTask = false;
        if (method.ReturnType is INamedTypeSymbol rt && !method.ReturnsVoid
            && rt.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks"
            && rt.Name is "Task" or "ValueTask")
        {
            returnsValueTask = rt.Name == "ValueTask";
            if (rt.TypeArguments.Length == 1)
            {
                responseType = rt.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                returnsTaskWithValue = true;
            }
        }

        return new MqttMethodInfo
        {
            MethodName = method.Name,
            TopicTemplate = template,
            TopicParameters = topicParams,
            AllParameters = allParams,
            ReturnsTask = !method.ReturnsVoid &&
                          (method.ReturnType.ToDisplayString().StartsWith("System.Threading.Tasks.Task") ||
                           method.ReturnType.ToDisplayString().StartsWith("System.Threading.Tasks.ValueTask")),
            ReturnsTaskWithValue = returnsTaskWithValue,
            ReturnsValueTask = returnsValueTask,
            ResponseType = responseType,
            RateLimitAttributes = rateLimits!,
            AuthInfo = GetAuthInfo(method)
        };
    }

    private static AuthInfo GetAuthInfo(IMethodSymbol method)
    {
        var info = new AuthInfo();
        if (method.GetAttributes().Any(a => a.AttributeClass?.Name is "MqttAllowAnonymousAttribute" or "MqttAllowAnonymous"))
        {
            info.AllowAnonymous = true;
            return info;
        }
        var methodAttr = method.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "MqttAuthorizeAttribute" or "MqttAuthorize");
        var classAttr = method.ContainingType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "MqttAuthorizeAttribute" or "MqttAuthorize");
        var eff = methodAttr ?? classAttr;
        if (eff == null) return info;
        info.RequiresAuth = true;
        foreach (var na in eff.NamedArguments)
            switch (na.Key)
            {
                case "Roles": info.RequiredRoles = na.Value.Value?.ToString(); break;
                case "Policies": info.RequiredPolicies = na.Value.Value?.ToString(); break;
                case "Policy": info.AuthPolicy = na.Value.Value?.ToString(); break;
                case "RequireAllRoles": info.RequireAllRoles = (bool)(na.Value.Value ?? false); break;
                case "UnauthorizedMessage": info.UnauthorizedMessage = na.Value.Value?.ToString(); break;
            }
        return info;
    }

    private static bool IsRateLimitAttr(INamedTypeSymbol? sym)
    {
        var b = sym?.BaseType;
        while (b != null) { if (b.Name == "RateLimitAttribute") return true; b = b.BaseType; }
        return false;
    }

    private static RateLimitInfo? GetRateLimitInfo(AttributeData attr)
    {
        if (attr.AttributeClass == null) return null;
        var info = new RateLimitInfo { AttrTypeName = attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) };
        var attrName = attr.AttributeClass.Name;
        info.StrategyType = attrName.EndsWith("RateLimitAttribute")
            ? attrName.Substring(0, attrName.Length - "RateLimitAttribute".Length)
            : attrName.EndsWith("Attribute")
                ? attrName.Substring(0, attrName.Length - "Attribute".Length)
                : attrName;

        info.Priority = (int)(attr.NamedArguments.FirstOrDefault(a => a.Key == "Priority").Value.Value ?? 100);
        info.Name = attr.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value?.ToString();

        var cfg = new Dictionary<string, object>();
        if (attrName == "TokenBucketRateLimitAttribute" && attr.ConstructorArguments.Length >= 3)
        {
            cfg["Capacity"] = attr.ConstructorArguments[0].Value ?? 0;
            cfg["RefillRate"] = attr.ConstructorArguments[1].Value ?? 0;
            cfg["RefillIntervalMs"] = attr.ConstructorArguments[2].Value ?? 0;
            cfg["TokensPerRequest"] = attr.NamedArguments.FirstOrDefault(a => a.Key == "TokensPerRequest").Value.Value ?? 1;
        }
        info.Configuration = cfg;
        return info;
    }

    private static int? GetWildcardIndex(IParameterSymbol p)
    {
        var a = p.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.Name is "FromMqttTopicAttribute" or "FromMqttTopic");
        if (a == null) return null;
        return a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is int idx ? idx : 1;
    }

    private static string? GetNamespace(ISymbol sym)
    {
        var ns = sym.ContainingNamespace;
        return ns == null || ns.IsGlobalNamespace ? null : ns.ToDisplayString();
    }

    // ── Code generation ───────────────────────────────────────────────────────

    private static void Execute(Compilation compilation, ImmutableArray<MqttControllerInfo> controllers, SourceProductionContext ctx)
    {
        // Diagnostic stub
        var diag = new StringBuilder();
        diag.AppendLine("// <auto-generated />");
        diag.AppendLine($"// MqttControllerGenerator — assembly: {compilation.AssemblyName}, controllers: {controllers.Length}");
        foreach (var c in controllers)
        {
            diag.AppendLine($"// {c.FullyQualifiedName} (prefix: '{c.Prefix}')");
            foreach (var m in c.Methods) diag.AppendLine($"//   {m.MethodName} → {m.TopicTemplate}");
        }
        ctx.AddSource("_MqttDiagnostic.g.cs", SourceText.From(diag.ToString(), Encoding.UTF8));

        if (controllers.Length == 0) return;

        ctx.AddSource("MqttControllerRegistration.g.cs",
            SourceText.From(GenerateRegistration(compilation, controllers), Encoding.UTF8));

        foreach (var c in controllers)
            ctx.AddSource($"{c.ClassName}Dispatcher.g.cs",
                SourceText.From(GenerateDispatcher(c), Encoding.UTF8));

        ctx.AddSource("GeneratedMqttRoutingService.g.cs",
            SourceText.From(GenerateRoutingService(compilation, controllers), Encoding.UTF8));
    }

    // ── Registration class ────────────────────────────────────────────────────

    private static string GenerateRegistration(Compilation compilation, ImmutableArray<MqttControllerInfo> controllers)
    {
        var generatedNs = $"{compilation.AssemblyName}.Mqtt.Generated";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using MqttControllerFramework;");
        sb.AppendLine("using MqttControllerFramework.Routing;");
        sb.AppendLine("using MqttControllerFramework.RateLimiting;");
        sb.AppendLine();
        sb.AppendLine($"namespace {generatedNs}");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"MqttControllerFramework.SourceGenerators\", \"1.0.0\")]");
        sb.AppendLine("    public sealed class GeneratedMqttControllerRegistration : IMqttControllerRegistration");
        sb.AppendLine("    {");

        // GetControllerTypes
        sb.AppendLine("        public IReadOnlyList<Type> GetControllerTypes() => new Type[]");
        sb.AppendLine("        {");
        foreach (var c in controllers)
            sb.AppendLine($"            typeof({c.FullyQualifiedName}),");
        sb.AppendLine("        };");
        sb.AppendLine();

        // GetRoutes
        sb.AppendLine("        public IReadOnlyList<MqttRoute> GetRoutes()");
        sb.AppendLine("        {");
        sb.AppendLine("            var routes = new List<MqttRoute>();");
        foreach (var c in controllers)
        {
            var ns = c.Namespace ?? generatedNs;
            foreach (var m in c.Methods)
            {
                var tpl = FinalTemplate(c.Prefix, m.TopicTemplate);
                sb.AppendLine($"            // {c.ClassName}.{m.MethodName}");
                sb.AppendLine("            routes.Add(new MqttRoute");
                sb.AppendLine("            {");
                sb.AppendLine($"                TopicTemplate = \"{tpl}\",");
                sb.AppendLine($"                ControllerType = typeof({c.FullyQualifiedName}),");
                sb.AppendLine($"                HandlerMethod = typeof({c.FullyQualifiedName}).GetMethod(\"{m.MethodName}\")!,");
                sb.AppendLine($"                TopicParameters = new Dictionary<int, System.Reflection.ParameterInfo>");
                sb.AppendLine("                {");
                foreach (var tp in m.TopicParameters)
                    sb.AppendLine($"                    {{ {tp.WildcardIndex}, typeof({c.FullyQualifiedName}).GetMethod(\"{m.MethodName}\")!.GetParameters()[{tp.ParameterIndex}] }},");
                sb.AppendLine("                },");
                sb.AppendLine($"                DispatcherType = typeof({ns}.I{c.ClassName}Dispatcher),");
                sb.AppendLine($"                DispatcherMethodName = \"Dispatch_{m.MethodName}\",");
                if (m.RateLimitAttributes.Count > 0)
                {
                    sb.AppendLine("                RateLimitConfigs = new List<RouteRateLimitConfig>");
                    sb.AppendLine("                {");
                    foreach (var rl in m.RateLimitAttributes.OrderBy(a => a.Priority))
                    {
                        sb.AppendLine("                    new RouteRateLimitConfig");
                        sb.AppendLine("                    {");
                        sb.AppendLine($"                        StrategyType = \"{rl.StrategyType}\",");
                        sb.AppendLine($"                        Priority = {rl.Priority},");
                        if (!string.IsNullOrEmpty(rl.Name)) sb.AppendLine($"                        Name = \"{rl.Name}\",");
                        sb.AppendLine("                        Configuration = new Dictionary<string, object>");
                        sb.AppendLine("                        {");
                        foreach (var kv in rl.Configuration)
                            sb.AppendLine($"                            [\"{kv.Key}\"] = {kv.Value},");
                        sb.AppendLine("                        }");
                        sb.AppendLine("                    },");
                    }
                    sb.AppendLine("                },");
                }
                sb.AppendLine($"                RequiresAuthorization = {m.AuthInfo.RequiresAuth.ToString().ToLower()},");
                sb.AppendLine($"                AllowAnonymous = {m.AuthInfo.AllowAnonymous.ToString().ToLower()},");
                sb.AppendLine("            });");
                sb.AppendLine();
            }
        }
        sb.AppendLine("            return routes;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // RegisterDispatchers — also registers routing service as IMqttRoutingService
        sb.AppendLine("        public void RegisterDispatchers(Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");
        sb.AppendLine($"            services.AddSingleton<MqttControllerFramework.Routing.IMqttRoutingService, {generatedNs}.GeneratedMqttRoutingService>();");
        foreach (var c in controllers)
        {
            var ns = c.Namespace ?? generatedNs;
            sb.AppendLine($"            services.AddScoped<{ns}.I{c.ClassName}Dispatcher, {ns}.{c.ClassName}Dispatcher>();");
        }
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Dispatcher class ──────────────────────────────────────────────────────

    private static string GenerateDispatcher(MqttControllerInfo controller)
    {
        var ns = controller.Namespace ?? "Generated";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Buffers;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using Microsoft.Extensions.Options;");
        sb.AppendLine("using MQTTnet;");
        sb.AppendLine("using MQTTnet.Server;");
        sb.AppendLine("using MqttControllerFramework.Abstracts;");
        sb.AppendLine("using MqttControllerFramework.Configuration;");
        sb.AppendLine("using MqttControllerFramework.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");

        // Interface
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"MqttControllerFramework.SourceGenerators\", \"1.0.0\")]");
        sb.AppendLine($"    public interface I{controller.ClassName}Dispatcher");
        sb.AppendLine("    {");
        foreach (var m in controller.Methods)
            sb.AppendLine($"        ValueTask Dispatch_{m.MethodName}(InterceptingPublishEventArgs args, string topic, CancellationToken cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Implementation
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"MqttControllerFramework.SourceGenerators\", \"1.0.0\")]");
        sb.AppendLine($"    public sealed partial class {controller.ClassName}Dispatcher : I{controller.ClassName}Dispatcher");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {controller.FullyQualifiedName} _controller;");
        sb.AppendLine("        private readonly JsonSerializerOptions _jsonOptions;");
        sb.AppendLine("        private readonly IMqttJsonTypeInfoCache _typeInfoCache;");
        sb.AppendLine("        private readonly IMqttPayloadParserRegistry _parserRegistry;");
        sb.AppendLine("        private readonly MQTTnet.Server.MqttServer _mqttServer;");
        sb.AppendLine($"        private readonly ILogger<{controller.ClassName}Dispatcher> _logger;");
        sb.AppendLine("        private readonly string _originName;");
        sb.AppendLine("        private readonly ReadOnlyMemory<byte> _originValue;");
        sb.AppendLine();
        sb.AppendLine($"        public {controller.ClassName}Dispatcher(");
        sb.AppendLine($"            {controller.FullyQualifiedName} controller,");
        sb.AppendLine("            JsonSerializerOptions jsonOptions,");
        sb.AppendLine("            IMqttJsonTypeInfoCache typeInfoCache,");
        sb.AppendLine("            IMqttPayloadParserRegistry parserRegistry,");
        sb.AppendLine("            MQTTnet.Server.MqttServer mqttServer,");
        sb.AppendLine("            IOptions<MqttServerSettings> settings,");
        sb.AppendLine($"            ILogger<{controller.ClassName}Dispatcher> logger)");
        sb.AppendLine("        {");
        sb.AppendLine("            _controller = controller;");
        sb.AppendLine("            _jsonOptions = jsonOptions;");
        sb.AppendLine("            _typeInfoCache = typeInfoCache;");
        sb.AppendLine("            _parserRegistry = parserRegistry;");
        sb.AppendLine("            _mqttServer = mqttServer;");
        sb.AppendLine("            _logger = logger;");
        sb.AppendLine("            _originName = settings.Value.ServerOriginPropertyName;");
        sb.AppendLine("            _originValue = Encoding.UTF8.GetBytes(settings.Value.ServerOriginPropertyValue);");

        // Register custom parsers
        var seen = new HashSet<string>();
        foreach (var m in controller.Methods)
            foreach (var p in m.AllParameters.Where(x => x.ImplementsCustomParser && !string.IsNullOrEmpty(x.ContentType)))
                if (seen.Add($"{p.ContentType}|{p.Type}"))
                    sb.AppendLine($"            _parserRegistry.RegisterParser(\"{p.ContentType}\", typeof({p.Type}), () => new {p.Type}());");

        sb.AppendLine("        }");
        sb.AppendLine();

        // Dispatch methods
        foreach (var m in controller.Methods)
        {
            var tpl = FinalTemplate(controller.Prefix, m.TopicTemplate);
            sb.AppendLine($"        public async ValueTask Dispatch_{m.MethodName}(InterceptingPublishEventArgs args, string topic, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            LogDispatch_{m.MethodName}(topic);");

            if (m.TopicParameters.Any())
                sb.AppendLine("            var topicSegments = topic.Split('/');");

            foreach (var p in m.AllParameters)
            {
                if (p.IsFromTopic)
                {
                    var tp = m.TopicParameters.First(x => x.Name == p.Name);
                    int segIdx = GetSegmentIndex(tpl, tp.WildcardIndex);
                    if (tp.IsEnum)
                        sb.AppendLine($"            var {p.Name} = Enum.TryParse<{p.Type}>(topicSegments[{segIdx}], true, out var _{p.Name}) ? _{p.Name} : default({p.Type});");
                    else
                        sb.AppendLine($"            var {p.Name} = ({p.Type})Convert.ChangeType(topicSegments[{segIdx}], typeof({p.Type}));");
                }
                else if (p.IsByteArray)
                {
                    sb.AppendLine($"            byte[] {p.Name} = args.ApplicationMessage.Payload.IsSingleSegment");
                    sb.AppendLine($"                ? args.ApplicationMessage.Payload.FirstSpan.ToArray()");
                    sb.AppendLine($"                : args.ApplicationMessage.Payload.ToArray();");
                }
                else if (p.ImplementsCustomParser)
                {
                    sb.AppendLine($"            var {p.Name} = default({p.Type});");
                    sb.AppendLine($"            if (args.ApplicationMessage.Payload.Length > 0)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var parser = _parserRegistry.TryGetParser(args.ApplicationMessage.ContentType, typeof({p.Type}))");
                    sb.AppendLine($"                    ?? _parserRegistry.TryGetParserByType(typeof({p.Type}));");
                    sb.AppendLine($"                if (parser is IMqttPayloadParser<{p.Type}> typedParser)");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    if (args.ApplicationMessage.Payload.IsSingleSegment)");
                    sb.AppendLine($"                        {p.Name} = typedParser.Parse(args.ApplicationMessage.Payload.FirstSpan);");
                    sb.AppendLine("                    else");
                    sb.AppendLine("                    {");
                    sb.AppendLine($"                        var len = (int)args.ApplicationMessage.Payload.Length;");
                    sb.AppendLine($"                        var rent = ArrayPool<byte>.Shared.Rent(len);");
                    sb.AppendLine("                        try");
                    sb.AppendLine("                        {");
                    sb.AppendLine($"                            args.ApplicationMessage.Payload.CopyTo(rent);");
                    sb.AppendLine($"                            {p.Name} = typedParser.Parse(rent.AsSpan(0, len));");
                    sb.AppendLine("                        }");
                    sb.AppendLine("                        finally { ArrayPool<byte>.Shared.Return(rent); }");
                    sb.AppendLine("                    }");
                    sb.AppendLine("                }");
                    sb.AppendLine("                else");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    var ti = _typeInfoCache.TryGet(typeof({p.Type})) ?? _jsonOptions.GetTypeInfo(typeof({p.Type}));");
                    sb.AppendLine($"                    if (ti != null) {{ var r = new Utf8JsonReader(args.ApplicationMessage.Payload); {p.Name} = ({p.Type})JsonSerializer.Deserialize(ref r, ti)!; }}");
                    sb.AppendLine("                }");
                    sb.AppendLine("            }");
                }
                else if (!p.IsInterceptingPublishEventArgs && !p.IsCancellationToken)
                {
                    sb.AppendLine($"            var {p.Name} = default({p.Type});");
                    sb.AppendLine($"            if (args.ApplicationMessage.Payload.Length > 0)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var ti = _typeInfoCache.TryGet(typeof({p.Type})) ?? _jsonOptions.GetTypeInfo(typeof({p.Type}));");
                    sb.AppendLine($"                if (ti != null) {{ var r = new Utf8JsonReader(args.ApplicationMessage.Payload); {p.Name} = ({p.Type})JsonSerializer.Deserialize(ref r, ti)!; }}");
                    sb.AppendLine("            }");
                }
            }

            // Method call
            var callArgs = string.Join(", ", m.AllParameters.Select(p =>
                p.IsInterceptingPublishEventArgs ? "args" :
                p.IsCancellationToken ? "cancellationToken" :
                p.IsJsonDeserialized ? $"{p.Name}!" : p.Name));

            if (m.ReturnsTaskWithValue && m.ResponseType != null)
                sb.AppendLine($"            var result = await _controller.{m.MethodName}({callArgs}){(m.ReturnsValueTask ? ".AsTask()" : "")};");
            else if (m.ReturnsTask)
                sb.AppendLine($"            await _controller.{m.MethodName}({callArgs}){(m.ReturnsValueTask ? ".AsTask()" : "")};");
            else
                sb.AppendLine($"            _controller.{m.MethodName}({callArgs});");

            // Response publish
            if (m.ReturnsTaskWithValue && m.ResponseType != null)
            {
                sb.AppendLine("            var responseTopic = args.ApplicationMessage.ResponseTopic;");
                sb.AppendLine("            if (!string.IsNullOrEmpty(responseTopic))");
                sb.AppendLine("            {");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var rti = _typeInfoCache.TryGet(typeof({m.ResponseType}));");
                sb.AppendLine($"                    var responseJson = rti != null");
                sb.AppendLine($"                        ? JsonSerializer.SerializeToUtf8Bytes(result, rti)");
                sb.AppendLine($"                        : JsonSerializer.SerializeToUtf8Bytes(result, typeof({m.ResponseType}), _jsonOptions);");
                sb.AppendLine("                    var msg = new MQTTnet.MqttApplicationMessageBuilder()");
                sb.AppendLine("                        .WithTopic(responseTopic)");
                sb.AppendLine("                        .WithPayload(responseJson)");
                sb.AppendLine("                        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)");
                sb.AppendLine("                        .WithUserProperty(_originName, _originValue)");
                sb.AppendLine("                        .Build();");
                sb.AppendLine("                    await _mqttServer.InjectApplicationMessage(");
                sb.AppendLine("                        new MQTTnet.Server.InjectedMqttApplicationMessage(msg) { SenderClientId = \"MqttServer\" },");
                sb.AppendLine("                        cancellationToken);");
                sb.AppendLine("                }");
                sb.AppendLine("                catch (Exception ex) { LogFailedToSendResponse(responseTopic, ex); }");
                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Logger methods
        foreach (var m in controller.Methods)
        {
            sb.AppendLine($"        [LoggerMessage(LogLevel.Trace, \"Dispatching MQTT '{{Topic}}' → {controller.ClassName}.{m.MethodName}\")]");
            sb.AppendLine($"        partial void LogDispatch_{m.MethodName}(string Topic);");
            sb.AppendLine();
        }
        sb.AppendLine("        [LoggerMessage(LogLevel.Error, \"Failed to send response to '{ResponseTopic}'\")]");
        sb.AppendLine("        partial void LogFailedToSendResponse(string ResponseTopic, Exception ex);");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Routing service ───────────────────────────────────────────────────────

    private static string GenerateRoutingService(Compilation compilation, ImmutableArray<MqttControllerInfo> controllers)
    {
        var generatedNs = $"{compilation.AssemblyName}.Mqtt.Generated";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using MQTTnet;");
        sb.AppendLine("using MQTTnet.Server;");
        sb.AppendLine("using MqttControllerFramework.Authorization;");
        sb.AppendLine("using MqttControllerFramework.Pipeline;");
        sb.AppendLine("using MqttControllerFramework.Routing;");
        sb.AppendLine();
        sb.AppendLine($"namespace {generatedNs}");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"MqttControllerFramework.SourceGenerators\", \"1.0.0\")]");
        sb.AppendLine("    public sealed class GeneratedMqttRoutingService : IMqttRoutingService");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly ILogger<GeneratedMqttRoutingService> _logger;");
        sb.AppendLine("        private readonly RouteMatchCache _cache = new(128);");
        sb.AppendLine();
        sb.AppendLine("        public GeneratedMqttRoutingService(ILogger<GeneratedMqttRoutingService> logger)");
        sb.AppendLine("            => _logger = logger;");
        sb.AppendLine();

        // IsRouteRegistered
        sb.AppendLine("        public bool IsRouteRegistered(string topic)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_cache.TryGetRoute(topic) >= 0) return true;");
        sb.AppendLine("            var bytes = Encoding.UTF8.GetBytes(topic);");
        sb.AppendLine("            var id = MatchTopicPattern(bytes);");
        sb.AppendLine("            if (id >= 0) { _cache.AddRoute(topic, id); return true; }");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // RouteAsync — pipeline terminal called by MqttBrokerHostedService after middleware
        sb.AppendLine("        public async Task RouteAsync(MqttMessageContext context)");
        sb.AppendLine("        {");
        sb.AppendLine("            var args = context.Args;");
        sb.AppendLine("            var sp = context.Services;");
        sb.AppendLine("            var topic = args.ApplicationMessage.Topic;");
        sb.AppendLine("            var ct = args.CancellationToken;");
        sb.AppendLine();
        sb.AppendLine("            var cachedId = _cache.TryGetRoute(topic);");
        sb.AppendLine("            if (cachedId >= 0) { await DispatchById(cachedId, args, topic, sp, ct); return; }");
        sb.AppendLine();
        sb.AppendLine("            var bytes = Encoding.UTF8.GetBytes(topic);");
        sb.AppendLine("            var routeId = MatchTopicPattern(bytes);");
        sb.AppendLine("            if (routeId >= 0) { _cache.AddRoute(topic, routeId); await DispatchById(routeId, args, topic, sp, ct); }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // DFA
        var dfa = new MqttTopicDfaGenerator();
        int rid = 0;
        foreach (var c in controllers)
            foreach (var m in c.Methods)
                dfa.AddPattern(FinalTemplate(c.Prefix, m.TopicTemplate), rid++);
        sb.Append(dfa.GenerateDfaMatchingCode());
        sb.AppendLine();

        // DispatchById
        sb.AppendLine("        private async ValueTask DispatchById(int routeId, InterceptingPublishEventArgs args, string topic, IServiceProvider sp, System.Threading.CancellationToken ct)");
        sb.AppendLine("        {");
        sb.AppendLine("            args.ProcessPublish = false;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                switch (routeId)");
        sb.AppendLine("                {");

        int dispId = 0;
        foreach (var c in controllers)
        {
            var ns = c.Namespace ?? generatedNs;
            foreach (var m in c.Methods)
            {
                sb.AppendLine($"                    case {dispId}:");
                sb.AppendLine("                    {");

                if (m.AuthInfo.RequiresAuth && !m.AuthInfo.AllowAnonymous)
                {
                    sb.AppendLine("                        var authProvider = sp.GetService<IMqttAuthorizationProvider>();");
                    sb.AppendLine("                        if (authProvider != null)");
                    sb.AppendLine("                        {");
                    sb.AppendLine("                            var authResult = await authProvider.AuthorizePublishAsync(");
                    sb.AppendLine("                                args.UserName, topic,");
                    sb.AppendLine("                                (int)args.ApplicationMessage.QualityOfServiceLevel,");
                    sb.AppendLine("                                args.ApplicationMessage.Retain, ct);");
                    sb.AppendLine("                            if (!authResult.IsAuthorized)");
                    sb.AppendLine("                            {");
                    var msg = m.AuthInfo.UnauthorizedMessage ?? "Not authorized to publish to this topic";
                    sb.AppendLine($"                                _logger.LogWarning(\"Publish denied for '{{U}}' on '{{T}}': {{R}}\", args.UserName, topic, authResult.DenialReason ?? \"{msg}\");");
                    sb.AppendLine("                                args.Response.ReasonCode = MQTTnet.Protocol.MqttPubAckReasonCode.NotAuthorized;");
                    sb.AppendLine($"                                args.Response.ReasonString = authResult.DenialReason ?? \"{msg}\";");
                    sb.AppendLine("                                return;");
                    sb.AppendLine("                            }");
                    sb.AppendLine("                            var qos = (int)args.ApplicationMessage.QualityOfServiceLevel;");
                    sb.AppendLine("                            if (authResult.MaxQoS.HasValue && qos > authResult.MaxQoS.Value)");
                    sb.AppendLine("                            {");
                    sb.AppendLine("                                args.Response.ReasonCode = MQTTnet.Protocol.MqttPubAckReasonCode.QuotaExceeded;");
                    sb.AppendLine("                                args.Response.ReasonString = $\"QoS {qos} exceeds maximum {authResult.MaxQoS.Value}\";");
                    sb.AppendLine("                                return;");
                    sb.AppendLine("                            }");
                    sb.AppendLine("                            if (args.ApplicationMessage.Retain && authResult.AllowRetain == false)");
                    sb.AppendLine("                            {");
                    sb.AppendLine("                                args.Response.ReasonCode = MQTTnet.Protocol.MqttPubAckReasonCode.NotAuthorized;");
                    sb.AppendLine("                                args.Response.ReasonString = \"Retain flag not allowed\";");
                    sb.AppendLine("                                return;");
                    sb.AppendLine("                            }");
                    sb.AppendLine("                        }");
                }

                sb.AppendLine($"                        var d = sp.GetRequiredService<{ns}.I{c.ClassName}Dispatcher>();");
                sb.AppendLine($"                        await d.Dispatch_{m.MethodName}(args, topic, ct);");
                sb.AppendLine("                        break;");
                sb.AppendLine("                    }");
                dispId++;
            }
        }

        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine("                _logger.LogError(ex, \"Error executing MQTT handler for topic '{Topic}'\", topic);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // RouteMatchCache
        sb.AppendLine("        private sealed class RouteMatchCache(int capacity)");
        sb.AppendLine("        {");
        sb.AppendLine("            private readonly CacheEntry[] _e = new CacheEntry[capacity];");
        sb.AppendLine("            private int _next;");
        sb.AppendLine("            public int TryGetRoute(string topic)");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = 0; i < capacity; i++)");
        sb.AppendLine("                    if (_e[i].Topic != null && string.Equals(_e[i].Topic, topic, StringComparison.Ordinal))");
        sb.AppendLine("                        return _e[i].RouteId;");
        sb.AppendLine("                return -1;");
        sb.AppendLine("            }");
        sb.AppendLine("            public void AddRoute(string topic, int routeId)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var e = ref _e[_next];");
        sb.AppendLine("                e.Topic = topic; e.RouteId = routeId;");
        sb.AppendLine("                _next = (_next + 1) % capacity;");
        sb.AppendLine("            }");
        sb.AppendLine("            private struct CacheEntry { public string? Topic; public int RouteId; }");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FinalTemplate(string prefix, string template)
        => string.Join("/", new[] { prefix, template }.Where(s => !string.IsNullOrEmpty(s)));

    private static int GetSegmentIndex(string template, int wildcardIndex)
    {
        var segs = template.Split('/');
        int seen = 0;
        for (int i = 0; i < segs.Length; i++)
            if (segs[i] == "+" && ++seen == wildcardIndex) return i;
        return -1;
    }

    // ── Model classes ─────────────────────────────────────────────────────────

    private class MqttControllerInfo
    {
        public string ClassName { get; set; } = "";
        public string? Namespace { get; set; }
        public string FullyQualifiedName { get; set; } = "";
        public string Prefix { get; set; } = "";
        public List<MqttMethodInfo> Methods { get; set; } = new();
    }

    private class MqttMethodInfo
    {
        public string MethodName { get; set; } = "";
        public string TopicTemplate { get; set; } = "";
        public List<MqttTopicParamInfo> TopicParameters { get; set; } = new();
        public List<MqttParamInfo> AllParameters { get; set; } = new();
        public bool ReturnsTask { get; set; }
        public bool ReturnsTaskWithValue { get; set; }
        public bool ReturnsValueTask { get; set; }
        public string? ResponseType { get; set; }
        public List<RateLimitInfo> RateLimitAttributes { get; set; } = new();
        public AuthInfo AuthInfo { get; set; } = new();
    }

    private class MqttTopicParamInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool IsEnum { get; set; }
        public int WildcardIndex { get; set; }
        public int ParameterIndex { get; set; }
    }

    private class MqttParamInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int Index { get; set; }
        public bool IsFromTopic { get; set; }
        public bool IsCancellationToken { get; set; }
        public bool IsByteArray { get; set; }
        public bool IsInterceptingPublishEventArgs { get; set; }
        public bool IsJsonDeserialized { get; set; }
        public string? ContentType { get; set; }
        public bool ImplementsCustomParser { get; set; }
    }

    private class AuthInfo
    {
        public bool RequiresAuth { get; set; }
        public bool AllowAnonymous { get; set; }
        public string? RequiredRoles { get; set; }
        public string? RequiredPolicies { get; set; }
        public string? AuthPolicy { get; set; }
        public bool RequireAllRoles { get; set; }
        public string? UnauthorizedMessage { get; set; }
    }

    private class RateLimitInfo
    {
        public string AttrTypeName { get; set; } = "";
        public string StrategyType { get; set; } = "";
        public int Priority { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
    }
}
