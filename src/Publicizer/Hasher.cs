using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Publicizer;

/// <summary>
/// Helper class for various hash related functions.
/// </summary>
internal static class Hasher
{
    // Includes the commit hash via SourceLink, so it changes on every build.
    // Feeding it into the cache key invalidates assemblies publicized by an
    // older Publicizer whose publicization logic may have differed.
    private static readonly string publicizerVersion =
        typeof(Hasher).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    internal static string ComputeHash(string assemblyPath, PublicizerAssemblyContext assemblyContext)
    {
        var sb = new StringBuilder();
        sb.Append(publicizerVersion);
        sb.Append(assemblyContext.AssemblyName);
        sb.Append(assemblyContext.IncludeCompilerGeneratedMembers);
        sb.Append(assemblyContext.IncludeVirtualMembers);
        sb.Append(assemblyContext.ExplicitlyPublicizeAssembly);
        sb.Append(assemblyContext.ExplicitlyDoNotPublicizeAssembly);
        // Delimited and tagged per set, so that "Asm:AB" cannot hash the same as "Asm:A" plus
        // "Asm:B", and so that a lone Publicize target cannot hash the same as the lone
        // DoNotPublicize target naming the same member. Sorted because these are HashSets, whose
        // enumeration order is not contractual: the same targets authored in a different order
        // must not cost a second cache entry.
        AppendPatterns(sb, "|publicize|", assemblyContext.PublicizeMemberPatterns);
        AppendPatterns(sb, "|donotpublicize|", assemblyContext.DoNotPublicizeMemberPatterns);
        if (assemblyContext.PublicizeMemberRegexPattern is not null)
        {
            sb.Append(assemblyContext.PublicizeMemberRegexPattern.ToString());
        }
        // Scope order is deliberately significant: equally specific scopes are resolved by item
        // order, so reordering two items really can change what gets publicized.
        foreach (PublicizeScope scope in assemblyContext.Scopes)
        {
            // Delimited, so that Namespace="AB" cannot hash the same as Namespace="A" Type="B".
            _ = sb.Append("|scope|").Append(scope.Deny)
                .Append('|').Append(scope.Namespace)
                .Append('|').Append(scope.TypeReflectionName)
                .Append('|').Append(scope.IncludeVirtualMembers)
                .Append('|').Append(scope.IncludeCompilerGeneratedMembers);
        }

        byte[] patternBytes = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        byte[] allBytes = [.. assemblyBytes, .. patternBytes];

        return ComputeHash(allBytes);
    }

    private static void AppendPatterns(StringBuilder sb, string tag, HashSet<string> patterns)
    {
        foreach (string pattern in patterns.OrderBy(pattern => pattern, StringComparer.Ordinal))
        {
            _ = sb.Append(tag).Append(pattern);
        }
    }

    private static string ComputeHash(byte[] bytes)
    {
        using var algorithm = MD5.Create();

        byte[] computedHash = algorithm.ComputeHash(bytes);
        var sb = new StringBuilder();
        foreach (byte b in computedHash)
        {
            sb.Append($"{b:X2}");
        }
        string hexadecimalHash = sb.ToString();

        return hexadecimalHash;
    }
}
