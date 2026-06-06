using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MqttControllerFramework.SourceGenerators;

/// <summary>
/// Generates a Deterministic Finite Automaton (DFA) for MQTT topic pattern matching.
/// Uses unsafe byte-level operations for maximum performance.
/// </summary>
internal sealed class MqttTopicDfaGenerator
{
    private readonly List<RoutePattern> _patterns = new();

    public void AddPattern(string pattern, int routeId) =>
        _patterns.Add(new RoutePattern(pattern, routeId));

    public string GenerateDfaMatchingCode()
    {
        var sb = new StringBuilder();

        var exact = _patterns.Where(p => !p.HasWildcard).ToList();
        var single = _patterns.Where(p => p.HasSingleWildcard && !p.HasMultiLevelWildcard).ToList();
        var multi = _patterns.Where(p => p.HasMultiLevelWildcard).ToList();

        GenerateByteConstants(sb);
        GenerateStaticByteArrays(sb, exact);

        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]");
        sb.AppendLine("        private static unsafe int MatchTopicPattern(global::System.ReadOnlySpan<byte> topicBytes)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (topicBytes.IsEmpty) return -1;");
        sb.AppendLine();

        if (exact.Count > 0)
        {
            sb.AppendLine("            int hash = ComputeTopicHash(topicBytes);");
            sb.AppendLine("            switch (hash)");
            sb.AppendLine("            {");
            foreach (var p in exact)
            {
                var fn = GetFieldName(p.Pattern);
                sb.AppendLine($"                case {ComputeHash(p.Pattern)}:");
                sb.AppendLine($"                    if (ExactMatch(topicBytes, {fn})) return {p.RouteId};");
                sb.AppendLine("                    break;");
            }
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        if (single.Count > 0 || multi.Count > 0)
        {
            sb.AppendLine("            fixed (byte* topicPtr = topicBytes)");
            sb.AppendLine("            {");
            sb.AppendLine("                return MatchWildcardPatterns(topicPtr, topicBytes.Length);");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            return -1;");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        GenerateHashMethod(sb);
        GenerateExactMatchMethod(sb);
        if (single.Count > 0 || multi.Count > 0)
            GenerateWildcardMatchMethod(sb, single, multi);

        return sb.ToString();
    }

    private static void GenerateByteConstants(StringBuilder sb)
    {
        sb.AppendLine("        private const byte ByteSlash = (byte)'/';");
        sb.AppendLine();
    }

    private static void GenerateStaticByteArrays(StringBuilder sb, List<RoutePattern> exact)
    {
        if (exact.Count == 0) return;
        foreach (var p in exact)
            sb.AppendLine($"        private static readonly byte[] {GetFieldName(p.Pattern)} = {ByteArrayLiteral(p.Pattern)};");
        sb.AppendLine();
    }

    private static string GetFieldName(string pattern)
    {
        var sb = new StringBuilder("Pattern_");
        foreach (var c in pattern)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static string ByteArrayLiteral(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        return $"new byte[] {{ {string.Join(", ", bytes)} }}";
    }

    private static void GenerateHashMethod(StringBuilder sb)
    {
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        private static unsafe int ComputeTopicHash(global::System.ReadOnlySpan<byte> bytes)");
        sb.AppendLine("        {");
        sb.AppendLine("            const uint FnvPrime = 16777619;");
        sb.AppendLine("            const uint FnvOffset = 2166136261;");
        sb.AppendLine("            uint hash = FnvOffset;");
        sb.AppendLine("            for (int i = 0; i < bytes.Length; i++) { hash ^= bytes[i]; hash *= FnvPrime; }");
        sb.AppendLine("            return (int)hash;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateExactMatchMethod(StringBuilder sb)
    {
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        private static unsafe bool ExactMatch(global::System.ReadOnlySpan<byte> topic, global::System.ReadOnlySpan<byte> pattern)");
        sb.AppendLine("            => topic.SequenceEqual(pattern);");
        sb.AppendLine();
    }

    private static void GenerateWildcardMatchMethod(StringBuilder sb, List<RoutePattern> single, List<RoutePattern> multi)
    {
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        private static unsafe int MatchWildcardPatterns(byte* topicPtr, int topicLength)");
        sb.AppendLine("        {");
        sb.AppendLine("            int topicSegmentCount = 1;");
        sb.AppendLine("            for (int i = 0; i < topicLength; i++) if (topicPtr[i] == ByteSlash) topicSegmentCount++;");
        sb.AppendLine();

        var groups = single.Concat(multi).GroupBy(p => p.Segments.Length).OrderBy(g => g.Key);
        foreach (var group in groups)
        {
            bool hasMulti = group.Any(p => p.HasMultiLevelWildcard);
            sb.AppendLine(hasMulti
                ? $"            if (topicSegmentCount >= {group.Key - 1})"
                : $"            if (topicSegmentCount == {group.Key})");
            sb.AppendLine("            {");
            foreach (var p in group) GeneratePatternMatch(sb, p, "                ");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("            return -1;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GeneratePatternMatch(StringBuilder sb, RoutePattern p, string indent)
    {
        sb.AppendLine($"{indent}// Pattern: {p.Pattern}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    int matchPos = 0; bool match = true;");
        for (int i = 0; i < p.Segments.Length; i++)
        {
            var seg = p.Segments[i];
            if (seg == "#")
            {
                sb.AppendLine($"{indent}    goto pattern_{p.RouteId}_match;");
            }
            else if (seg == "+")
            {
                sb.AppendLine($"{indent}    while (matchPos < topicLength && topicPtr[matchPos] != ByteSlash) matchPos++;");
                if (i < p.Segments.Length - 1)
                {
                    sb.AppendLine($"{indent}    if (matchPos >= topicLength) {{ match = false; goto pattern_{p.RouteId}_end; }}");
                    sb.AppendLine($"{indent}    matchPos++;");
                }
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(seg);
                foreach (var b in bytes)
                {
                    sb.AppendLine($"{indent}    if (matchPos >= topicLength || topicPtr[matchPos++] != {b}) {{ match = false; goto pattern_{p.RouteId}_end; }}");
                }
                if (i < p.Segments.Length - 1)
                {
                    sb.AppendLine($"{indent}    if (matchPos >= topicLength || topicPtr[matchPos] != ByteSlash) {{ match = false; goto pattern_{p.RouteId}_end; }}");
                    sb.AppendLine($"{indent}    matchPos++;");
                }
            }
        }
        if (!p.HasMultiLevelWildcard)
            sb.AppendLine($"{indent}    if (matchPos != topicLength) match = false;");
        sb.AppendLine($"{indent}    pattern_{p.RouteId}_match: if (match) return {p.RouteId};");
        sb.AppendLine($"{indent}    pattern_{p.RouteId}_end: ;");
        sb.AppendLine($"{indent}}}");
    }

    private static int ComputeHash(string s)
    {
        const uint prime = 16777619, offset = 2166136261;
        uint h = offset;
        foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= prime; }
        return (int)h;
    }

    private sealed class RoutePattern
    {
        public string Pattern { get; }
        public int RouteId { get; }
        public string[] Segments { get; }
        public bool HasSingleWildcard { get; }
        public bool HasMultiLevelWildcard { get; }
        public bool HasWildcard { get; }

        public RoutePattern(string pattern, int routeId)
        {
            Pattern = pattern;
            RouteId = routeId;
            Segments = pattern.Split('/');
            HasSingleWildcard = Segments.Any(s => s == "+");
            HasMultiLevelWildcard = Segments.Any(s => s == "#");
            HasWildcard = HasSingleWildcard || HasMultiLevelWildcard;
        }
    }
}
